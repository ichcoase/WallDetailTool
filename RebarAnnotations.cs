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
            internal ObjectId SlashId;
            internal string OriginalText;
            internal Point3d OriginalPosition;
            internal bool Relayout;
            internal DBText Text;
            internal Polyline Leader;
            internal Polyline Slash;
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
                    if (old.Slash != null)
                        TransientManager.CurrentTransientManager.EraseTransient(old.Slash, viewports);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception) { /* Closing drawing. */ }
                finally
                {
                    old.Text.Dispose();
                    if (old.Leader != null) old.Leader.Dispose();
                    if (old.Slash != null) old.Slash.Dispose();
                }
            }
            if (panel != null) panel.SetAnnotationPreviewAvailable(false);
        }

        internal static void PreviewAnnotation(bool relayout = false)
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
                    Polyline leader = null, slash = null;
                    bool textShown = false, lineShown = false, slashShown = false;
                    try
                    {
                        ObjectId textId = ObjectId.Null, leaderId = ObjectId.Null, slashId = ObjectId.Null;
                        string originalText = null;
                        Point3d originalPosition = Point3d.Origin;
                        if (relayout && existing == null)
                            throw new InvalidOperationException("这条钢筋还没有标注，请先创建标注。");
                        if (existing == null || relayout)
                        {
                            if (existing != null)
                            {
                                if (existing.TextId.IsErased || existing.LeaderId.IsErased ||
                                    (!existing.SlashId.IsNull && existing.SlashId.IsErased))
                                    throw new InvalidOperationException("原标注实体已删除，请刷新列表。");
                                DBText saved = tr.GetObject(existing.TextId, OpenMode.ForRead) as DBText;
                                Entity savedLeader = tr.GetObject(existing.LeaderId, OpenMode.ForRead) as Entity;
                                if (saved == null || !(savedLeader is Line || savedLeader is Polyline))
                                    throw new InvalidOperationException("原标注实体类型已变化。");
                                originalText = saved.TextString;
                                originalPosition = saved.Position;
                                textId = existing.TextId;
                                leaderId = existing.LeaderId;
                                slashId = existing.SlashId;
                            }
                            var anchorPrompt = new PromptPointOptions("\n点选钢筋上的引出位置: ");
                            PromptPointResult anchor = editor.GetPoint(anchorPrompt);
                            if (anchor.Status != PromptStatus.OK) throw new OperationCanceledException();
                            PromptPointResult location = editor.GetPoint("\n指定文字下方横线的末端（文字右端）: ");
                            if (location.Status != PromptStatus.OK) throw new OperationCanceledException();
                            Point3d onSteel = steel.GetClosestPointTo(anchor.Value, false);
                            double height = AnnotationHeightMm * Commands.MillimetresToDrawingUnits(doc.Database);
                            AnnotationGeometry.Build(doc.Database, steel, onSteel, location.Value,
                                CadLabel(label), styleId, height, out text, out leader, out slash);
                        }
                        else
                        {
                            if (existing.TextId.IsErased || existing.LeaderId.IsErased)
                                throw new InvalidOperationException("标注文字或引线已删除，当前不能修改，请刷新列表。");
                            DBText saved = tr.GetObject(existing.TextId, OpenMode.ForRead) as DBText;
                            Entity savedLine = tr.GetObject(existing.LeaderId, OpenMode.ForRead) as Entity;
                            if (saved == null || !(savedLine is Line || savedLine is Polyline))
                                throw new InvalidOperationException("标注实体类型已改变。");
                            text = (DBText)saved.Clone();
                            originalText = saved.TextString;
                            originalPosition = saved.Position;
                            if (originalText == CadLabel(label))
                                throw new InvalidOperationException("标注文字没有变化。");
                            text.TextString = CadLabel(label);
                            textId = existing.TextId;
                            leaderId = existing.LeaderId;
                            slashId = existing.SlashId;
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
                        if (slash != null)
                        {
                            slash.ColorIndex = 1;
                            if (!TransientManager.CurrentTransientManager.AddTransient(slash, TransientDrawingMode.DirectShortTerm, 128, viewports))
                                throw new InvalidOperationException("无法显示斜杠预览。");
                            slashShown = true;
                        }
                        annotationPreview = new AnnotationPreview { Document = doc, RebarId = rebarId,
                            RebarEntityId = rebar.EntityId, SpaceId = steel.OwnerId, Units = doc.Database.Insunits,
                            AnnotationId = existing == null ? Guid.Empty : existing.Id,
                            TextId = textId, LeaderId = leaderId, SlashId = slashId, OriginalText = originalText,
                            OriginalPosition = originalPosition, Relayout = relayout,
                            Text = text, Leader = leader, Slash = slash, Label = label };
                        panel.SetAnnotationPreviewAvailable(true);
                        panel.ShowStatus(existing == null ? "红色文字、直角引线和斜杠是临时预览；确认后写入图纸。" :
                            relayout ? "红色标注是位置调整预览；确认后替换原文字布局和引线，不改钢筋。" :
                            "红色文字是修改预览；确认后只改标注文字，不改钢筋。");
                    }
                    catch
                    {
                        if (annotationPreview == null)
                        {
                            if (textShown) TransientManager.CurrentTransientManager.EraseTransient(text, viewports);
                            if (lineShown) TransientManager.CurrentTransientManager.EraseTransient(leader, viewports);
                            if (slashShown) TransientManager.CurrentTransientManager.EraseTransient(slash, viewports);
                            if (text != null) text.Dispose();
                            if (leader != null) leader.Dispose();
                            if (slash != null) slash.Dispose();
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
                if ((state.AnnotationId == Guid.Empty || state.Relayout) &&
                    !LayerTools.ConfirmNew(doc.Database, doc.Editor, AnnotationTextLayer))
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
                            state.Text, state.Leader, state.Slash, state.Label);
                        tr.Commit();
                    }
                    else
                    {
                        DrawingData.Annotation annotation = data.Annotations.Find(a => a.Id == state.AnnotationId);
                        if (annotation == null || annotation.RebarId != state.RebarId ||
                            annotation.TextId != state.TextId || annotation.LeaderId != state.LeaderId ||
                            annotation.SlashId != state.SlashId || state.TextId.IsErased || state.LeaderId.IsErased ||
                            (!state.SlashId.IsNull && state.SlashId.IsErased))
                            throw new InvalidOperationException("标注已变化或删除，请重新预览。");
                        if (state.Relayout)
                            AnnotationService.Relayout(doc.Database, tr, annotation, state.OriginalText,
                                state.OriginalPosition, state.Text, state.Leader, state.Slash, state.Label);
                        else
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

    internal static class AnnotationGeometry
    {
        internal static void Build(Database db, Polyline steel, Point3d anchor, Point3d textEnd,
            string label, ObjectId styleId, double height, out DBText text,
            out Polyline leader, out Polyline slash)
        {
            text = null; leader = null; slash = null;
            if (steel == null || steel.NumberOfVertices < 2 ||
                (steel.Normal - Vector3d.ZAxis).Length > 1e-8 ||
                !Commands.Finite(height) || height <= 0)
                throw new InvalidOperationException("当前只支持平面直线钢筋。");
            for (int i = 0; i < steel.NumberOfVertices - (steel.Closed ? 0 : 1); i++)
            {
                Point2d a = steel.GetPoint2dAt(i), b = steel.GetPoint2dAt((i + 1) % steel.NumberOfVertices);
                if (Math.Abs(steel.GetBulgeAt(i)) > 1e-10 || a.GetDistanceTo(b) < 1e-8)
                    throw new InvalidOperationException("钢筋含圆弧或零长度线段，不能标注。");
            }
            double dx = textEnd.X - anchor.X, dy = textEnd.Y - anchor.Y;
            if (Math.Abs(dx) < height * .3)
                throw new InvalidOperationException("文字右端与引出点的水平距离太小，请向左或向右移开。");
            if (!Commands.Finite(dx) || !Commands.Finite(dy))
                throw new InvalidOperationException("标注位置无效。");
            Vector3d tangent = steel.GetFirstDerivative(anchor);
            double tangentLength = Math.Sqrt(tangent.X * tangent.X + tangent.Y * tangent.Y);
            if (tangentLength < 1e-8)
                throw new InvalidOperationException("引出点位于无法确定方向的位置，请在线段中间重选。");
            double scale = height / 62.5;
            double slashLength = 150 * scale, slashWidth = 40 * scale;
            // Rotate the local steel tangent 45 degrees so the short mark crosses it.
            double ux = (tangent.X - tangent.Y) / (tangentLength * Math.Sqrt(2));
            double uy = (tangent.X + tangent.Y) / (tangentLength * Math.Sqrt(2));
            try
            {
                text = new DBText(); text.SetDatabaseDefaults(db);
                text.TextStyleId = styleId; text.Height = height; text.TextString = label;
                text.HorizontalMode = TextHorizontalMode.TextRight;
                text.AlignmentPoint = new Point3d(textEnd.X, textEnd.Y + height * .12, anchor.Z);
                text.Position = text.AlignmentPoint;
                text.AdjustAlignment(db);
                leader = new Polyline(); leader.SetDatabaseDefaults(db); leader.Elevation = anchor.Z;
                leader.AddVertexAt(0, new Point2d(anchor.X, anchor.Y), 0, 0, 0);
                int last = 1;
                if (Math.Abs(dy) > height * .01)
                {
                    leader.AddVertexAt(last++, new Point2d(anchor.X, textEnd.Y), 0, 0, 0);
                }
                leader.AddVertexAt(last, new Point2d(textEnd.X, textEnd.Y), 0, 0, 0);
                slash = new Polyline(); slash.SetDatabaseDefaults(db); slash.Elevation = anchor.Z;
                slash.AddVertexAt(0, new Point2d(anchor.X - ux * slashLength / 2,
                    anchor.Y - uy * slashLength / 2), 0, slashWidth, slashWidth);
                slash.AddVertexAt(1, new Point2d(anchor.X + ux * slashLength / 2,
                    anchor.Y + uy * slashLength / 2), 0, 0, 0);
            }
            catch
            {
                if (text != null) text.Dispose();
                if (leader != null) leader.Dispose();
                if (slash != null) slash.Dispose();
                text = null; leader = null; slash = null;
                throw;
            }
        }
    }

    internal static class AnnotationService
    {
        internal static Guid Create(Database db, Transaction tr, Guid rebarId, ObjectId spaceId,
            DBText previewText, Polyline previewLeader, Polyline previewSlash, string label)
        {
            if (previewText == null || previewLeader == null || previewSlash == null || spaceId.IsNull)
                throw new InvalidOperationException("标注预览无效。");
            TextStyleTable styles = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (!styles.Has("TSSD")) throw new InvalidOperationException("当前图纸缺少 TSSD 文字样式。");
            LayerTools.Ensure(db, tr, "S-TEXT");
            using (DBText text = (DBText)previewText.Clone())
            using (Polyline leader = (Polyline)previewLeader.Clone())
            using (Polyline slash = (Polyline)previewSlash.Clone())
            {
                text.ColorIndex = 256;
                leader.ColorIndex = 256;
                slash.ColorIndex = 256;
                text.TextStyleId = styles["TSSD"];
                text.Layer = "S-TEXT";
                leader.Layer = "S-TEXT";
                slash.Layer = "S-TEXT";
                BlockTableRecord space = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForWrite);
                space.AppendEntity(text);
                tr.AddNewlyCreatedDBObject(text, true);
                text.AdjustAlignment(db);
                space.AppendEntity(leader);
                tr.AddNewlyCreatedDBObject(leader, true);
                space.AppendEntity(slash);
                tr.AddNewlyCreatedDBObject(slash, true);
                return DrawingData.AddAnnotation(db, tr, rebarId, text, leader, slash, label);
            }
        }

        internal static void Relayout(Database db, Transaction tr, DrawingData.Annotation annotation,
            string expectedText, Point3d expectedPosition, DBText previewText,
            Polyline previewLeader, Polyline previewSlash, string label)
        {
            if (annotation == null || previewText == null || previewLeader == null || previewSlash == null ||
                annotation.TextId.IsErased || annotation.LeaderId.IsErased ||
                (!annotation.SlashId.IsNull && annotation.SlashId.IsErased))
                throw new InvalidOperationException("标注已变化，请重新预览。");
            DBText text = tr.GetObject(annotation.TextId, OpenMode.ForWrite) as DBText;
            Entity oldLeader = tr.GetObject(annotation.LeaderId, OpenMode.ForWrite) as Entity;
            if (text == null || oldLeader == null || text.TextString != expectedText || text.Position != expectedPosition)
                throw new InvalidOperationException("原标注已修改，请重新预览。");
            if (!annotation.SlashId.IsNull)
                ((Entity)tr.GetObject(annotation.SlashId, OpenMode.ForWrite)).Erase();
            ObjectId spaceId = text.OwnerId;
            if (oldLeader.OwnerId != spaceId)
                throw new InvalidOperationException("原引线所在空间已变化。");
            LayerTools.Ensure(db, tr, "S-TEXT");
            BlockTableRecord space = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForWrite);
            using (Polyline leader = (Polyline)previewLeader.Clone())
            using (Polyline slash = (Polyline)previewSlash.Clone())
            {
                leader.Layer = slash.Layer = "S-TEXT";
                leader.ColorIndex = slash.ColorIndex = 256;
                space.AppendEntity(leader); tr.AddNewlyCreatedDBObject(leader, true);
                space.AppendEntity(slash); tr.AddNewlyCreatedDBObject(slash, true);
                oldLeader.Erase();
                text.TextString = previewText.TextString;
                text.TextStyleId = previewText.TextStyleId;
                text.Height = previewText.Height;
                text.HorizontalMode = previewText.HorizontalMode;
                text.AlignmentPoint = previewText.AlignmentPoint;
                text.AdjustAlignment(db);
                text.Layer = "S-TEXT";
                annotation.LeaderId = leader.ObjectId;
                annotation.SlashId = slash.ObjectId;
                annotation.Label = label;
                DrawingData.UpdateAnnotation(db, tr, annotation);
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

        [CommandMethod("WDTLABELAYOUT", CommandFlags.Modal)]
        public void PreviewDiameterLayout() { RebarPalette.PreviewAnnotation(true); }

        [CommandMethod("WDTLABELCONFIRM", CommandFlags.Modal)]
        public void ConfirmDiameterAnnotation() { RebarPalette.ConfirmAnnotation(); }
    }
}
