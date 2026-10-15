using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace WallDetailTool
{
    public partial class Commands
    {
        private const double GeometryTolerance = 1e-6;

        [CommandMethod("WDTREBAR", CommandFlags.Modal)]
        public void Rebar()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            try
            {
                var options = new PromptEntityOptions("\n选择开放式直线二维多段线: ");
                options.SetRejectMessage("\n请选择二维多段线，不支持三维多段线。");
                options.AddAllowedClass(typeof(Polyline), true);
                options.AddAllowedClass(typeof(Polyline2d), true);
                var selection = ed.GetEntity(options);
                if (selection.Status != PromptStatus.OK) return;

                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    Entity original = (Entity)tr.GetObject(selection.ObjectId, OpenMode.ForRead);
                    using (Polyline source = CopyContour(original))
                    {
                        ValidatePolyline(source);
                        // Inputs are millimetres. Unitless drawings explicitly use 1 unit = 1 mm.
                        double scale = doc.Database.Insunits == UnitsValue.Undefined ? 1.0
                            : UnitsConverter.GetConversionFactor(UnitsValue.Millimeters, doc.Database.Insunits);
                        if (!Finite(scale) || scale <= 0) throw new InvalidOperationException("无法换算图纸单位。");
                        if (doc.Database.Insunits == UnitsValue.Undefined)
                            ed.WriteMessage("\n图纸未设置单位，本命令按 1 图形单位 = 1 mm 处理。");
                        double cover, diameter;
                        if (!ReadSize(ed, "混凝土保护层厚度（mm）", 25, true, out cover) ||
                            !ReadSize(ed, "钢筋直径（mm）", 12, false, out diameter)) return;
                        double distance = (cover + diameter / 2.0) * scale;
                        if (!Finite(distance) || distance <= GeometryTolerance)
                            throw new InvalidOperationException("中心线偏移距离无效或小于几何容差。");

                        double signedDistance;
                        while (true)
                        {
                            var point = ed.GetPoint("\n在线段中部附近点选钢筋所在的一侧（避开拐角）: ");
                            if (point.Status != PromptStatus.OK) return;
                            if (TryDirection(source, point.Value, distance, out signedDistance)) break;
                            ed.WriteMessage("\n方向不明确：请在线段旁点选，不要点在线上、端部或拐角等距处。");
                        }

                        using (Polyline result = OffsetOne(source, signedDistance))
                        {
                            ValidatePolyline(result);
                            // Reject collapsed corners instead of silently dropping contour segments.
                            if (result.NumberOfVertices != source.NumberOfVertices)
                                throw new InvalidOperationException("偏移使轮廓线段消失或拓扑发生变化，请减小距离或调整轮廓。");
                            ValidateClearance(source, result, distance);
                            result.SetDatabaseDefaults(doc.Database);
                            result.ConstantWidth = 0;
                            result.Thickness = 0;
                            for (int i = 0; i < result.NumberOfVertices; i++)
                            {
                                result.SetStartWidthAt(i, 0);
                                result.SetEndWidthAt(i, 0);
                            }
                            var space = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);
                            space.AppendEntity(result);
                            tr.AddNewlyCreatedDBObject(result, true);
                            tr.Commit();
                        }
                        ed.WriteMessage("\n钢筋中心线已生成；偏移距离 = {0:g} mm。原轮廓未修改。", cover + diameter / 2.0);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n未生成钢筋中心线：{0}", ex.Message);
            }
        }

        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }

        private static bool ReadSize(Editor ed, string message, double value, bool allowZero, out double size)
        {
            var options = new PromptDoubleOptions("\n" + message)
            {
                DefaultValue = value, UseDefaultValue = true, AllowNegative = false, AllowZero = allowZero
            };
            var result = ed.GetDouble(options);
            size = result.Value;
            if (result.Status != PromptStatus.OK) return false;
            if (!Finite(size) || size < 0 || (!allowZero && size == 0))
                throw new InvalidOperationException("输入尺寸无效。");
            return true;
        }

        private static Polyline CopyContour(Entity original)
        {
            var light = original as Polyline;
            if (light != null) return (Polyline)light.Clone();
            var old = original as Polyline2d;
            if (old == null || old.PolyType != Poly2dType.SimplePoly)
                throw new InvalidOperationException("不支持拟合、样条或三维多段线。");
            var copy = new Polyline();
            try
            {
                // Convert a transient clone, never transfer the original entity's identity.
                using (var clone = (Polyline2d)old.Clone()) copy.ConvertFrom(clone, false);
                return copy;
            }
            catch { copy.Dispose(); throw; }
        }

        private static Polyline OffsetOne(Polyline source, double distance)
        {
            using (DBObjectCollection curves = source.GetOffsetCurves(distance))
            {
                try
                {
                    if (curves.Count != 1 || !(curves[0] is Polyline))
                        throw new InvalidOperationException("原生偏移未得到一条完整二维多段线（可能断裂或距离过大）。");
                    return (Polyline)curves[0].Clone();
                }
                finally { foreach (DBObject curve in curves) curve.Dispose(); }
            }
        }

        private static bool TryDirection(Polyline source, Point3d picked, double distance, out double signedDistance)
        {
            signedDistance = 0;
            // Editor points are WCS; project onto the contour plane, including tilted/elevated contours.
            Vector3d normal = source.Normal.GetNormal();
            Point3d point = picked - normal * (picked - source.StartPoint).DotProduct(normal);
            int nearest = -1;
            double best = double.MaxValue;
            bool tied = false;
            Point3d foot = Point3d.Origin;
            for (int i = 0; i < source.NumberOfVertices - 1; i++)
            {
                using (var line = Segment(source, i))
                {
                    Point3d q = line.GetClosestPointTo(point, false);
                    double d = q.DistanceTo(point);
                    if (Math.Abs(d - best) <= GeometryTolerance) tied = true;
                    else if (d < best)
                    {
                        best = d; nearest = i; foot = q; tied = false;
                    }
                }
            }
            if (nearest < 0 || tied || best <= GeometryTolerance) return false;
            if (foot.DistanceTo(source.GetPoint3dAt(nearest)) <= GeometryTolerance ||
                foot.DistanceTo(source.GetPoint3dAt(nearest + 1)) <= GeometryTolerance) return false;

            // Ask the native engine which side a positive offset means; do not assume inside/outside.
            using (var probe = new Polyline())
            {
                probe.Normal = source.Normal;
                probe.Elevation = source.Elevation;
                probe.AddVertexAt(0, source.GetPoint2dAt(nearest), 0, 0, 0);
                probe.AddVertexAt(1, source.GetPoint2dAt(nearest + 1), 0, 0, 0);
                using (Polyline positive = OffsetOne(probe, distance))
                {
                    Vector3d shift = positive.StartPoint - probe.StartPoint;
                    signedDistance = shift.DotProduct(point - foot) > 0 ? distance : -distance;
                }
            }
            return true;
        }

        private static Line Segment(Polyline p, int i)
        {
            return new Line(p.GetPoint3dAt(i), p.GetPoint3dAt(i + 1));
        }

        private static void ValidatePolyline(Polyline p)
        {
            if (p.Closed || p.NumberOfVertices < 2)
                throw new InvalidOperationException("必须是至少包含一条线段的开放式多段线。");
            for (int i = 0; i < p.NumberOfVertices; i++)
            {
                Point3d v = p.GetPoint3dAt(i);
                if (!Finite(v.X) || !Finite(v.Y) || !Finite(v.Z))
                    throw new InvalidOperationException("轮廓包含无效坐标。");
                if (!Finite(p.GetBulgeAt(i)) || p.GetBulgeAt(i) != 0)
                    throw new InvalidOperationException("暂不支持包含圆弧的多段线。");
                if (i + 1 < p.NumberOfVertices && v.DistanceTo(p.GetPoint3dAt(i + 1)) <= GeometryTolerance)
                    throw new InvalidOperationException("轮廓含零长度或过短线段。");
            }
            for (int i = 0; i < p.NumberOfVertices - 1; i++)
                using (Line a = Segment(p, i))
                    for (int j = i + 1; j < p.NumberOfVertices - 1; j++)
                        using (Line b = Segment(p, j))
                        {
                            bool adjacent = j == i + 1;
                            var hits = new Point3dCollection();
                            a.IntersectWith(b, Intersect.OnBothOperands, hits, IntPtr.Zero, IntPtr.Zero);
                            foreach (Point3d hit in hits)
                                if (!adjacent || hit.DistanceTo(a.EndPoint) > GeometryTolerance)
                                    throw new InvalidOperationException("轮廓存在自交或非相邻线段接触。");
                            // IntersectWith alone may not report collinear overlaps.
                            if (PointOn(a.StartPoint, b) || PointOn(b.EndPoint, a) ||
                                (!adjacent && (PointOn(a.EndPoint, b) || PointOn(b.StartPoint, a))))
                                throw new InvalidOperationException("轮廓存在重叠、折返或非相邻线段接触。");
                        }
        }

        private static bool PointOn(Point3d point, Line line)
        {
            return point.DistanceTo(line.GetClosestPointTo(point, false)) <= GeometryTolerance;
        }

        private static void ValidateClearance(Polyline source, Polyline result, double distance)
        {
            double tolerance = Math.Max(GeometryTolerance, distance * 1e-8);
            for (int i = 0; i < source.NumberOfVertices - 1; i++)
                using (Line a = Segment(source, i))
                    for (int j = 0; j < result.NumberOfVertices - 1; j++)
                        using (Line b = Segment(result, j))
                        {
                            var hits = new Point3dCollection();
                            a.IntersectWith(b, Intersect.OnBothOperands, hits, IntPtr.Zero, IntPtr.Zero);
                            // For disjoint coplanar straight segments, minimum distance occurs at an endpoint.
                            double gap = Math.Min(Math.Min(
                                a.StartPoint.DistanceTo(b.GetClosestPointTo(a.StartPoint, false)),
                                a.EndPoint.DistanceTo(b.GetClosestPointTo(a.EndPoint, false))), Math.Min(
                                b.StartPoint.DistanceTo(a.GetClosestPointTo(b.StartPoint, false)),
                                b.EndPoint.DistanceTo(a.GetClosestPointTo(b.EndPoint, false))));
                            if (hits.Count > 0 || gap < distance - tolerance)
                                throw new InvalidOperationException("偏移结果穿过原轮廓或局部净距不足，请调整轮廓或偏移距离。");
                        }
        }
    }
}
