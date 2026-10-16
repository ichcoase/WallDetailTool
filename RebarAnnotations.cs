using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using TransientManager = Autodesk.AutoCAD.GraphicsInterface.TransientManager;
using TransientDrawingMode = Autodesk.AutoCAD.GraphicsInterface.TransientDrawingMode;

namespace WallDetailTool
{
    internal static partial class RebarPalette
    {
        private const string AnnotationTextLayer = "S-TEXT";
        private const string AnnotationLeaderLayer = "s-floor_de_dim";
        private const string AnnotationTextStyle = "TSSD";
        private const double AnnotationHeightMm = 62.5;
        private sealed class AnnotationPreview
        {
            internal Document Document;
            internal Guid RebarId;
            internal ObjectId RebarEntityId;
            internal ObjectId SpaceId;
            internal UnitsValue Units;
            internal Guid AnnotationId;
            internal ObjectId TextId;
            internal ObjectId LeaderId;
            internal string OriginalText;
            internal Point3d OriginalPosition;
            internal DBText Text;
            internal Line Leader;
            internal string Label;
        }
        private static AnnotationPreview annotationPreview;

        private static string CadLabel(string value)
        {
            return value.Replace("Φ", "%%c").Replace("φ", "%%c");
        }
        internal static string DisplayLabel(string value)
        {
            return (value ?? "").Replace("%%c", "Φ").Replace("%%C", "Φ");
        }
        private static ObjectId TextStyle(Database db, Transaction tr)
        {
            TextStyleTable styles = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (!styles.Has(AnnotationTextStyle))
                throw new InvalidOperationException("当前图纸缺少 TSSD 文字样式，请使用含有该样式的图纸模板。");
            return styles[AnnotationTextStyle];
        }
        private static void ClearAnnotationPreview()
        {
            AnnotationPreview old = annotationPreview;
            annotationPreview = null;
            if (old != null)
            {
                try
                {
                    TransientManager.CurrentTransientManager.EraseTransient(old.Text, viewports);
                    if (old.Leader != null)
                        TransientManager.CurrentTransientManager.EraseTransient(old.Leader, viewports);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception) { /* Closing drawing. */ }
                finally
                {
                    old.Text.Dispose();
                    if (old.Leader != null) old.Leader.Dispose();
                }
            }
            if (panel != null) panel.SetAnnotationPreviewAvailable(false);
        }

