using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using TransientManager = Autodesk.AutoCAD.GraphicsInterface.TransientManager;
using TransientDrawingMode = Autodesk.AutoCAD.GraphicsInterface.TransientDrawingMode;

namespace WallDetailTool
{
    // Palette buttons schedule commands so all database reads/writes occur in AutoCAD's document context.
    internal static class RebarPalette
    {
        private static PaletteSet palette;
        private static RebarPanel panel;
        private static PreviewState preview;
        private static readonly IntegerCollection viewports = new IntegerCollection();

        private sealed class PreviewState
        {
            internal Document Document;
            internal ObjectId SourceId;
            internal ObjectId SpaceId;
            internal UnitsValue Units;
            internal Polyline SourceSnapshot;
            internal Polyline Result;
            internal double Cover;
            internal double Diameter;
        }

        internal static void Open()
        {
            if (palette == null)
            {
                panel = new RebarPanel();
                panel.VisibleChanged += (sender, args) => { if (!panel.Visible) ClearPreview(); };
                palette = new PaletteSet("WallDetailTool 钢筋", new Guid("AD68FBD8-B905-42C1-A73B-CB63399C94D4"));
                palette.Size = new Size(300, 350);
                palette.DockEnabled = DockSides.Left | DockSides.Right;
                palette.Add("钢筋", panel);
                palette.StateChanged += (sender, args) => { if (!palette.Visible) ClearPreview(); };
                CadApplication.DocumentManager.DocumentToBeDeactivated += OnDocumentChanging;
                CadApplication.DocumentManager.DocumentToBeDestroyed += OnDocumentChanging;
                CadApplication.DocumentManager.DocumentActivated += OnDocumentActivated;
            }
            palette.Visible = true;
            panel.RefreshDocument();
        }

        private static void OnDocumentChanging(object sender, DocumentCollectionEventArgs args)
        {
            if (preview != null && ReferenceEquals(preview.Document, args.Document)) ClearPreview();
        }

        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs args)
        {
            if (preview != null && !ReferenceEquals(preview.Document, args.Document)) ClearPreview();
            if (panel != null) panel.RefreshDocument();
        }

        internal static void Queue(string command)
        {
            Document document = CadApplication.DocumentManager.MdiActiveDocument;
            if (document == null) { if (panel != null) panel.ShowStatus("请先打开图纸。"); return; }
            document.SendStringToExecute(command + " ", true, false, false);
        }

        internal static void ClearPreview()
        {
            PreviewState old = preview;
            preview = null;
            if (old != null)
            {
                try { TransientManager.CurrentTransientManager.EraseTransient(old.Result, viewports); }
                catch (Autodesk.AutoCAD.Runtime.Exception) { /* The document may already be closing. */ }
                finally { old.Result.Dispose(); old.SourceSnapshot.Dispose(); }
            }
            if (panel != null) panel.SetPreviewAvailable(false);
        }

