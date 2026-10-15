using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using TransientManager = Autodesk.AutoCAD.GraphicsInterface.TransientManager;
using TransientDrawingMode = Autodesk.AutoCAD.GraphicsInterface.TransientDrawingMode;

namespace WallDetailTool
{
    internal static partial class RebarPalette
    {
        private sealed class EditState
        {
            internal Document Document;
            internal Guid RebarId;
            internal ObjectId EntityId;
            internal ObjectId SpaceId;
            internal ObjectId SourceId;
            internal UnitsValue Units;
            internal Polyline Original;
            internal Polyline SourceSnapshot;
            internal Polyline Result;
            internal double Cover;
            internal double Diameter;
            internal double Side;
            internal string SourceHash;
            internal string Layer;
            internal bool ReplacePath;
            internal bool ManualPath;
            internal bool ManualWidth;
            internal string PreviousLayer;
            internal double PreviousCover;
            internal double PreviousDiameter;
            internal string PreviousGeometryHash;
            internal ObjectId PreviousSourceId;
            internal double PreviousSide;
            internal string PreviousMode;
            internal bool SourceChanged;
        }
        private static EditState editPreview;

        private static void ClearEditPreview()
        {
            EditState old = editPreview;
            editPreview = null;
            if (old != null)
            {
                try { TransientManager.CurrentTransientManager.EraseTransient(old.Result, viewports); }
                catch (Autodesk.AutoCAD.Runtime.Exception) { /* The document may be closing. */ }
                finally
                {
                    old.Result.Dispose();
                    old.Original.Dispose();
                    if (old.SourceSnapshot != null) old.SourceSnapshot.Dispose();
                }
            }
            if (panel != null) panel.SetEditPreviewAvailable(false);
        }