        internal static void PreviewAnnotation()
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null || panel == null || palette == null || !palette.Visible) return;
            ClearPreview();
            Editor editor = doc.Editor;
            try
            {
                Guid rebarId = panel.SelectedRebarId;
                if (rebarId == Guid.Empty) throw new InvalidOperationException("请在列表中选中一条钢筋。");
                string label = panel.AnnotationLabel;
                if (string.IsNullOrWhiteSpace(label) || label != label.Trim() || label.Length > 120 ||
                    label.IndexOf('\n') >= 0 || label.IndexOf('\r') >= 0)
                    throw new InvalidOperationException("标注文字须为1至120个字符，不能换行或留首尾空格。");
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    DrawingData.Index data = DrawingData.Read(doc.Database, tr);
                    DrawingData.Rebar rebar = data.Rebars.Find(r => r.Id == rebarId);
                    if (rebar == null || rebar.EntityId.IsNull || rebar.EntityId.IsErased)
                        throw new InvalidOperationException("钢筋已删除或失联。");
                    Polyline steel = tr.GetObject(rebar.EntityId, OpenMode.ForRead) as Polyline;
                    if (steel == null) throw new InvalidOperationException("关联对象不是二维钢筋多段线。");
                    DrawingData.Annotation existing = data.Annotations.Find(a => a.RebarId == rebarId);
                    ObjectId styleId = TextStyle(doc.Database, tr);
                    DBText text = null;
                    Line leader = null;
                    bool textShown = false, lineShown = false;
                    try
                    {
                        ObjectId textId = ObjectId.Null, leaderId = ObjectId.Null;
                        string originalText = null;
                        Point3d originalPosition = Point3d.Origin;
                        if (existing == null)
                        {
                            var anchorPrompt = new PromptPointOptions("\n点选钢筋上的引出位置: ");
                            PromptPointResult anchor = editor.GetPoint(anchorPrompt);
                            if (anchor.Status != PromptStatus.OK) throw new OperationCanceledException();
                            PromptPointResult location = editor.GetPoint("\n指定标注文字的位置: ");
                            if (location.Status != PromptStatus.OK) throw new OperationCanceledException();
                            Point3d onSteel = steel.GetClosestPointTo(anchor.Value, false);
                            double height = AnnotationHeightMm * Commands.MillimetresToDrawingUnits(doc.Database);
                            text = new DBText();
                            text.SetDatabaseDefaults(doc.Database);
                            text.TextStyleId = styleId;
                            text.Height = height;
                            text.Position = location.Value;
                            text.TextString = CadLabel(label);
                            leader = new Line(onSteel, location.Value);
                            leader.SetDatabaseDefaults(doc.Database);
                        }
                        else
                        {
                            if (existing.TextId.IsErased || existing.LeaderId.IsErased)
                                throw new InvalidOperationException("标注文字或引线已删除，当前不能修改，请刷新列表。");
                            DBText saved = tr.GetObject(existing.TextId, OpenMode.ForRead) as DBText;
                            Line savedLine = tr.GetObject(existing.LeaderId, OpenMode.ForRead) as Line;
                            if (saved == null || savedLine == null)
                                throw new InvalidOperationException("标注实体类型已改变。");
                            text = (DBText)saved.Clone();
                            originalText = saved.TextString;
                            originalPosition = saved.Position;
                            if (originalText == CadLabel(label))
                                throw new InvalidOperationException("标注文字没有变化。");
                            text.TextString = CadLabel(label);
                            textId = existing.TextId;
                            leaderId = existing.LeaderId;
                        }
                        text.ColorIndex = 1;
                        if (!TransientManager.CurrentTransientManager.AddTransient(text, TransientDrawingMode.DirectShortTerm, 128, viewports))
                            throw new InvalidOperationException("无法显示标注预览。");
                        textShown = true;
                        if (leader != null)
                        {
                            leader.ColorIndex = 1;
                            if (!TransientManager.CurrentTransientManager.AddTransient(leader, TransientDrawingMode.DirectShortTerm, 128, viewports))
                                throw new InvalidOperationException("无法显示引线预览。");
                            lineShown = true;
                        }
                        annotationPreview = new AnnotationPreview { Document = doc, RebarId = rebarId,
                            RebarEntityId = rebar.EntityId, SpaceId = steel.OwnerId, Units = doc.Database.Insunits,
                            AnnotationId = existing == null ? Guid.Empty : existing.Id,
                            TextId = textId, LeaderId = leaderId, OriginalText = originalText,
                            OriginalPosition = originalPosition, Text = text, Leader = leader, Label = label };
                        panel.SetAnnotationPreviewAvailable(true);
                        panel.ShowStatus(existing == null ? "红色文字和引线是临时预览；确认后写入图纸。" :
                            "红色文字是修改预览；确认后只改标注文字，不改钢筋。");
                    }
                    catch
                    {
                        if (annotationPreview == null)
                        {
                            if (textShown) TransientManager.CurrentTransientManager.EraseTransient(text, viewports);
                            if (lineShown) TransientManager.CurrentTransientManager.EraseTransient(leader, viewports);
                            if (text != null) text.Dispose();
                            if (leader != null) leader.Dispose();
                        }
                        throw;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ClearPreview();
                panel.ShowStatus("标注预览已取消，图纸不变。");
            }
            catch (System.Exception ex)
            {
                ClearPreview();
                panel.ShowStatus("标注预览失败：" + ex.Message);
                editor.WriteMessage("\n标注预览失败：{0}", ex.Message);
            }
        }

