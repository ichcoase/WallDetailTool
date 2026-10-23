using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace WallDetailTool
{
    // V0.1 stages 1–4 share read-only contour selection and separate geometry calculations.
    public partial class Commands
    {
        private const string ConcreteLayer = "WDT-CONCRETE";
        private static string LineLayer { get { return UpstandLayers.Read().Line; } }
        private static string DotLayer { get { return UpstandLayers.Read().Dot; } }
        private static string LabelLayer { get { return UpstandLayers.Read().Text; } }
        private static string DimensionLayer { get { return UpstandLayers.Read().Dimension; } }
        private const string AnnotationTextStyle = "TSSD";
        private const string AnnotationDimensionStyle = "TSSD_25";
        private const double AnnotationTextHeight = 62.5;
        private const double AnnotationDimensionScale = 25;
        private const double AnnotationDimensionTextHeight = AnnotationTextHeight / AnnotationDimensionScale;
        private const string ParamRecord = "WDT_UPSTAND_LAST";
        private const string BlockParamRecord = "WDT_UPSTAND_PARAMS";
        private const double Epsilon = 1e-6;

        private sealed class Rect
        {
            internal double L, R, B, T, Z;
            internal double W { get { return R - L; } }
            internal double H { get { return T - B; } }
            internal bool Same(Rect b) { return Near(L,b.L) && Near(R,b.R) && Near(B,b.B) && Near(T,b.T) && Near(Z,b.Z); }
        }

        private sealed class DetailParams
        {
            internal double Cover = 25, LineDiameter = 8, LineSpacing = 150;
            internal double DotDiameter = 6, DotSpacing = 200;
            internal double LeftAnchor, RightAnchor;
            internal double Radius = 20;
            internal double TextHeight = AnnotationTextHeight;
            internal double LineDisplayWidth = PluginSettings.ReadLineWidthMillimetres();
            internal double DotDisplayDiameter = PluginSettings.ReadDotDiameterMillimetres();
        }

        private static bool Near(double a, double b) { return Math.Abs(a-b) <= Epsilon; }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        [CommandMethod("WDTUPDOTSIZE", CommandFlags.Modal)]
        public void SetUpstandDotSize()
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                var option = new PromptDoubleOptions("\n圆点筋图形显示直径（mm）: ") { AllowNegative = false, AllowZero = false,
                    DefaultValue = PluginSettings.ReadDotDiameterMillimetres(), UseDefaultValue = true };
                PromptDoubleResult result = doc.Editor.GetDouble(option);
                if (result.Status == PromptStatus.OK) PluginSettings.SaveDotDiameterMillimetres(result.Value);
            }
            catch (System.Exception ex) { doc.Editor.WriteMessage("\n未保存设置：" + ex.Message); }
        }

        [CommandMethod("WDTUPCONTOUR", CommandFlags.Modal)]
        public void ExtractUpstandContour()
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            try
            {
                UpstandGeometry chosen=SelectUpstandGeometry(doc);
                if (chosen==null) return;
                ed.WriteMessage(string.Format("\n识别成功：{0}；左={1:g}，右={2:g}，顶={3:g}，梁顶={4:g}，宽={5:g} mm，高={6:g} mm，基点=({7:g},{8:g})。不生成钢筋或修改图元。",
                    chosen.InputKind,chosen.Left,chosen.Right,chosen.Top,chosen.BeamTop,chosen.Width,chosen.Height,chosen.BaseX,chosen.BaseY));
            }
            catch (System.Exception ex) { ed.WriteMessage("\n未提取轮廓：" + ex.Message); }
        }

        private static UpstandGeometry SelectUpstandGeometry(Document doc)
        {
            Editor ed=doc.Editor;
                ed.WriteMessage("\n点选或框选梁上反坎轮廓图元（LINE/LWPOLYLINE）；只识别所选对象，原图元不修改。");
                PromptSelectionResult selected = ed.GetSelection();
                if (selected.Status != PromptStatus.OK) return null;
                var segments = new List<ContourSegment>();
                double? elevation = null;
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject item in selected.Value)
                    {
                        if (item == null) continue;
                        Entity entity = tr.GetObject(item.ObjectId, OpenMode.ForRead) as Entity;
                        Line line = entity as Line;
                        if (line != null)
                        {
                            AddContourElevation(ref elevation, line.StartPoint.Z, line.EndPoint.Z);
                            segments.Add(new ContourSegment(new ContourPoint(line.StartPoint.X,line.StartPoint.Y),
                                new ContourPoint(line.EndPoint.X,line.EndPoint.Y)));
                            continue;
                        }
                        Polyline polyline = entity as Polyline;
                        if (polyline == null) throw new InvalidOperationException("选区含不支持的图元（仅 LINE/LWPOLYLINE），请重选。");
                        if (polyline.Normal.Z < 1-Epsilon || Math.Abs(polyline.Normal.X)>Epsilon || Math.Abs(polyline.Normal.Y)>Epsilon)
                            throw new InvalidOperationException("轮廓不在 WCS 水平二维平面，请重选。");
                        int count=polyline.NumberOfVertices;
                        if (count<2) throw new InvalidOperationException("多段线顶点不足，请重选。");
                        for (int i=0;i<(polyline.Closed ? count : count-1);i++)
                        {
                            if (Math.Abs(polyline.GetBulgeAt(i))>Epsilon) throw new InvalidOperationException("轮廓含圆弧；V0.1 只支持直线段，请重选。");
                            Point3d a=polyline.GetPoint3dAt(i), b=polyline.GetPoint3dAt((i+1)%count);
                            AddContourElevation(ref elevation,a.Z,b.Z);
                            segments.Add(new ContourSegment(new ContourPoint(a.X,a.Y),new ContourPoint(b.X,b.Y)));
                        }
                    }
                }
                if (doc.Database.Insunits != UnitsValue.Millimeters && doc.Database.Insunits != UnitsValue.Undefined)
                    ed.WriteMessage("\n注意：V0.1 固定 1 CAD 单位 = 1 mm；当前 INSUNITS 不是毫米，不执行自动换算。请确认图纸实际尺寸。");
                List<UpstandGeometry> candidates=UpstandRecognizer.Recognize(segments,elevation ?? 0);
                UpstandGeometry chosen=candidates[0];
                if (candidates.Count>1)
                {
                    ed.WriteMessage("\n找到 " + candidates.Count + " 个有效反坎候选，请点选目标内部位置。");
                    PromptPointResult target=ed.GetPoint("\n点选目标反坎内部: ");
                    if (target.Status!=PromptStatus.OK) return null;
                    chosen=UpstandRecognizer.SelectCandidate(candidates,target.Value.X,target.Value.Y);
                    // Re-run all checks after the manual decision, using only the selected input geometry.
                    candidates=UpstandRecognizer.Recognize(segments,elevation ?? 0);
                    if (!candidates.Exists(c=>c.InputKind==chosen.InputKind && Math.Abs(c.Left-chosen.Left)<Epsilon &&
                        Math.Abs(c.Right-chosen.Right)<Epsilon && Math.Abs(c.Top-chosen.Top)<Epsilon &&
                        Math.Abs(c.BeamTop-chosen.BeamTop)<Epsilon))
                        throw new InvalidOperationException("目标反坎二次校验失败，请重选。");
                }
                return chosen;
        }

        private static void AddContourElevation(ref double? elevation,double a,double b)
        {
            if (Math.Abs(a-b)>Epsilon || (elevation.HasValue && Math.Abs(elevation.Value-a)>Epsilon))
                throw new InvalidOperationException("选中图元不在同一 WCS 二维平面，请重选。");
            elevation=a;
        }

        [CommandMethod("WDTUPDRAW", CommandFlags.Modal)]
        public void DrawUpstand() { DrawUpstandCore(null); }

        internal static void DrawUpstandCore(UpstandPanelValues panelValues)
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            try
            {
                UpstandGeometry geometry=SelectUpstandGeometry(doc);
                if (geometry==null) return;
                DetailParams values = panelValues == null ? ReadLast(doc.Database) : FromPanel(panelValues);
                {
                    if (panelValues == null && !ReadParameters(ed, values)) return;
                    UpstandRebarParameters p=RebarParameters(values);
                    ed.WriteMessage("\n线筋显示宽度="+p.LineDisplayWidth.ToString("g")+" mm；点筋显示直径="+
                        p.DotDisplayDiameter.ToString("g")+" mm。固定 1 CAD 单位 = 1 mm。");
                    UpstandRebarLayout layout=UpstandRebarGeometry.Calculate(geometry,p);
                    UpstandAnnotationLayout annotations=null;
                    ObjectId dimStyleId=ObjectId.Null;
                    var annotationWarnings=new List<string>();
                        try
                        {
                            dimStyleId=UpstandDimensionStyle(doc.Database);
                            if (dimStyleId.IsNull)
                                annotationWarnings.Add("当前图纸缺少 TSSD_25 尺寸样式；生成时将按指定设置创建。");
                            using (ViewTableRecord view=ed.GetCurrentView())
                            {
                                double left=view.CenterPoint.X-view.Width/2;
                                annotations=UpstandAnnotationGeometry.Calculate(geometry,layout,p,
                                    values.TextHeight,left,left+view.Width);
                            }
                            annotationWarnings.AddRange(annotations.Warnings);
                        }
                        catch (System.Exception ex)
                        {
                            annotations=null;
                            ed.WriteMessage("\n标注布局不可用，仍可生成有效钢筋："+ex.Message);
                        }
                        foreach (string warning in annotationWarnings) ed.WriteMessage("\n"+warning);
                        var failures=new List<string>();
                        ed.Command("_.UNDO","_Begin");
                        try
                        {
                        if (annotations!=null && dimStyleId.IsNull)
                        {
                            try { PrepareArchitecturalTick(doc); }
                            catch (System.Exception ex) { failures.Add("建筑标记准备失败："+ex.Message); }
                        }
                        using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                        {
                            EnsureLayer(doc.Database, tr, LineLayer, 1);
                            EnsureLayer(doc.Database, tr, DotLayer, 2);
                            BlockTable blocks=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForWrite);
                            var definition=new BlockTableRecord { Name="WDT-UPSTAND-"+Guid.NewGuid().ToString("N") };
                            ObjectId definitionId=blocks.Add(definition);
                            tr.AddNewlyCreatedDBObject(definition,true);
                            Matrix3d local=Matrix3d.Displacement(new Vector3d(-geometry.BaseX,-geometry.BaseY,-geometry.Elevation));
                            List<Entity> generated=BuildUpstandRebars(layout,LineLayer,DotLayer);
                            try
                            {
                                foreach (Entity entity in generated) { entity.TransformBy(local); Append(definition, tr, entity); }
                                definition.DowngradeOpen();
                                if (annotations!=null)
                                {
                                    TryAppendAnnotation(doc.Database,definitionId,local,LabelLayer,71,"线筋标注",failures,
                                        optional => BuildUpstandLabel(doc.Database,annotations.LineLabel,annotations,"0",
                                            EnsureUpstandTextStyle(doc.Database,optional)));
                                    TryAppendAnnotation(doc.Database,definitionId,local,LabelLayer,71,"点筋标注",failures,
                                        optional => BuildUpstandLabel(doc.Database,annotations.DotLabel,annotations,"0",
                                            EnsureUpstandTextStyle(doc.Database,optional)));
                                    TryAppendAnnotation(doc.Database,definitionId,local,DimensionLayer,70,"宽度尺寸",failures,
                                        optional => BuildCommittedUpstandDimension(doc.Database,optional,
                                            annotations.WidthA,annotations.WidthB,annotations.WidthLine,annotations.Elevation));
                                    TryAppendAnnotation(doc.Database,definitionId,local,DimensionLayer,70,"高度尺寸",failures,
                                        optional => BuildCommittedUpstandDimension(doc.Database,optional,
                                            annotations.HeightA,annotations.HeightB,annotations.HeightLine,annotations.Elevation));
                                }
                                else failures.Add("全部自动标注（布局失败）");
                                BlockTableRecord space=(BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId,OpenMode.ForWrite);
                                var reference=new BlockReference(new Point3d(geometry.BaseX,geometry.BaseY,geometry.Elevation),definitionId)
                                    { Rotation=0, Layer=((LayerTableRecord)tr.GetObject(doc.Database.Clayer,OpenMode.ForRead)).Name };
                                Append(space,tr,reference);
                                WriteBlockParameters(tr,reference,values,p,geometry);
                                WriteLast(doc.Database, tr, values);
                                tr.Commit();
                            }
                            finally { foreach (Entity entity in generated) if (entity.ObjectId.IsNull) entity.Dispose(); }
                        }
                        }
                        finally { ed.Command("_.UNDO","_End"); }
                        ed.WriteMessage("\n已生成一个普通块，含有效钢筋及已成功的标注；锚固长度请审核实际构造。原混凝土轮廓未写入块。");
                        foreach (string warning in annotationWarnings) ed.WriteMessage("\n"+warning);
                        foreach (string failure in failures) ed.WriteMessage("\n未完成："+failure);
                        return;
                }
            }
            catch (System.Exception ex) { ed.WriteMessage("\n未生成钢筋：" + ex.Message); }
        }

        private static UpstandRebarParameters RebarParameters(DetailParams v)
        {
            return new UpstandRebarParameters { Cover=v.Cover,LineDiameter=v.LineDiameter,
                LineSpacing=v.LineSpacing,DotDiameter=v.DotDiameter,DotSpacing=v.DotSpacing,
                LeftAnchor=v.LeftAnchor,RightAnchor=v.RightAnchor,Radius=v.Radius,
                LineDisplayWidth=v.LineDisplayWidth,
                DotDisplayDiameter=v.DotDisplayDiameter };
        }

        private static List<Entity> BuildUpstandRebars(UpstandRebarLayout layout,string lineLayer,string dotLayer)
        {
            var entities=new List<Entity>();
            try
            {
                var line=new Polyline(layout.Line.Count) { Elevation=layout.Elevation,Layer=lineLayer };
                entities.Add(line);
                for (int i=0;i<layout.Line.Count;i++)
                {
                    RebarVertex v=layout.Line[i];
                    line.AddVertexAt(i,new Point2d(v.X,v.Y),v.Bulge,layout.LineWidth,layout.LineWidth);
                }
                foreach (ContourPoint p in layout.LeftDots) entities.Add(UpstandDot(p,layout.Elevation,layout.DotDiameter,dotLayer));
                foreach (ContourPoint p in layout.RightDots) entities.Add(UpstandDot(p,layout.Elevation,layout.DotDiameter,dotLayer));
                foreach (ContourPoint p in layout.TopDots) entities.Add(UpstandDot(p,layout.Elevation,layout.DotDiameter,dotLayer));
                return entities;
            }
            catch { foreach (Entity e in entities) e.Dispose(); throw; }
        }

        private static Polyline UpstandDot(ContourPoint center,double z,double diameter,string layer)
        {
            double pathRadius=diameter/4, width=diameter/2;
            var dot=new Polyline(2) { Closed=true,Elevation=z,Layer=layer };
            try
            {
                dot.AddVertexAt(0,new Point2d(center.X+pathRadius,center.Y),1,width,width);
                dot.AddVertexAt(1,new Point2d(center.X-pathRadius,center.Y),1,width,width);
                return dot;
            }
            catch { dot.Dispose(); throw; }
        }

        private static ObjectId UpstandTextStyle(Database db)
        {
            using (Transaction tr=db.TransactionManager.StartTransaction())
            {
                TextStyleTable styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForRead);
                return styles.Has(AnnotationTextStyle) ? styles[AnnotationTextStyle] : ObjectId.Null;
            }
        }

        private static ObjectId EnsureUpstandTextStyle(Database db,Transaction tr)
        {
            TextStyleTable styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForRead);
            if (styles.Has(AnnotationTextStyle)) return styles[AnnotationTextStyle];
            var style=new TextStyleTableRecord
            {
                Name=AnnotationTextStyle,
                FileName="Tssdeng.shx",
                BigFontFileName="Tssdchn.shx",
                TextSize=0,
                XScale=0.75,
                ObliquingAngle=0,
                IsVertical=false,
                IsShapeFile=false
            };
            try
            {
                styles.UpgradeOpen();
                ObjectId id=styles.Add(style);
                tr.AddNewlyCreatedDBObject(style,true);
                return id;
            }
            catch { style.Dispose(); throw; }
        }

        private static ObjectId UpstandDimensionStyle(Database db)
        {
            using (Transaction tr=db.TransactionManager.StartTransaction())
            {
                DimStyleTable styles=(DimStyleTable)tr.GetObject(db.DimStyleTableId,OpenMode.ForRead);
                return styles.Has(AnnotationDimensionStyle) ? styles[AnnotationDimensionStyle] : ObjectId.Null;
            }
        }

        private static ObjectId EnsureUpstandDimensionStyle(Database db,Transaction tr,ObjectId textStyleId)
        {
            DimStyleTable styles=(DimStyleTable)tr.GetObject(db.DimStyleTableId,OpenMode.ForRead);
            if (styles.Has(AnnotationDimensionStyle)) return styles[AnnotationDimensionStyle];
            ObjectId architecturalTick=ArchitecturalTickId(db,tr);
            var style=new DimStyleTableRecord();
            try
            {
                style.CopyFrom(tr.GetObject(db.Dimstyle,OpenMode.ForRead));
                style.Name=AnnotationDimensionStyle;
                style.Dimscale=AnnotationDimensionScale;
                style.Dimtxt=AnnotationDimensionTextHeight;
                style.Dimtxsty=textStyleId;
                style.Dimclrt=Color.FromColorIndex(ColorMethod.ByAci,7);
                style.Dimclrd=Color.FromColorIndex(ColorMethod.ByBlock,0);
                style.Dimclre=Color.FromColorIndex(ColorMethod.ByBlock,0);
                style.Dimalt=false;
                style.Dimtol=false;
                style.Dimlim=false;
                style.Dimlfac=1;
                style.Dimdec=0;
                style.Dimlunit=2;
                style.Dimrnd=0;
                style.Dimtsz=0;
                style.Dimasz=1;
                style.Dimsah=false;
                style.Dimblk=architecturalTick;
                style.Dimcen=0;
                style.Dimexe=1;
                style.Dimexo=2.5;
                style.Dimgap=1;
                style.Dimtad=1;
                style.Dimjust=0;
                style.Dimtih=false;
                style.Dimtoh=false;
                style.Dimtfill=0;
                style.Dimtmove=1;
                style.Dimtix=true;
                style.Dimtofl=true;
                style.Annotative=AnnotativeStates.False;
                styles.UpgradeOpen();
                ObjectId id=styles.Add(style);
                tr.AddNewlyCreatedDBObject(style,true);
                return id;
            }
            catch { style.Dispose(); throw; }
        }

        private static void PrepareArchitecturalTick(Document doc)
        {
            using (Transaction check=doc.Database.TransactionManager.StartTransaction())
            {
                BlockTable existing=(BlockTable)check.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);
                if (existing.Has("_ARCHTICK")) return;
            }

            // SETVAR uses AutoCAD's built-in arrow creation path. Run it before opening
            // the generation transaction, then restore the user's drawing override.
            string previous=Convert.ToString(CadApplication.GetSystemVariable("DIMBLK"));
            try { doc.Editor.Command("_.SETVAR","DIMBLK","_ARCHTICK"); }
            finally { doc.Editor.Command("_.SETVAR","DIMBLK",String.IsNullOrEmpty(previous) ? "." : previous); }

            using (Transaction check=doc.Database.TransactionManager.StartTransaction())
            {
                BlockTable created=(BlockTable)check.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);
                if (!created.Has("_ARCHTICK"))
                    throw new InvalidOperationException("AutoCAD 未能建立内置建筑标记箭头 _ARCHTICK。");
            }
        }

        private static ObjectId ArchitecturalTickId(Database db,Transaction tr)
        {
            const string arrowName="_ARCHTICK";
            BlockTable blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
            if (blocks.Has(arrowName)) return blocks[arrowName];
            throw new InvalidOperationException("当前图纸尚未建立建筑标记箭头 _ARCHTICK。");
        }

        private static List<Entity> BuildUpstandLabel(Database db,UpstandLabelLayout label,
            UpstandAnnotationLayout layout,string layer,ObjectId textStyleId)
        {
            var entities=new List<Entity>();
            try
            {
                var text=new DBText();
                entities.Add(text);
                text.SetDatabaseDefaults(db);
                text.Layer=layer; text.ColorIndex=256;
                text.TextString=label.Text.Replace("φ","%%132");
                text.TextStyleId=textStyleId;
                text.Height=layout.TextHeight;
                Point3d anchor=new Point3d(label.TextAnchor.X,label.TextAnchor.Y,layout.Elevation);
                text.HorizontalMode=label.Rightward ? TextHorizontalMode.TextLeft : TextHorizontalMode.TextRight;
                text.Position=anchor;
                if (!label.Rightward)
                {
                    text.AlignmentPoint=anchor;
                    text.AdjustAlignment(db);
                }
                // Use the actual TEXT extents. If an unappended preview has no extents,
                // stop at the anchor rather than let the underline run past the text.
                double endX=anchor.X;
                try
                {
                    Extents3d bounds=text.GeometricExtents;
                    double edge=label.Rightward ? bounds.MaxPoint.X : bounds.MinPoint.X;
                    if (!Double.IsNaN(edge) && !Double.IsInfinity(edge) &&
                        (label.Rightward ? edge>=anchor.X : edge<=anchor.X)) endX=edge;
                }
                catch (Autodesk.AutoCAD.Runtime.Exception) { /* Nonresident TEXT may have no extents yet. */ }
                for (int i=0;i<label.Leaders.Count;i++)
                {
                    AnnotationSegment segment=label.Leaders[i];
                    AnnotationPoint end=i==label.Leaders.Count-1 && !label.KeepLastLeaderEnd ?
                        new AnnotationPoint(endX,segment.B.Y) : segment.B;
                    if (Math.Abs(end.X-segment.A.X)<1e-8 && Math.Abs(end.Y-segment.A.Y)<1e-8) continue;
                    entities.Add(new Line(new Point3d(segment.A.X,segment.A.Y,layout.Elevation),
                        new Point3d(end.X,end.Y,layout.Elevation)) { Layer=layer,ColorIndex=256 });
                }
                if (label.HasSlash)
                {
                    var slash=new Polyline(2) { Layer=layer,ColorIndex=256,Elevation=layout.Elevation };
                    entities.Add(slash);
                    slash.AddVertexAt(0,new Point2d(label.SlashA.X,label.SlashA.Y),0,
                        label.SlashWidth,label.SlashWidth);
                    slash.AddVertexAt(1,new Point2d(label.SlashB.X,label.SlashB.Y),0,
                        label.SlashWidth,label.SlashWidth);
                }
                return entities;
            }
            catch { foreach (Entity entity in entities) entity.Dispose(); throw; }
        }

        private static RotatedDimension UpstandDimension(Database db,AnnotationPoint a,AnnotationPoint b,
            AnnotationPoint line,double z,string layer,ObjectId textStyleId,
            ObjectId dimStyleId)
        {
            if (dimStyleId.IsNull)
                throw new InvalidOperationException("当前图纸缺少 TSSD_25 尺寸样式。");
            double angle=Math.Abs(a.Y-b.Y)>Math.Abs(a.X-b.X) ? Math.PI/2 : 0;
            var dimension=new RotatedDimension(angle,new Point3d(a.X,a.Y,z),
                new Point3d(b.X,b.Y,z),new Point3d(line.X,line.Y,z),"",dimStyleId);
            dimension.SetDatabaseDefaults(db);
            dimension.Layer=layer;
            dimension.ColorIndex=256;
            dimension.TextStyleId=textStyleId;
            dimension.Dimtxt=AnnotationDimensionTextHeight;
            dimension.Dimscale=AnnotationDimensionScale;
            Color byLayer=Color.FromColorIndex(ColorMethod.ByLayer,256);
            dimension.Dimclrd=byLayer;
            dimension.Dimclre=byLayer;
            dimension.Dimclrt=Color.FromColorIndex(ColorMethod.ByAci,7);
            dimension.Dimalt=false;
            dimension.Dimtol=false;
            dimension.Dimlim=false;
            dimension.Dimlunit=2;
            dimension.Dimdec=0;
            dimension.Dimlfac=1;
            dimension.Dimrnd=0;
            dimension.Dimtad=1;
            dimension.Dimgap=1;
            return dimension;
        }

        private static List<Entity> BuildCommittedUpstandDimension(Database db,Transaction tr,
            AnnotationPoint a,AnnotationPoint b,AnnotationPoint line,double elevation)
        {
            ObjectId textStyleId=EnsureUpstandTextStyle(db,tr);
            ObjectId dimStyleId=EnsureUpstandDimensionStyle(db,tr,textStyleId);
            return new List<Entity> { UpstandDimension(db,a,b,line,elevation,"0",textStyleId,dimStyleId) };
        }

        private static void TryAppendAnnotation(Database db,ObjectId definitionId,Matrix3d local,string layer,short color,string name,
            List<string> failures,Func<Transaction,List<Entity>> build)
        {
            List<Entity> entities=null;
            try
            {
                // Nested transaction rolls back an incomplete optional group while retaining steel
                // in the parent transaction. The command's final commit is still one UNDO step.
                using (Transaction optional=db.TransactionManager.StartTransaction())
                {
                    EnsureLayer(db,optional,layer,color);
                    BlockTableRecord space=(BlockTableRecord)optional.GetObject(definitionId,OpenMode.ForWrite);
                    entities=build(optional);
                    foreach (Entity entity in entities)
                    {
                        entity.Layer=layer;
                        entity.TransformBy(local);
                        Append(space,optional,entity);
                    }
                    optional.Commit();
                }
            }
            catch (System.Exception ex) { failures.Add(name+"："+ex.Message); }
            finally
            {
                if (entities!=null) foreach (Entity entity in entities)
                    if (entity.ObjectId.IsNull) entity.Dispose();
            }
        }

        private static Rect PickRect(Document doc, string name)
        {
            var options = new PromptEntityOptions("\n选择" + name + "（闭合矩形多段线）: ");
            options.SetRejectMessage("\n请选择闭合矩形二维多段线。由四条直线组成的矩形请先用 PEDIT/JOIN 转成闭合多段线。");
            options.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult result = doc.Editor.GetEntity(options);
            if (result.Status != PromptStatus.OK) return null;
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                Rect rect = ReadRect(tr.GetObject(result.ObjectId, OpenMode.ForRead) as Polyline);
                Check(rect != null, name + "必须是轴对齐的闭合矩形二维多段线，可先用原生 PEDIT/JOIN 修正。");
                return rect;
            }
        }

        private static Rect ReadRect(Polyline p)
        {
            if (p == null || !p.Closed || p.NumberOfVertices != 4 ||
                Math.Abs(p.Normal.X) > Epsilon || Math.Abs(p.Normal.Y) > Epsilon || p.Normal.Z < 1-Epsilon) return null;
            var r = new Rect { L = double.PositiveInfinity, B = double.PositiveInfinity,
                R = double.NegativeInfinity, T = double.NegativeInfinity, Z = p.Elevation };
            for (int i = 0; i < 4; i++)
            {
                if (!Near(p.GetBulgeAt(i), 0)) return null;
                Point3d a = p.GetPoint3dAt(i), b = p.GetPoint3dAt((i+1)%4);
                if (!Near(a.Z,r.Z) || !Near(a.Z,b.Z) || (Near(a.X,b.X) == Near(a.Y,b.Y))) return null;
                r.L = Math.Min(r.L,a.X); r.R = Math.Max(r.R,a.X);
                r.B = Math.Min(r.B,a.Y); r.T = Math.Max(r.T,a.Y);
            }
            if (r.W <= Epsilon || r.H <= Epsilon) return null;
            for (int i = 0; i < 4; i++)
            {
                Point3d v = p.GetPoint3dAt(i);
                if (!(Near(v.X,r.L) || Near(v.X,r.R)) || !(Near(v.Y,r.B) || Near(v.Y,r.T))) return null;
            }
            return r;
        }

        private static void AddUnique(List<Rect> result, Rect value)
        {
            foreach (Rect r in result) if (r.Same(value)) return;
            result.Add(value);
        }

        private static void CollectLineRectangles(List<Line> lines, List<Rect> result)
        {
            // Only complete four-edge loops qualify; independent architectural lines remain untouched.
            foreach (Line bottom in lines) foreach (Line top in lines)
            {
                if (bottom == top || !Near(bottom.StartPoint.Y,bottom.EndPoint.Y) ||
                    !Near(top.StartPoint.Y,top.EndPoint.Y) || !Near(bottom.StartPoint.Z,top.StartPoint.Z)) continue;
                double b = bottom.StartPoint.Y, t = top.StartPoint.Y;
                if (t <= b + Epsilon) continue;
                double l = Math.Min(bottom.StartPoint.X,bottom.EndPoint.X), r = Math.Max(bottom.StartPoint.X,bottom.EndPoint.X);
                if (r-l <= Epsilon || !Near(l,Math.Min(top.StartPoint.X,top.EndPoint.X)) ||
                    !Near(r,Math.Max(top.StartPoint.X,top.EndPoint.X))) continue;
                bool left = false, right = false;
                foreach (Line side in lines)
                {
                    if (!Near(side.StartPoint.X,side.EndPoint.X) || !Near(side.StartPoint.Z,bottom.StartPoint.Z)) continue;
                    double low = Math.Min(side.StartPoint.Y,side.EndPoint.Y), high = Math.Max(side.StartPoint.Y,side.EndPoint.Y);
                    if (!Near(low,b) || !Near(high,t)) continue;
                    if (Near(side.StartPoint.X,l)) left = true;
                    if (Near(side.StartPoint.X,r)) right = true;
                }
                if (left && right) AddUnique(result,new Rect { L=l,R=r,B=b,T=t,Z=bottom.StartPoint.Z });
            }
        }

        private static bool TryClassify(List<Rect> all, out Rect beam, out Rect slab, out Rect upstand)
        {
            beam = slab = upstand = null;
            int count = 0;
            foreach (Rect b in all) foreach (Rect s in all) foreach (Rect u in all)
            {
                if (b == s || b == u || s == u) continue;
                if (!Relations(b,s,u)) continue;
                beam=b; slab=s; upstand=u; count++;
            }
            return count == 1;
        }

        private static bool Relations(Rect b, Rect s, Rect u)
        {
            double align = Math.Max(1e-3, Math.Min(Math.Min(b.H,u.H), Math.Max(s.H,b.H)) * .05);
            double side = Math.Max(1e-3, b.W * .02);
            bool samePlane = Math.Abs(b.Z-s.Z) <= align && Math.Abs(b.Z-u.Z) <= align;
            bool upstandAboveBeam = Math.Abs(u.B-b.T) <= align && u.T > b.T+align
                && u.L >= b.L-side && u.R <= b.R+side;
            // Slab position is not a precondition for placing steel in the chosen beam.
            return samePlane && s.W > s.H && upstandAboveBeam;
        }

        private static Polyline Rectangle(Rect r, string layer)
        {
            var p = new Polyline(4) { Closed=true, Elevation=r.Z, Layer=layer };
            p.AddVertexAt(0,new Point2d(r.L,r.B),0,0,0); p.AddVertexAt(1,new Point2d(r.R,r.B),0,0,0);
            p.AddVertexAt(2,new Point2d(r.R,r.T),0,0,0); p.AddVertexAt(3,new Point2d(r.L,r.T),0,0,0);
            return p;
        }

        private static bool Ask(Editor ed, string name, ref double value, bool zero=false)
        {
            var options = new PromptDoubleOptions("\n"+name+"（mm） <"+value.ToString("g")+">: ")
            { AllowNegative=false, AllowZero=zero, DefaultValue=value, UseDefaultValue=true };
            PromptDoubleResult result = ed.GetDouble(options);
            if (result.Status != PromptStatus.OK) return false;
            Check(Finite(result.Value) && (zero ? result.Value>=0 : result.Value>0), name+"无效。");
            value=result.Value; return true;
        }
        private static bool ReadParameters(Editor ed, DetailParams p)
        {
            ed.WriteMessage("\n默认实际直径/间距：线筋%%1328@150、点筋%%1326@200。锚固与 R 均为绘图输入值，不作规范计算。");
            if (!Ask(ed,"保护层",ref p.Cover,true) || !Ask(ed,"线筋实际直径",ref p.LineDiameter)
                || !Ask(ed,"线筋设计间距",ref p.LineSpacing) || !Ask(ed,"点筋实际直径",ref p.DotDiameter)
                || !Ask(ed,"点筋中心间距",ref p.DotSpacing)) return false;
            if (!AskAnchor(ed,"左侧伸入梁内长度（设计确认）",ref p.LeftAnchor)) return false;
            if (p.RightAnchor<=0) p.RightAnchor=p.LeftAnchor;
            if (!AskAnchor(ed,"右侧伸入梁内长度（默认与左侧相同）",ref p.RightAnchor)) return false;
            if (!Ask(ed,"中心线圆弧半径 R（绘图值，默认20）",ref p.Radius)) return false;
            return true;
        }

        private static bool AskAnchor(Editor ed, string name, ref double value)
        {
            if (value > 0) return Ask(ed,name,ref value);
            var options = new PromptDoubleOptions("\n"+name+"（mm；须明确输入）: ")
                { AllowNegative=false, AllowZero=false, UseDefaultValue=false };
            PromptDoubleResult result = ed.GetDouble(options);
            if (result.Status != PromptStatus.OK) return false;
            Check(Finite(result.Value) && result.Value>0, name+"无效。");
            value=result.Value; return true;
        }

        private static List<Entity> BuildDetail(Database db, Rect beam, Rect slab, Rect up, DetailParams p,
            double scale, bool preview)
        {
            var entities = new List<Entity>();
            try
            {
                // Previews must use an existing layer. Target layers are created only in the confirm transaction.
                string concreteLayer = preview ? "0" : ConcreteLayer;
                string lineLayer = preview ? "0" : LineLayer;
                string dotLayer = preview ? "0" : DotLayer;
                string labelLayer = preview ? "0" : LabelLayer;
                string dimensionLayer = preview ? "0" : DimensionLayer;
                double width = PluginSettings.ReadLineWidthMillimetres()*scale;
                double dot = PluginSettings.ReadDotDiameterMillimetres()*scale;
                double inset = p.Cover*scale + width/2; // This detail's approved display-based offset.
                double left=up.L+inset, right=up.R-inset, top=up.T-inset;
                double beamTop=beam.T;
                Check(left-width/2 >= beam.L-Epsilon && right+width/2 <= beam.R+Epsilon,
                    "U筋两侧未落入所选梁的宽度范围，请检查梁和反坎的选择。");
                Check(right-left > width+2*dot+Epsilon, "反坎过窄，U筋与两列圆点筋无法放下。");
                Check(top > beamTop+dot+width+50*scale, "反坎高度不足，最下排圆点筋无法满足距梁顶50～150 mm。");
                Check(p.LeftAnchor*scale <= beam.H+Epsilon && p.RightAnchor*scale <= beam.H+Epsilon,
                    "输入的直锚长度超过梁高；没有已审核的弯折规则，停止生成。");
                entities.Add(Rectangle(beam,concreteLayer)); entities.Add(Rectangle(slab,concreteLayer));
                entities.Add(Rectangle(up,concreteLayer));
                var u = new Polyline(4) { Elevation=beam.Z, Layer=lineLayer };
                u.AddVertexAt(0,new Point2d(left,beamTop-p.LeftAnchor*scale),0,width,width);
                u.AddVertexAt(1,new Point2d(left,top),0,width,width);
                u.AddVertexAt(2,new Point2d(right,top),0,width,width);
                u.AddVertexAt(3,new Point2d(right,beamTop-p.RightAnchor*scale),0,width,width);
                u.ConstantWidth=width; entities.Add(u);
                double yFirst=top-width/2-dot/2;
                double bottomMin=beamTop+50*scale+dot/2, bottomMax=beamTop+150*scale+dot/2;
                Check(yFirst >= bottomMin-Epsilon, "反坎高度不足，无法布置第一排圆点筋。");
                double yLast=Math.Min(yFirst,bottomMax);
                double maximum=p.DotSpacing*scale;
                int rows = yFirst-yLast <= Epsilon ? 1 : (int)Math.Ceiling((yFirst-yLast)/maximum)+1;
                Check(rows > 0 && rows <= 10000, "圆点筋排数超出可处理范围。");
                double actual=rows == 1 ? 0 : (yFirst-yLast)/(rows-1);
                double xLeft=left+width/2+dot/2, xRight=right-width/2-dot/2;
                Check(xRight-xLeft >= dot-Epsilon, "两列点筋相交，几何空间不足。");
                for (int i=0;i<rows;i++)
                {
                    double y=yFirst-i*actual;
                    entities.Add(Dot(xLeft,y,beam.Z,dot,dotLayer));
                    entities.Add(Dot(xRight,y,beam.Z,dot,dotLayer));
                }
                double labelHeight= Math.Max(2.5*scale, Math.Min(20*scale, up.W/16));
                entities.Add(Text(db,"%%132"+p.LineDiameter.ToString("g")+"@"+p.LineSpacing.ToString("g"),
                    new Point3d(up.R+20*scale,up.T,beam.Z),labelHeight,labelLayer));
                entities.Add(Text(db,"%%132"+p.DotDiameter.ToString("g")+"@"+p.DotSpacing.ToString("g"),
                    new Point3d(up.R+20*scale,up.T-3*labelHeight,beam.Z),labelHeight,labelLayer));
                entities.Add(Text(db,"L="+p.LeftAnchor.ToString("g")+" / R="+p.RightAnchor.ToString("g")+" mm (审核)",
                    new Point3d(up.R+20*scale,up.T-6*labelHeight,beam.Z),labelHeight,labelLayer));
                entities.Add(Dimension(db,new Point3d(up.L,up.T,beam.Z),new Point3d(up.R,up.T,beam.Z),
                    new Point3d((up.L+up.R)/2,up.T+30*scale,beam.Z),dimensionLayer));
                entities.Add(Dimension(db,new Point3d(up.L,beamTop,beam.Z),new Point3d(up.L,up.T,beam.Z),
                    new Point3d(up.L-30*scale,(beamTop+up.T)/2,beam.Z),dimensionLayer));
                entities.Add(Dimension(db,new Point3d(beam.L,beam.B,beam.Z),new Point3d(beam.R,beam.B,beam.Z),
                    new Point3d((beam.L+beam.R)/2,beam.B-30*scale,beam.Z),dimensionLayer));
                return entities;
            }
            catch { foreach (Entity e in entities) e.Dispose(); throw; }
        }

        private static Polyline Dot(double x,double y,double z,double diameter,string layer)
        {
            double r=diameter/2, bulge=Math.Tan(Math.PI/8);
            var p=new Polyline(4) { Closed=true, Elevation=z, Layer=layer };
            p.AddVertexAt(0,new Point2d(x+r,y),bulge,0,0);
            p.AddVertexAt(1,new Point2d(x,y+r),bulge,0,0);
            p.AddVertexAt(2,new Point2d(x-r,y),bulge,0,0);
            p.AddVertexAt(3,new Point2d(x,y-r),bulge,0,0);
            return p;
        }
        private static DBText Text(Database db,string value,Point3d position,double height,string layer)
        {
            var t=new DBText(); t.SetDatabaseDefaults(db);
            t.TextString=value; t.Position=position; t.Height=height; t.Layer=layer;
            return t;
        }
        private static RotatedDimension Dimension(Database db,Point3d a,Point3d b,Point3d line,string layer)
        {
            double angle=Math.Abs(a.Y-b.Y) > Math.Abs(a.X-b.X) ? Math.PI/2 : 0;
            var d=new RotatedDimension(angle,a,b,line,"",db.Dimstyle);
            d.SetDatabaseDefaults(db); d.Layer=layer; return d;
        }
        private static void Append(BlockTableRecord space,Transaction tr,Entity entity)
        {
            space.AppendEntity(entity); tr.AddNewlyCreatedDBObject(entity,true);
        }
        private static void EnsureLayer(Database db,Transaction tr,string name,short color,bool enforceColor=false)
        {
            LayerTable table=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
            if (table.Has(name))
            {
                if (enforceColor)
                {
                    LayerTableRecord existing=(LayerTableRecord)tr.GetObject(table[name],OpenMode.ForWrite);
                    if (existing.Color.ColorIndex!=color)
                        existing.Color=Color.FromColorIndex(ColorMethod.ByAci,color);
                }
                return;
            }
            table.UpgradeOpen();
            var layer=new LayerTableRecord { Name=name, Color=Color.FromColorIndex(ColorMethod.ByAci,color) };
            table.Add(layer); tr.AddNewlyCreatedDBObject(layer,true);
        }
        private static DetailParams FromPanel(UpstandPanelValues v)
        {
            return new DetailParams { Cover=v.Cover,LineDiameter=v.LineDiameter,LineSpacing=v.LineSpacing,
                DotDiameter=v.DotDiameter,DotSpacing=v.DotSpacing,LeftAnchor=v.LeftAnchor,
                RightAnchor=v.RightAnchor,Radius=v.Radius,TextHeight=v.TextHeight,
                LineDisplayWidth=v.LineDisplayWidth,DotDisplayDiameter=v.DotDisplayDiameter };
        }
        internal static UpstandPanelValues PanelValues(Database db)
        {
            DetailParams p=ReadLast(db);
            return new UpstandPanelValues { Cover=p.Cover,LineDiameter=p.LineDiameter,LineSpacing=p.LineSpacing,
                DotDiameter=p.DotDiameter,DotSpacing=p.DotSpacing,LeftAnchor=p.LeftAnchor,
                RightAnchor=p.RightAnchor,Radius=p.Radius,TextHeight=p.TextHeight,
                LineDisplayWidth=p.LineDisplayWidth,DotDisplayDiameter=p.DotDisplayDiameter };
        }
        private static void WriteBlockParameters(Transaction tr,BlockReference reference,DetailParams v,
            UpstandRebarParameters p,UpstandGeometry geometry)
        {
            reference.CreateExtensionDictionary();
            DBDictionary data=(DBDictionary)tr.GetObject(reference.ExtensionDictionary,OpenMode.ForWrite);
            var record=new Xrecord();
            data.SetAt(BlockParamRecord,record);
            tr.AddNewlyCreatedDBObject(record,true);
            record.Data=new ResultBuffer(
                new TypedValue((int)DxfCode.Int16,1),
                new TypedValue((int)DxfCode.Real,v.Cover),new TypedValue((int)DxfCode.Real,v.LineDiameter),
                new TypedValue((int)DxfCode.Real,v.LineSpacing),new TypedValue((int)DxfCode.Real,v.DotDiameter),
                new TypedValue((int)DxfCode.Real,v.DotSpacing),new TypedValue((int)DxfCode.Real,v.LeftAnchor),
                new TypedValue((int)DxfCode.Real,v.RightAnchor),new TypedValue((int)DxfCode.Real,v.Radius),
                new TypedValue((int)DxfCode.Real,p.LineDisplayWidth),new TypedValue((int)DxfCode.Real,p.DotDisplayDiameter),
                new TypedValue((int)DxfCode.Real,v.TextHeight),
                new TypedValue((int)DxfCode.Real,geometry.Left),new TypedValue((int)DxfCode.Real,geometry.Right),
                new TypedValue((int)DxfCode.Real,geometry.Top),new TypedValue((int)DxfCode.Real,geometry.BeamTop),
                new TypedValue((int)DxfCode.Int16,(short)geometry.InputKind));
        }
        private static DetailParams ReadLast(Database db)
        {
            var p=new DetailParams();
            using (Transaction tr=db.TransactionManager.StartTransaction())
            {
                DBDictionary nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForRead);
                if (!nod.Contains(ParamRecord))
                {
                    UpstandPanelValues defaults=UpstandPanelValues.ReadUserDefaults();
                    return FromPanel(defaults);
                }
                Xrecord record=tr.GetObject(nod.GetAt(ParamRecord),OpenMode.ForRead) as Xrecord;
                if (record == null || record.Data == null) return p;
                TypedValue[] v=record.Data.AsArray();
                if (v.Length != 7 && v.Length != 8 && v.Length != 9 && v.Length != 11) return p;
                try
                {
                    p.Cover=Convert.ToDouble(v[0].Value); p.LineDiameter=Convert.ToDouble(v[1].Value);
                    p.LineSpacing=Convert.ToDouble(v[2].Value); p.DotDiameter=Convert.ToDouble(v[3].Value);
                    p.DotSpacing=Convert.ToDouble(v[4].Value); p.LeftAnchor=Convert.ToDouble(v[5].Value);
                    p.RightAnchor=Convert.ToDouble(v[6].Value);
                    if (v.Length>=8) p.Radius=Convert.ToDouble(v[7].Value);
                    if (v.Length>=9) p.TextHeight=Convert.ToDouble(v[8].Value);
                    if (v.Length==11) { p.LineDisplayWidth=Convert.ToDouble(v[9].Value);
                        p.DotDisplayDiameter=Convert.ToDouble(v[10].Value); }
                }
                catch { return new DetailParams(); }
                return p;
            }
        }
        private static void WriteLast(Database db,Transaction tr,DetailParams p)
        {
            DBDictionary nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForWrite);
            Xrecord record;
            if (nod.Contains(ParamRecord)) record=(Xrecord)tr.GetObject(nod.GetAt(ParamRecord),OpenMode.ForWrite);
            else { record=new Xrecord(); nod.SetAt(ParamRecord,record); tr.AddNewlyCreatedDBObject(record,true); }
            record.Data=new ResultBuffer(new TypedValue((int)DxfCode.Real,p.Cover),new TypedValue((int)DxfCode.Real,p.LineDiameter),
                new TypedValue((int)DxfCode.Real,p.LineSpacing),new TypedValue((int)DxfCode.Real,p.DotDiameter),
                new TypedValue((int)DxfCode.Real,p.DotSpacing),new TypedValue((int)DxfCode.Real,p.LeftAnchor),
                new TypedValue((int)DxfCode.Real,p.RightAnchor),new TypedValue((int)DxfCode.Real,p.Radius),
                new TypedValue((int)DxfCode.Real,p.TextHeight),
                new TypedValue((int)DxfCode.Real,p.LineDisplayWidth),
                new TypedValue((int)DxfCode.Real,p.DotDisplayDiameter));
        }
    }
}