        internal static void PreviewEdit()
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null || panel == null || palette == null || !palette.Visible) return;
            ClearPreview();
            Editor editor = doc.Editor;
            try
            {
                Guid rebarId = panel.SelectedRebarId;
                if (rebarId == Guid.Empty) throw new InvalidOperationException("请在列表中选中一条钢筋。");
                double cover, diameter;
                panel.ReadParameters(out cover, out diameter);
                string layer = LayerTools.CheckName(panel.LayerName);
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    DrawingData.Rebar data = DrawingData.Read(doc.Database, tr).Rebars.Find(r => r.Id == rebarId);
                    if (data == null || data.EntityId.IsNull || data.EntityId.IsErased)
                        throw new InvalidOperationException("钢筋已删除或失联，不能修改。");
                    Polyline steel = tr.GetObject(data.EntityId, OpenMode.ForRead) as Polyline;
                    if (steel == null) throw new InvalidOperationException("关联对象已不是二维多段线。");
                    bool geometryChange = cover != data.Cover || diameter != data.Diameter;
                    bool layerChange = steel.Layer != layer;
                    if (!geometryChange && !layerChange) throw new InvalidOperationException("尺寸和图层没有变化。");
                    double scale = Commands.MillimetresToDrawingUnits(doc.Database);
                    bool manualPath = data.Mode == "Free" || (!string.IsNullOrEmpty(data.GeometryHash) &&
                        data.GeometryHash != DrawingData.GeometryHash(steel));
                    bool manualWidth = WidthChanged(steel, data.Diameter * scale);
                    Polyline result = null, original = null, sourceSnapshot = null;
                    ObjectId sourceId = data.SourceId;
                    double side = data.Side;
                    string sourceHash = data.SourceHash;
                    bool sourceChanged = false;
                    bool displayed = false;
                    try
                    {
                        original = (Polyline)steel.Clone();
                        if (geometryChange)
                        {
                            if (sourceId.IsNull)
                            {
                                var options = new PromptEntityOptions("\n旧钢筋缺少源轮廓，请重新选择原混凝土轮廓: ");
                                options.AddAllowedClass(typeof(Polyline), true);
                                options.AddAllowedClass(typeof(Polyline2d), true);
                                PromptEntityResult chosen = editor.GetEntity(options);
                                if (chosen.Status != PromptStatus.OK) throw new OperationCanceledException();
                                if (chosen.ObjectId == data.EntityId) throw new InvalidOperationException("源轮廓不能是钢筋自身。");
                                sourceId = chosen.ObjectId;
                            }
                            if (sourceId.IsErased) throw new InvalidOperationException("源轮廓已删除，不能重新偏移。");
                            Entity sourceEntity = tr.GetObject(sourceId, OpenMode.ForRead) as Entity;
                            if (sourceEntity == null) throw new InvalidOperationException("源轮廓已失联。");
                            using (Polyline source = Commands.CopyContour(sourceEntity))
                            {
                                Commands.ValidatePolyline(source);
                                sourceSnapshot = (Polyline)source.Clone();
                                sourceHash = DrawingData.GeometryHash(source);
                                sourceChanged = !string.IsNullOrEmpty(data.SourceHash) && data.SourceHash != sourceHash;
                                double distance = (cover + diameter / 2.0) * scale;
                                if (!Commands.Finite(distance) || distance <= 1e-6)
                                    throw new InvalidOperationException("偏移距离无效或过小。");
                                if (side == 0)
                                {
                                    while (true)
                                    {
                                        PromptPointResult point = editor.GetPoint("\n在线段中部旁点选钢筋所在的一侧: ");
                                        if (point.Status != PromptStatus.OK) throw new OperationCanceledException();
                                        double signed;
                                        if (!Commands.TryDirection(source, point.Value, distance, out signed))
                                        { editor.WriteMessage("\n方向不明确，请避开端点与拐角。 "); continue; }
                                        side = Math.Sign(signed);
                                        break;
                                    }
                                }
                                result = Commands.CreateRebar(source, side * distance, distance, diameter * scale, doc.Database);
                            }
                        }
                        else result = (Polyline)steel.Clone();
                        result.ColorIndex = 1;
                        if (!TransientManager.CurrentTransientManager.AddTransient(result, TransientDrawingMode.DirectShortTerm, 128, viewports))
                            throw new InvalidOperationException("AutoCAD 未能显示临时预览。");
                        displayed = true;
                        editPreview = new EditState
                        {
                            Document = doc, RebarId = rebarId, EntityId = data.EntityId, SpaceId = steel.OwnerId,
                            SourceId = sourceId, Units = doc.Database.Insunits, Original = original,
                            SourceSnapshot = sourceSnapshot, Result = result, Cover = cover, Diameter = diameter,
                            Side = side, SourceHash = sourceHash, Layer = layer, ReplacePath = geometryChange,
                            ManualPath = manualPath, ManualWidth = manualWidth, PreviousLayer = steel.Layer,
                            PreviousCover = data.Cover, PreviousDiameter = data.Diameter,
                            PreviousGeometryHash = data.GeometryHash, PreviousSourceId = data.SourceId,
                            PreviousSide = data.Side, PreviousMode = data.Mode, SourceChanged = sourceChanged
                        };
                        panel.SetEditPreviewAvailable(true);
                        panel.ShowStatus((manualPath && geometryChange ? "注意：确认后将替换 CAD 中手工调整的路径。" :
                            sourceChanged ? "源轮廓已改动；请核对红色预览后再确认。" :
                            manualWidth && !geometryChange ? "CAD 中改过宽度；只改图层会保留该宽度。" :
                            "红色为修改预览。") + " 目标图层：" + layer + "；确认前不会修改图纸。");
                    }
                    catch
                    {
                        if (editPreview == null)
                        {
                            if (displayed) TransientManager.CurrentTransientManager.EraseTransient(result, viewports);
                            if (result != null) result.Dispose();
                            if (original != null) original.Dispose();
                            if (sourceSnapshot != null) sourceSnapshot.Dispose();
                        }
                        throw;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ClearPreview();
                panel.ShowStatus("已取消修改预览，原钢筋不变。");
            }
            catch (System.Exception ex)
            {
                ClearPreview();
                panel.ShowStatus("修改预览失败：" + ex.Message);
                editor.WriteMessage("\n修改预览失败：{0}", ex.Message);
            }
        }