        internal static void ConfirmAnnotation()
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null || panel == null || annotationPreview == null) return;
            AnnotationPreview state = annotationPreview;
            try
            {
                if (!ReferenceEquals(doc, state.Document) || panel.SelectedRebarId != state.RebarId ||
                    panel.AnnotationLabel != state.Label || doc.Database.Insunits != state.Units)
                    throw new InvalidOperationException("图纸、钢筋、单位或文字已变化，请重新预览。");
                if (state.AnnotationId == Guid.Empty &&
                    (!LayerTools.ConfirmNew(doc.Database, doc.Editor, AnnotationTextLayer) ||
                     !LayerTools.ConfirmNew(doc.Database, doc.Editor, AnnotationLeaderLayer)))
                {
                    ClearPreview();
                    panel.ShowStatus("未创建图层；标注预览已取消。");
                    return;
                }
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    DrawingData.Index data = DrawingData.Read(doc.Database, tr);
                    DrawingData.Rebar rebar = data.Rebars.Find(r => r.Id == state.RebarId);
                    if (rebar == null || rebar.EntityId != state.RebarEntityId || rebar.EntityId.IsErased)
                        throw new InvalidOperationException("钢筋已变化或删除，请重新预览。");
                    Polyline steel = tr.GetObject(rebar.EntityId, OpenMode.ForRead) as Polyline;
                    if (steel == null || steel.OwnerId != state.SpaceId)
                        throw new InvalidOperationException("钢筋所在空间已变化，请重新预览。");
                    if (state.AnnotationId == Guid.Empty)
                    {
                        if (data.Annotations.Exists(a => a.RebarId == state.RebarId))
                            throw new InvalidOperationException("该钢筋已有标注，请刷新。");
                        AnnotationService.Create(doc.Database, tr, state.RebarId, state.SpaceId,
                            state.Text, state.Leader, state.Label);
                        tr.Commit();
                    }
                    else
                    {
                        DrawingData.Annotation annotation = data.Annotations.Find(a => a.Id == state.AnnotationId);
                        if (annotation == null || annotation.RebarId != state.RebarId ||
                            annotation.TextId != state.TextId || annotation.LeaderId != state.LeaderId ||
                            state.TextId.IsErased || state.LeaderId.IsErased)
                            throw new InvalidOperationException("标注已变化或删除，请重新预览。");
                        AnnotationService.Edit(doc.Database, tr, annotation, state.OriginalText,
                            state.OriginalPosition, state.Label);
                        tr.Commit();
                    }
                }
                ClearPreview();
                panel.RefreshTree(state.RebarId);
                panel.ShowStatus("直径标注已保存；钢筋实体及几何未修改。请人工核对文字与钢筋计算直径。");
            }
            catch (System.Exception ex)
            {
                ClearPreview();
                panel.ShowStatus("标注未保存：" + ex.Message);
                doc.Editor.WriteMessage("\n标注未保存：{0}", ex.Message);
            }
        }
    }

    internal static class AnnotationService
    {
        internal static Guid Create(Database db, Transaction tr, Guid rebarId, ObjectId spaceId,
            DBText previewText, Line previewLeader, string label)
        {
            if (previewText == null || previewLeader == null || spaceId.IsNull)
                throw new InvalidOperationException("标注预览无效。");
            TextStyleTable styles = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (!styles.Has("TSSD")) throw new InvalidOperationException("当前图纸缺少 TSSD 文字样式。");
            LayerTools.Ensure(db, tr, "S-TEXT");
            LayerTools.Ensure(db, tr, "s-floor_de_dim");
            using (DBText text = (DBText)previewText.Clone())
            using (Line leader = (Line)previewLeader.Clone())
            {
                text.ColorIndex = 256;
                leader.ColorIndex = 256;
                text.TextStyleId = styles["TSSD"];
                text.Layer = "S-TEXT";
                leader.Layer = "s-floor_de_dim";
                BlockTableRecord space = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForWrite);
                space.AppendEntity(text);
                tr.AddNewlyCreatedDBObject(text, true);
                space.AppendEntity(leader);
                tr.AddNewlyCreatedDBObject(leader, true);
                return DrawingData.AddAnnotation(db, tr, rebarId, text, leader, label);
            }
        }

        internal static void Edit(Database db, Transaction tr, DrawingData.Annotation annotation,
            string expectedText, Point3d expectedPosition, string label)
        {
            if (annotation == null || annotation.TextId.IsNull || annotation.TextId.IsErased ||
                annotation.LeaderId.IsNull || annotation.LeaderId.IsErased)
                throw new InvalidOperationException("标注已删除或失联。");
            DBText text = tr.GetObject(annotation.TextId, OpenMode.ForWrite) as DBText;
            if (text == null || text.TextString != expectedText || text.Position != expectedPosition)
                throw new InvalidOperationException("标注文字已变化，请重新预览。");
            text.TextString = label.Replace("Φ", "%%c").Replace("φ", "%%c");
            annotation.Label = label;
            DrawingData.UpdateAnnotation(db, tr, annotation);
        }
    }

    public partial class Commands
    {
        [CommandMethod("WDTLABELPREVIEW", CommandFlags.Modal)]
        public void PreviewDiameterAnnotation() { RebarPalette.PreviewAnnotation(); }

        [CommandMethod("WDTLABELCONFIRM", CommandFlags.Modal)]
        public void ConfirmDiameterAnnotation() { RebarPalette.ConfirmAnnotation(); }
    }
}
