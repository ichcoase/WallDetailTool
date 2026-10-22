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
    internal static partial class RebarPalette
    {
        private static PaletteSet palette;
        private static RebarPanel panel;
        private static UpstandPanelView upstandPanel;
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
            internal double DisplayWidthMillimetres;
            internal Guid GroupId;
            internal double Side;
            internal string Layer;
        }

        internal static void Open()
        {
            if (palette == null)
            {
                panel = new RebarPanel();
                upstandPanel = new UpstandPanelView();
                panel.VisibleChanged += (sender, args) => { if (!panel.Visible) ClearPreview(); };
                palette = new PaletteSet("WallDetailTool 梁上反坎", new Guid("AD68FBD8-B905-42C1-A73B-CB63399C94D4"));
                palette.Size = new Size(320, 600);
                palette.DockEnabled = DockSides.Left | DockSides.Right;
                palette.Add("梁上反坎", upstandPanel);
                palette.StateChanged += (sender, args) => { if (!palette.Visible) ClearPreview(); };
                CadApplication.DocumentManager.DocumentToBeDeactivated += OnDocumentChanging;
                CadApplication.DocumentManager.DocumentToBeDestroyed += OnDocumentChanging;
                CadApplication.DocumentManager.DocumentActivated += OnDocumentActivated;
            }
            palette.Visible = true;
            panel.RefreshDocument();
            RefreshUpstandPanel();
        }

        private static void OnDocumentChanging(object sender, DocumentCollectionEventArgs args)
        {
            if ((preview != null && ReferenceEquals(preview.Document, args.Document)) ||
                (editPreview != null && ReferenceEquals(editPreview.Document, args.Document)) ||
                (annotationPreview != null && ReferenceEquals(annotationPreview.Document, args.Document))) ClearPreview();
        }

        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs args)
        {
            if ((preview != null && !ReferenceEquals(preview.Document, args.Document)) ||
                (editPreview != null && !ReferenceEquals(editPreview.Document, args.Document)) ||
                (annotationPreview != null && !ReferenceEquals(annotationPreview.Document, args.Document))) ClearPreview();
            if (panel != null) panel.RefreshDocument();
            RefreshUpstandPanel();
        }

        private static void RefreshUpstandPanel()
        {
            Document active=CadApplication.DocumentManager.MdiActiveDocument;
            if (upstandPanel!=null && active!=null) upstandPanel.SetValues(Commands.PanelValues(active.Database));
        }
        internal static UpstandPanelValues ReadUpstandPanel()
        {
            if (upstandPanel==null) throw new InvalidOperationException("请先运行 WDTPANEL 打开反坎参数面板。");
            return upstandPanel.ReadValues();
        }
        internal static void UpdateUpstandAdvanced(UpstandPanelValues values)
        {
            if (upstandPanel!=null) upstandPanel.UpdateAdvanced(values);
        }

        internal static void Queue(string command)
        {
            Document document = CadApplication.DocumentManager.MdiActiveDocument;
            if (document == null) { if (panel != null) panel.ShowStatus("请先打开图纸。"); return; }
            document.SendStringToExecute(command + " ", true, false, false);
        }

        internal static void ClearPreview()
        {
            ClearEditPreview();
            ClearAnnotationPreview();
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
                double displayWidth = PluginSettings.ReadLineWidthMillimetres();
                string layer = LayerTools.CheckName(panel.LayerName);
                Guid groupId = panel.SelectedGroupId;
                if (groupId == Guid.Empty) throw new InvalidOperationException("请先在侧边栏创建并选中一个钢筋组。");
                using (Transaction read = document.Database.TransactionManager.StartTransaction())
                    if (!DrawingData.Read(document.Database, read).Groups.Exists(g => g.Id == groupId))
                        throw new InvalidOperationException("所选钢筋组已不存在，请刷新面板。");
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
                    Polyline result = Commands.CreateRebar(source, signedDistance, distance, displayWidth * scale, document.Database);
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
                            Cover = cover, Diameter = diameter, DisplayWidthMillimetres = displayWidth, GroupId = groupId,
                            Side = Math.Sign(signedDistance), Layer = layer
                        };
                        panel.SetPreviewAvailable(true);
                        panel.ShowStatus("红色为临时预览；目标图层：" + layer + "。确认后才写入图纸。");
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
                    PluginSettings.ReadLineWidthMillimetres() != state.DisplayWidthMillimetres ||
                    panel.SelectedGroupId != state.GroupId ||
                    panel.LayerName != state.Layer ||
                    document.Database.Insunits != state.Units || document.Database.CurrentSpaceId != state.SpaceId)
                    throw new InvalidOperationException("参数、图纸单位或绘图空间已变化，请重新预览。");
                if (!LayerTools.ConfirmNew(document.Database, document.Editor, state.Layer))
                {
                    ClearPreview();
                    panel.ShowStatus("未创建图层；预览已取消，图纸未改变。");
                    return;
                }
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
                        LayerTools.Ensure(document.Database, transaction, state.Layer);
                        // The red color belongs only to the temporary preview.
                        result.SetDatabaseDefaults(document.Database);
                        result.ColorIndex = 256;
                        result.Layer = state.Layer;
                        Commands.SetRebarWidth(result, state.DisplayWidthMillimetres * Commands.MillimetresToDrawingUnits(document.Database));
                        BlockTableRecord space = (BlockTableRecord)transaction.GetObject(state.SpaceId, OpenMode.ForWrite);
                        space.AppendEntity(result);
                        transaction.AddNewlyCreatedDBObject(result, true);
                        DrawingData.AddLinkedRebar(document.Database, transaction, state.GroupId,
                            result, state.SourceId, state.Side,
                            DrawingData.GeometryHash(state.SourceSnapshot), cover, diameter);
                        transaction.Commit();
                    }
                }
                ClearPreview();
                panel.RefreshTree();
                panel.ShowStatus("钢筋已生成。可用 AutoCAD 撤销命令移除。");
                document.Editor.WriteMessage("\n钢筋已生成：统一显示宽度 {0:g} mm，中心线偏移 {1:g} mm。", state.DisplayWidthMillimetres, cover + diameter / 2.0);
            }
            catch (System.Exception ex)
            {
                ClearPreview();
                panel.ShowStatus("未生成：" + ex.Message);
                document.Editor.WriteMessage("\n未生成钢筋：{0}", ex.Message);
            }
        }

        internal static void CreateNode()
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null || panel == null) return;
            try
            {
                double cover, diameter;
                panel.ReadParameters(out cover, out diameter);
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    DrawingData.AddNode(doc.Database, tr, panel.NodeName, cover, diameter);
                    tr.Commit();
                }
                panel.RefreshTree();
                panel.ShowStatus("节点已保存到当前图纸。");
            }
            catch (System.Exception ex) { panel.ShowStatus("创建节点失败：" + ex.Message); doc.Editor.WriteMessage("\n创建节点失败：{0}", ex.Message); }
        }

        internal static void CreateGroup()
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null || panel == null) return;
            try
            {
                Guid nodeId = panel.SelectedNodeId;
                if (nodeId == Guid.Empty) throw new InvalidOperationException("请先在列表中选中节点或其钢筋组。");
                double cover, diameter;
                panel.ReadParameters(out cover, out diameter);
                Guid groupId;
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    groupId = DrawingData.AddGroup(doc.Database, tr, nodeId, panel.GroupName, cover, diameter);
                    tr.Commit();
                }
                panel.RefreshTree(groupId);
                panel.ShowStatus("钢筋组已保存到当前图纸；选中后可以预览钢筋。");
            }
            catch (System.Exception ex) { panel.ShowStatus("创建钢筋组失败：" + ex.Message); doc.Editor.WriteMessage("\n创建钢筋组失败：{0}", ex.Message); }
        }

        internal static void Locate()
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null || panel == null) return;
            try
            {
                var options = new PromptEntityOptions("\n选择已关联的钢筋: ");
                options.AddAllowedClass(typeof(Polyline), true);
                PromptEntityResult selected = doc.Editor.GetEntity(options);
                if (selected.Status != PromptStatus.OK) return;
                Guid found = Guid.Empty;
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    DrawingData.Index data = DrawingData.Read(doc.Database, tr);
                    DrawingData.Rebar match = data.Rebars.Find(r => r.EntityId == selected.ObjectId);
                    if (match == null) { panel.ShowStatus("这条多段线尚未纳入钢筋组。"); return; }
                    found = match.Id;
                }
                panel.RefreshTree(found);
                panel.ShowStatus("已定位到钢筋。列表中的“已删除”表示图形已被擦除，可尝试撤销。");
            }
            catch (System.Exception ex) { panel.ShowStatus("定位失败：" + ex.Message); doc.Editor.WriteMessage("\n定位失败：{0}", ex.Message); }
        }

        internal static void UpdateDefaults()
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null || panel == null) return;
            try
            {
                Guid id = panel.SelectedEditableId;
                if (id == Guid.Empty) throw new InvalidOperationException("请先选中节点或钢筋组。");
                double cover, diameter;
                panel.ReadParameters(out cover, out diameter);
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    DrawingData.UpdateDefaults(doc.Database, tr, id, panel.SelectedEditableName, cover, diameter);
                    tr.Commit();
                }
                panel.RefreshTree(id);
                panel.ShowStatus("默认参数已保存；已有钢筋的尺寸和图形保持原样。");
            }
            catch (System.Exception ex) { panel.ShowStatus("保存默认参数失败：" + ex.Message); doc.Editor.WriteMessage("\n保存默认参数失败：{0}", ex.Message); }
        }

        private static bool SameContour(Polyline a, Polyline b)
        {
            if (a.NumberOfVertices != b.NumberOfVertices || a.Closed != b.Closed ||
                a.Normal != b.Normal || a.Elevation != b.Elevation ||
                a.ConstantWidth != b.ConstantWidth) return false;
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
        private readonly TextBox lineDisplayWidth = new TextBox { Width = 110 };
        private readonly Label units = new Label { AutoSize = true };
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(245, 0), Text = "选择轮廓后显示临时预览。" };
        private readonly Button select = new Button { Text = "选择轮廓并预览", Width = 220, Height = 34 };
        private readonly Button confirm = new Button { Text = "确认生成", Width = 220, Height = 34, Enabled = false };
        private readonly Button cancel = new Button { Text = "取消预览", Width = 220, Height = 34, Enabled = false };
        private readonly TreeView hierarchy = new TreeView { Width = 250, Height = 180, HideSelection = false };
        private readonly TextBox nodeName = new TextBox { Text = "节点 1", Width = 190 };
        private readonly TextBox groupName = new TextBox { Text = "钢筋组 1", Width = 190 };
        private readonly Button addNode = new Button { Text = "新建节点", Width = 105 };
        private readonly Button addGroup = new Button { Text = "新建钢筋组", Width = 105 };
        private readonly Button locate = new Button { Text = "在列表定位钢筋", Width = 220 };
        private readonly Button refresh = new Button { Text = "刷新列表", Width = 220 };
        private readonly Button saveDefaults = new Button { Text = "保存所选节点/组的名称与默认尺寸", Width = 250 };
        private readonly Button saveLineDisplayWidth = new Button { Text = "保存插件统一线筋宽度", Width = 220 };
        private readonly ComboBox layer = new ComboBox { Width = 230, DropDownStyle = ComboBoxStyle.DropDown };
        private readonly Button editPreview = new Button { Text = "预览修改所选钢筋", Width = 220, Height = 34 };
        private readonly Button editConfirm = new Button { Text = "确认修改", Width = 220, Height = 34, Enabled = false };
        private readonly TextBox annotation = new TextBox { Text = "Φ12", Width = 190 };
        private readonly Button annotationSelect = new Button { Text = "预览直径标注", Width = 220, Height = 34 };
        private readonly Button annotationLayout = new Button { Text = "调整已有标注引线", Width = 220, Height = 34 };
        private readonly Button annotationConfirm = new Button { Text = "确认直径标注", Width = 220, Height = 34, Enabled = false };
        private bool populating;
        private Document layerDocument;
        private DrawingData.Index current = new DrawingData.Index();

        internal RebarPanel()
        {
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = System.Windows.Forms.FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(12) };
            layout.Controls.Add(new Label { Text = "当前图纸：节点 → 钢筋组 → 钢筋", AutoSize = true });
            layout.Controls.Add(hierarchy);
            layout.Controls.Add(refresh);
            layout.Controls.Add(locate);
            layout.Controls.Add(new Label { Text = "节点名称", AutoSize = true });
            layout.Controls.Add(nodeName);
            layout.Controls.Add(addNode);
            layout.Controls.Add(new Label { Text = "钢筋组名称（先选节点）", AutoSize = true });
            layout.Controls.Add(groupName);
            layout.Controls.Add(addGroup);
            layout.Controls.Add(new Label { Text = "混凝土保护层（mm）", AutoSize = true });
            layout.Controls.Add(cover);
            layout.Controls.Add(new Label { Text = "计算用钢筋直径（mm）", AutoSize = true, Margin = new Padding(3, 12, 3, 3) });
            layout.Controls.Add(diameter);
            layout.Controls.Add(new Label { Text = "插件统一线筋显示宽度（mm，所有图纸共用）", AutoSize = true, Margin = new Padding(3, 12, 3, 3) });
            layout.Controls.Add(lineDisplayWidth);
            layout.Controls.Add(saveLineDisplayWidth);
            layout.Controls.Add(units);
            layout.Controls.Add(new Label { Text = "目标钢筋图层（可选已有或输入新名称）", AutoSize = true });
            layout.Controls.Add(layer);
            layout.Controls.Add(saveDefaults);
            layout.Controls.Add(select);
            layout.Controls.Add(confirm);
            layout.Controls.Add(cancel);
            layout.Controls.Add(editPreview);
            layout.Controls.Add(editConfirm);
            layout.Controls.Add(new Label { Text = "钢筋直径标注文字（只改文字）", AutoSize = true });
            layout.Controls.Add(annotation);
            layout.Controls.Add(annotationSelect);
            layout.Controls.Add(annotationLayout);
            layout.Controls.Add(annotationConfirm);
            layout.Controls.Add(status);
            Controls.Add(layout);
            cover.TextChanged += ParametersChanged;
            diameter.TextChanged += ParametersChanged;
            lineDisplayWidth.TextChanged += LineDisplayWidthChanged;
            layer.TextChanged += ParametersChanged;
            annotation.TextChanged += ParametersChanged;
            select.Click += (sender, args) => RebarPalette.Queue("WDTSELECT");
            confirm.Click += (sender, args) => RebarPalette.Queue("WDTCONFIRM");
            cancel.Click += (sender, args) => { RebarPalette.ClearPreview(); ShowStatus("预览已取消，没有生成图形。"); };
            addNode.Click += (sender, args) => RebarPalette.Queue("WDTNODE");
            addGroup.Click += (sender, args) => RebarPalette.Queue("WDTGROUP");
            locate.Click += (sender, args) => RebarPalette.Queue("WDTLOCATE");
            refresh.Click += (sender, args) => RefreshTree();
            saveDefaults.Click += (sender, args) => RebarPalette.Queue("WDTUPDATE");
            saveLineDisplayWidth.Click += (sender, args) => SaveLineDisplayWidth();
            editPreview.Click += (sender, args) => RebarPalette.Queue("WDTEDITPREVIEW");
            editConfirm.Click += (sender, args) => RebarPalette.Queue("WDTEDITCONFIRM");
            annotationSelect.Click += (sender, args) => RebarPalette.Queue("WDTLABELPREVIEW");
            annotationLayout.Click += (sender, args) => RebarPalette.Queue("WDTLABELAYOUT");
            annotationConfirm.Click += (sender, args) => RebarPalette.Queue("WDTLABELCONFIRM");
            hierarchy.AfterSelect += (sender, args) => { if (!populating) ApplySelection(); };
        }

        private void ApplySelection()
        {
                RebarPalette.ClearPreview();
                Guid groupId = SelectedGroupId;
                DrawingData.Group group = current.Groups.Find(g => g.Id == groupId);
                DrawingData.Node node = current.Nodes.Find(n => n.Id == SelectedNodeId);
                DrawingData.Rebar selectedRebar = current.Rebars.Find(r => r.Id == SelectedRebarId);
                if (selectedRebar != null)
                {
                    DrawingData.Annotation savedLabel = current.Annotations.Find(a => a.RebarId == selectedRebar.Id);
                    string currentLabel = savedLabel == null
                        ? "Φ" + selectedRebar.Diameter.ToString("g", CultureInfo.CurrentCulture) : savedLabel.Label;
                    if (savedLabel != null && !savedLabel.TextId.IsNull && !savedLabel.TextId.IsErased)
                    {
                        Document labelDoc = CadApplication.DocumentManager.MdiActiveDocument;
                        using (Transaction labelRead = labelDoc.Database.TransactionManager.StartTransaction())
                        {
                            DBText actual = labelRead.GetObject(savedLabel.TextId, OpenMode.ForRead) as DBText;
                            if (actual != null) currentLabel = RebarPalette.DisplayLabel(actual.TextString);
                        }
                    }
                    annotation.Text = currentLabel;
                }
                if (group != null || node != null)
                {
                    if (node != null) nodeName.Text = node.Name;
                    if (group != null) groupName.Text = group.Name;
                    cover.Text = (selectedRebar == null ? group == null ? node.Cover : group.Cover : selectedRebar.Cover).ToString(CultureInfo.CurrentCulture);
                    diameter.Text = (selectedRebar == null ? group == null ? node.Diameter : group.Diameter : selectedRebar.Diameter).ToString(CultureInfo.CurrentCulture);
                }
                if (selectedRebar != null && !selectedRebar.EntityId.IsNull && !selectedRebar.EntityId.IsErased)
                {
                    Document doc = CadApplication.DocumentManager.MdiActiveDocument;
                    using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        Polyline steel = tr.GetObject(selectedRebar.EntityId, OpenMode.ForRead) as Polyline;
                        if (steel != null)
                        {
                            layer.Text = steel.Layer;
                            if (selectedRebar.Mode == "Free" || (selectedRebar.GeometryHash != "" && selectedRebar.GeometryHash != DrawingData.GeometryHash(steel)))
                                ShowStatus("这条钢筋的路径已在 CAD 中手工调整。修改保护层会预览替换后的形状。");
                            else if (RebarPalette.WidthChanged(steel, PluginSettings.ReadLineWidthMillimetres() * Commands.MillimetresToDrawingUnits(doc.Database)))
                                ShowStatus("这条钢筋的宽度与当前插件统一设置不同；只改图层会保留现有宽度，重生成将预览统一宽度。");
                            else ShowStatus("已选中钢筋。修改尺寸、图层或标注文字后先预览，再确认；改标注只改文字。");
                        }
                    }
                }
        }

        private void ParametersChanged(object sender, EventArgs args)
        {
            RebarPalette.ClearPreview();
            ShowStatus("尺寸或图层已变化；请重新预览后确认。");
        }

        private void LineDisplayWidthChanged(object sender, EventArgs args)
        {
            RebarPalette.ClearPreview();
            ShowStatus("统一显示宽度仅在保存到插件设置后用于新生成或重新生成的线筋。");
        }

        private void SaveLineDisplayWidth()
        {
            double width;
            if (!double.TryParse(lineDisplayWidth.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out width) ||
                !Commands.Finite(width) || width <= 0 || width > 1000)
            {
                ShowStatus("统一线筋宽度请输入大于0且不超过1000的毫米数。");
                return;
            }
            try
            {
                PluginSettings.SaveLineWidthMillimetres(width);
                ShowStatus("插件统一线筋宽度已保存，适用于当前Windows用户打开的所有图纸。");
            }
            catch (System.Exception ex) { ShowStatus("保存统一线筋宽度失败：" + ex.Message); }
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
            try { lineDisplayWidth.Text = PluginSettings.ReadLineWidthMillimetres().ToString("g", CultureInfo.CurrentCulture); }
            catch (System.Exception ex)
            {
                lineDisplayWidth.Text = PluginSettings.DefaultLineWidthMillimetres.ToString("g", CultureInfo.CurrentCulture);
                ShowStatus("统一宽度设置无法读取，请保存有效宽度后继续：" + ex.Message);
            }
            Document document = CadApplication.DocumentManager.MdiActiveDocument;
            select.Enabled = document != null;
            if (document == null) { units.Text = "请先打开图纸。"; SetPreviewAvailable(false); RefreshTree(); return; }
            UnitsValue drawingUnits = document.Database.Insunits;
            units.Text = drawingUnits == UnitsValue.Undefined ? "未设置图纸单位：按毫米处理" :
                "图纸单位：" + drawingUnits + "；输入仍为毫米";
            RefreshLayers();
            RefreshTree();
        }

        internal void RefreshLayers()
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null) { layerDocument = null; layer.Items.Clear(); layer.Text = ""; return; }
            string desired = ReferenceEquals(doc, layerDocument) ? layer.Text : "";
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                layer.Items.Clear();
                foreach (string name in LayerTools.Names(doc.Database, tr)) layer.Items.Add(name);
                if (string.IsNullOrWhiteSpace(desired))
                    desired = ((LayerTableRecord)tr.GetObject(doc.Database.Clayer, OpenMode.ForRead)).Name;
            }
            layerDocument = doc;
            layer.Text = desired;
        }

        internal string LayerName { get { return layer.Text; } }
        internal string AnnotationLabel { get { return annotation.Text; } }
        internal Guid SelectedRebarId
        {
            get
            {
                Guid id = hierarchy.SelectedNode == null ? Guid.Empty : (Guid)hierarchy.SelectedNode.Tag;
                return current.Rebars.Exists(r => r.Id == id) ? id : Guid.Empty;
            }
        }

        internal string NodeName { get { return nodeName.Text; } }
        internal string GroupName { get { return groupName.Text; } }
        internal Guid SelectedEditableId
        {
            get
            {
                Guid id = hierarchy.SelectedNode == null ? Guid.Empty : (Guid)hierarchy.SelectedNode.Tag;
                return current.Nodes.Exists(n => n.Id == id) || current.Groups.Exists(g => g.Id == id) ? id : Guid.Empty;
            }
        }
        internal string SelectedEditableName
        {
            get { return current.Nodes.Exists(n => n.Id == SelectedEditableId) ? nodeName.Text : groupName.Text; }
        }
        internal Guid SelectedGroupId
        {
            get
            {
                Guid id = hierarchy.SelectedNode == null ? Guid.Empty : (Guid)hierarchy.SelectedNode.Tag;
                DrawingData.Group group = current.Groups.Find(g => g.Id == id);
                if (group != null) return group.Id;
                DrawingData.Rebar rebar = current.Rebars.Find(r => r.Id == id);
                return rebar == null ? Guid.Empty : rebar.GroupId;
            }
        }
        internal Guid SelectedNodeId
        {
            get
            {
                Guid id = hierarchy.SelectedNode == null ? Guid.Empty : (Guid)hierarchy.SelectedNode.Tag;
                if (current.Nodes.Exists(n => n.Id == id)) return id;
                DrawingData.Group group = current.Groups.Find(g => g.Id == id);
                if (group != null) return group.NodeId;
                DrawingData.Rebar rebar = current.Rebars.Find(r => r.Id == id);
                group = rebar == null ? null : current.Groups.Find(g => g.Id == rebar.GroupId);
                return group == null ? Guid.Empty : group.NodeId;
            }
        }
        internal void RefreshTree(Guid prefer = default(Guid))
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            Guid selected = prefer != Guid.Empty ? prefer :
                hierarchy.SelectedNode == null ? Guid.Empty : (Guid)hierarchy.SelectedNode.Tag;
            populating = true;
            try
            {
                hierarchy.Nodes.Clear();
                current = new DrawingData.Index();
                if (doc == null)
                {
                    addNode.Enabled = addGroup.Enabled = locate.Enabled = saveDefaults.Enabled = select.Enabled = annotationSelect.Enabled = annotationLayout.Enabled = false;
                    return;
                }
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    current = DrawingData.Read(doc.Database, tr);
                    TreeNode wanted = null;
                    foreach (DrawingData.Node node in current.Nodes)
                    {
                        TreeNode root = hierarchy.Nodes.Add(node.Name);
                        root.Tag = node.Id;
                        if (node.Id == selected) wanted = root;
                        foreach (DrawingData.Group group in current.Groups.FindAll(g => g.NodeId == node.Id))
                        {
                            TreeNode branch = root.Nodes.Add(group.Name);
                            branch.Tag = group.Id;
                            if (group.Id == selected) wanted = branch;
                            foreach (DrawingData.Rebar rebar in current.Rebars.FindAll(r => r.GroupId == group.Id))
                            {
                                bool erased = rebar.EntityId.IsNull || rebar.EntityId.IsErased;
                                bool manual = rebar.Mode == "Free";
                                if (!erased && !string.IsNullOrEmpty(rebar.GeometryHash))
                                {
                                    Polyline steel = tr.GetObject(rebar.EntityId, OpenMode.ForRead) as Polyline;
                                    manual = manual || (steel != null && rebar.GeometryHash != DrawingData.GeometryHash(steel));
                                }
                                DrawingData.Annotation mark = current.Annotations.Find(a => a.RebarId == rebar.Id);
                                string markText = mark == null ? null : mark.Label;
                                bool annotationChanged = false;
                                bool annotationLost = mark != null && (mark.TextId.IsNull || mark.TextId.IsErased ||
                                    mark.LeaderId.IsNull || mark.LeaderId.IsErased);
                                if (mark != null && !mark.TextId.IsNull && !mark.TextId.IsErased)
                                {
                                    DBText actual = tr.GetObject(mark.TextId, OpenMode.ForRead) as DBText;
                                    if (actual != null)
                                    {
                                        markText = RebarPalette.DisplayLabel(actual.TextString);
                                        annotationChanged = markText != mark.Label;
                                    }
                                }
                                TreeNode leaf = branch.Nodes.Add((erased ? "已删除 " : manual ? "手工调整 " : "钢筋 ") + rebar.Id.ToString("N").Substring(0, 8)
                                    + (mark == null ? "  Φ" + rebar.Diameter.ToString("g", CultureInfo.CurrentCulture) : "  " + markText)
                                    + (annotationChanged ? "（文字已手改）" : "")
                                    + (annotationLost ? "（标注已删除）" : "")
                                    + (erased && mark != null ? "（标注待处理）" : ""));
                                leaf.Tag = rebar.Id;
                                if (rebar.Id == selected) wanted = leaf;
                            }
                        }
                        root.Expand();
                    }
                    if (wanted != null) { wanted.EnsureVisible(); hierarchy.SelectedNode = wanted; }
                }
                addNode.Enabled = addGroup.Enabled = locate.Enabled = saveDefaults.Enabled = select.Enabled = editPreview.Enabled = annotationSelect.Enabled = annotationLayout.Enabled = true;
            }
            catch (System.Exception ex)
            {
                addNode.Enabled = addGroup.Enabled = locate.Enabled = saveDefaults.Enabled = select.Enabled = editPreview.Enabled = annotationSelect.Enabled = annotationLayout.Enabled = false;
                ShowStatus("图纸关联数据无法读取：" + ex.Message);
            }
            finally { populating = false; }
            if (hierarchy.SelectedNode != null) ApplySelection();
        }

        internal void SetPreviewAvailable(bool value) { confirm.Enabled = value; cancel.Enabled = value; }
        internal void SetEditPreviewAvailable(bool value) { editConfirm.Enabled = value; cancel.Enabled = value || confirm.Enabled; }
        internal void SetAnnotationPreviewAvailable(bool value) { annotationConfirm.Enabled = value; cancel.Enabled = value || confirm.Enabled || editConfirm.Enabled; }
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

        [CommandMethod("WDTNODE", CommandFlags.Modal)]
        public void CreateDetailNode() { RebarPalette.CreateNode(); }

        [CommandMethod("WDTGROUP", CommandFlags.Modal)]
        public void CreateRebarGroup() { RebarPalette.CreateGroup(); }

        [CommandMethod("WDTLOCATE", CommandFlags.Modal)]
        public void LocateRebar() { RebarPalette.Locate(); }

        [CommandMethod("WDTUPDATE", CommandFlags.Modal)]
        public void UpdateGroupDefaults() { RebarPalette.UpdateDefaults(); }

        [CommandMethod("WDTEDITPREVIEW", CommandFlags.Modal)]
        public void PreviewRebarEdit() { RebarPalette.PreviewEdit(); }

        [CommandMethod("WDTEDITCONFIRM", CommandFlags.Modal)]
        public void ConfirmRebarEdit() { RebarPalette.ConfirmEdit(); }
    }
}