        internal static bool WidthChanged(Polyline steel, double expected)
        {
            if (Math.Abs(steel.ConstantWidth - expected) > Math.Max(1e-6, expected * 1e-8)) return true;
            for (int i = 0; i < steel.NumberOfVertices - 1; i++)
                if (Math.Abs(steel.GetStartWidthAt(i) - expected) > 1e-6 ||
                    Math.Abs(steel.GetEndWidthAt(i) - expected) > 1e-6) return true;
            return false;
        }

        internal static void ConfirmEdit()
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null || panel == null || editPreview == null) return;
            EditState state = editPreview;
            try
            {
                double cover, diameter;
                panel.ReadParameters(out cover, out diameter);
                if (!ReferenceEquals(doc, state.Document) || panel.SelectedRebarId != state.RebarId ||
                    cover != state.Cover || diameter != state.Diameter || panel.LayerName != state.Layer ||
                    doc.Database.Insunits != state.Units)
                    throw new InvalidOperationException("图纸、选择、参数或单位已变化，请重新预览。");
                if (!LayerTools.ConfirmNew(doc.Database, doc.Editor, state.Layer))
                {
                    ClearPreview();
                    panel.ShowStatus("未创建图层；修改预览已取消，原钢筋不变。");
                    return;
                }
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    DrawingData.Rebar data = DrawingData.Read(doc.Database, tr).Rebars.Find(r => r.Id == state.RebarId);
                    if (data == null || data.EntityId != state.EntityId || data.Cover != state.PreviousCover ||
                        data.Diameter != state.PreviousDiameter || data.GeometryHash != state.PreviousGeometryHash ||
                        data.SourceId != state.PreviousSourceId || data.Side != state.PreviousSide ||
                        data.Mode != state.PreviousMode)
                        throw new InvalidOperationException("钢筋关联数据已变化，请重新预览。");
                    if (data.EntityId.IsErased) throw new InvalidOperationException("原钢筋已删除，请重新选择。");
                    Polyline current = tr.GetObject(data.EntityId, OpenMode.ForRead) as Polyline;
                    if (current == null || !SameContour(state.Original, current) || current.Layer != state.PreviousLayer ||
                        current.OwnerId != state.SpaceId)
                        throw new InvalidOperationException("原钢筋已变化，请重新预览。");
                    if (state.SourceSnapshot != null)
                    {
                        if (state.SourceId.IsErased) throw new InvalidOperationException("源轮廓已删除，请重新预览。");
                        Entity sourceEntity = tr.GetObject(state.SourceId, OpenMode.ForRead) as Entity;
                        if (sourceEntity == null) throw new InvalidOperationException("源轮廓已失联。");
                        using (Polyline source = Commands.CopyContour(sourceEntity))
                            if (!SameContour(state.SourceSnapshot, source))
                                throw new InvalidOperationException("源轮廓已变化，请重新预览。");
                    }
                    RebarEditService.Apply(doc.Database, tr, data, current, state.Result,
                        state.SourceId, state.Side, state.SourceHash, state.Cover, state.Diameter,
                        state.Layer, state.ReplacePath, state.ManualPath);
                    tr.Commit();
                }
                ClearPreview();
                panel.RefreshLayers();
                panel.RefreshTree(state.RebarId);
                panel.ShowStatus("修改已确认。可用 AutoCAD 撤销还原；原轮廓保持不变。");
            }
            catch (System.Exception ex)
            {
                ClearPreview();
                panel.ShowStatus("未修改：" + ex.Message);
                doc.Editor.WriteMessage("\n未修改钢筋：{0}", ex.Message);
            }
        }
    }
}