        internal static void SelectAndPreview()
        {
            Document document = CadApplication.DocumentManager.MdiActiveDocument;
            if (document == null || panel == null || palette == null || !palette.Visible) return;
            ClearPreview();
            Editor editor = document.Editor;
            try
            {
                double cover, diameter;
                panel.ReadParameters(out cover, out diameter);
                var options = new PromptEntityOptions("\n选择开放式直线二维混凝土轮廓: ");
                options.SetRejectMessage("\n请选择二维多段线。");
                options.AddAllowedClass(typeof(Polyline), true);
                options.AddAllowedClass(typeof(Polyline2d), true);
                PromptEntityResult selection = editor.GetEntity(options);
                if (selection.Status != PromptStatus.OK) { panel.ShowStatus("已取消选择，没有生成图形。"); return; }

                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                using (Polyline source = Commands.CopyContour((Entity)transaction.GetObject(selection.ObjectId, OpenMode.ForRead)))
                {
                    Commands.ValidatePolyline(source);
                    double scale = Commands.MillimetresToDrawingUnits(document.Database);
                    double distance = (cover + diameter / 2.0) * scale;
                    if (!Commands.Finite(distance) || distance <= 1e-6)
                        throw new InvalidOperationException("中心线偏移距离无效或小于几何容差。");
                    if (document.Database.Insunits == UnitsValue.Undefined)
                        editor.WriteMessage("\n图纸未设置单位，本命令按 1 图形单位 = 1 mm 处理。");

                    double signedDistance;
                    while (true)
                    {
                        PromptPointResult side = editor.GetPoint("\n在线段中部附近点选钢筋所在的一侧: ");
                        if (side.Status != PromptStatus.OK) { panel.ShowStatus("已取消方向选择，没有生成图形。"); return; }
                        if (Commands.TryDirection(source, side.Value, distance, out signedDistance)) break;
                        editor.WriteMessage("\n方向不明确，请避开轮廓、端点和拐角后重新点选。");
                    }
                    Polyline result = Commands.CreateRebar(source, signedDistance, distance, diameter * scale, document.Database);
                    Polyline snapshot = null;
                    bool displayed = false;
                    try
                    {
                        snapshot = (Polyline)source.Clone();
                        result.ColorIndex = 1;
                        if (!TransientManager.CurrentTransientManager.AddTransient(result, TransientDrawingMode.DirectShortTerm, 128, viewports))
                            throw new InvalidOperationException("AutoCAD 未能显示临时预览。");
                        displayed = true;
                        preview = new PreviewState
                        {
                            Document = document, SourceId = selection.ObjectId,
                            SpaceId = document.Database.CurrentSpaceId, Units = document.Database.Insunits,
                            SourceSnapshot = snapshot, Result = result,
                            Cover = cover, Diameter = diameter
                        };
                        panel.SetPreviewAvailable(true);
                        panel.ShowStatus("红色为临时预览。确认后才写入图纸；改参数后请重新预览。");
                    }
                    catch
                    {
                        if (preview == null)
                        {
                            if (displayed) TransientManager.CurrentTransientManager.EraseTransient(result, viewports);
                            result.Dispose();
                            if (snapshot != null) snapshot.Dispose();
                        }
                        throw;
                    }
                }
            }
            catch (System.Exception ex)
            {
                ClearPreview();
                panel.ShowStatus("预览失败：" + ex.Message);
                editor.WriteMessage("\n预览失败：{0}", ex.Message);
            }
        }

        internal static void Confirm()
        {
            Document document = CadApplication.DocumentManager.MdiActiveDocument;
            if (document == null || panel == null || preview == null) return;
            PreviewState state = preview;
            if (!ReferenceEquals(state.Document, document)) { ClearPreview(); return; }
            try
            {
                double cover, diameter;
                panel.ReadParameters(out cover, out diameter);
                if (cover != state.Cover || diameter != state.Diameter ||
                    document.Database.Insunits != state.Units || document.Database.CurrentSpaceId != state.SpaceId)
                    throw new InvalidOperationException("参数、图纸单位或绘图空间已变化，请重新预览。");
                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    Entity original = transaction.GetObject(state.SourceId, OpenMode.ForRead) as Entity;
                    if (original == null || original.IsErased)
                        throw new InvalidOperationException("原轮廓已删除，请重新选择。");
                    using (Polyline current = Commands.CopyContour(original))
                    {
                        if (!SameContour(state.SourceSnapshot, current))
                            throw new InvalidOperationException("原轮廓已变化，请重新预览。");
                    }
                    using (Polyline result = (Polyline)state.Result.Clone())
                    {
                        // The red color belongs only to the temporary preview.
                        result.SetDatabaseDefaults(document.Database);
                        result.ColorIndex = 256;
                        Commands.SetRebarWidth(result, state.Result.ConstantWidth);
                        BlockTableRecord space = (BlockTableRecord)transaction.GetObject(state.SpaceId, OpenMode.ForWrite);
                        space.AppendEntity(result);
                        transaction.AddNewlyCreatedDBObject(result, true);
                        transaction.Commit();
                    }
                }
                ClearPreview();
                panel.ShowStatus("钢筋已生成。可用 AutoCAD 撤销命令移除。");
                document.Editor.WriteMessage("\n钢筋已生成：宽度 {0:g} mm，中心线偏移 {1:g} mm。", diameter, cover + diameter / 2.0);
            }
            catch (System.Exception ex)
            {
                ClearPreview();
                panel.ShowStatus("未生成：" + ex.Message);
                document.Editor.WriteMessage("\n未生成钢筋：{0}", ex.Message);
            }
        }

        private static bool SameContour(Polyline a, Polyline b)
        {
            if (a.NumberOfVertices != b.NumberOfVertices || a.Closed != b.Closed ||
                a.Normal != b.Normal || a.Elevation != b.Elevation) return false;
            for (int i = 0; i < a.NumberOfVertices; i++)
                if (a.GetPoint2dAt(i) != b.GetPoint2dAt(i) || a.GetBulgeAt(i) != b.GetBulgeAt(i) ||
                    a.GetStartWidthAt(i) != b.GetStartWidthAt(i) || a.GetEndWidthAt(i) != b.GetEndWidthAt(i)) return false;
            return true;
        }
    }

    internal sealed class RebarPanel : UserControl
    {
        private readonly TextBox cover = new TextBox { Text = "25", Width = 190 };
        private readonly TextBox diameter = new TextBox { Text = "12", Width = 190 };
        private readonly Label units = new Label { AutoSize = true };
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(245, 0), Text = "选择轮廓后显示临时预览。" };
        private readonly Button select = new Button { Text = "选择轮廓并预览", Width = 220, Height = 34 };
        private readonly Button confirm = new Button { Text = "确认生成", Width = 220, Height = 34, Enabled = false };
        private readonly Button cancel = new Button { Text = "取消预览", Width = 220, Height = 34, Enabled = false };

        internal RebarPanel()
        {
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = System.Windows.Forms.FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(12) };
            layout.Controls.Add(new Label { Text = "混凝土保护层（mm）", AutoSize = true });
            layout.Controls.Add(cover);
            layout.Controls.Add(new Label { Text = "钢筋直径（mm）", AutoSize = true, Margin = new Padding(3, 12, 3, 3) });
            layout.Controls.Add(diameter);
            layout.Controls.Add(units);
            layout.Controls.Add(select);
            layout.Controls.Add(confirm);
            layout.Controls.Add(cancel);
            layout.Controls.Add(status);
            Controls.Add(layout);
            cover.TextChanged += ParametersChanged;
            diameter.TextChanged += ParametersChanged;
            select.Click += (sender, args) => RebarPalette.Queue("WDTSELECT");
            confirm.Click += (sender, args) => RebarPalette.Queue("WDTCONFIRM");
            cancel.Click += (sender, args) => { RebarPalette.ClearPreview(); ShowStatus("预览已取消，没有生成图形。"); };
        }

        private void ParametersChanged(object sender, EventArgs args)
        {
            RebarPalette.ClearPreview();
            ShowStatus("参数已变化；请重新选择轮廓并预览。");
        }

        internal void ReadParameters(out double coverMm, out double diameterMm)
        {
            if (!double.TryParse(cover.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out coverMm) ||
                !Commands.Finite(coverMm) || coverMm < 0)
                throw new InvalidOperationException("保护层请输入不小于零的毫米数。");
            if (!double.TryParse(diameter.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out diameterMm) ||
                !Commands.Finite(diameterMm) || diameterMm <= 0)
                throw new InvalidOperationException("直径请输入大于零的毫米数。");
        }

        internal void RefreshDocument()
        {
            Document document = CadApplication.DocumentManager.MdiActiveDocument;
            select.Enabled = document != null;
            if (document == null) { units.Text = "请先打开图纸。"; SetPreviewAvailable(false); return; }
            UnitsValue drawingUnits = document.Database.Insunits;
            units.Text = drawingUnits == UnitsValue.Undefined ? "未设置图纸单位：按毫米处理" :
                "图纸单位：" + drawingUnits + "；输入仍为毫米";
        }

        internal void SetPreviewAvailable(bool value) { confirm.Enabled = value; cancel.Enabled = value; }
        internal void ShowStatus(string message) { status.Text = message; }
    }

    public partial class Commands
    {
        [CommandMethod("WDTPANEL", CommandFlags.Session)]
        public void ShowRebarPalette() { RebarPalette.Open(); }

        [CommandMethod("WDTSELECT", CommandFlags.Modal)]
        public void SelectRebarPreview() { RebarPalette.SelectAndPreview(); }

        [CommandMethod("WDTCONFIRM", CommandFlags.Modal)]
        public void ConfirmRebarPreview() { RebarPalette.Confirm(); }
    }
}
