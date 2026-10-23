using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace WallDetailTool
{
    internal sealed class UpstandLayers
    {
        internal string Line="WDT-REBAR-LINE", Dot="WDT-REBAR-DOT", Text="S-TEXT", Dimension="s-floor_de_dim";
        private static string PathName { get { return Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),"WallDetailTool","upstand-layers.ini"); } }
        internal static UpstandLayers Read()
        {
            var value=new UpstandLayers();
            if (!File.Exists(PathName)) return value;
            string[] parts=File.ReadAllText(PathName,Encoding.UTF8).Split('|');
            if (parts.Length!=4) return value;
            value.Line=parts[0]; value.Dot=parts[1]; value.Text=parts[2]; value.Dimension=parts[3];
            value.Validate(); return value;
        }
        internal void Validate()
        {
            foreach (string name in new[]{Line,Dot,Text,Dimension})
                if (String.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[]{'<','>','/','\\','"',':',';','?','*','|',','})>=0)
                    throw new InvalidOperationException("图层名称无效："+name);
        }
        internal void Save()
        {
            Validate(); Directory.CreateDirectory(Path.GetDirectoryName(PathName));
            File.WriteAllText(PathName,String.Join("|",new[]{Line,Dot,Text,Dimension}),new UTF8Encoding(false));
        }
    }

    internal static class UpstandToolbar
    {
        private static PaletteSet toolbar;
        internal static void Open()
        {
            if (toolbar==null)
            {
                toolbar=new PaletteSet("WallDetailTool 工具条",new Guid("7EA33DAD-166B-4058-B37D-56120EE25037"));
                toolbar.Size=new Size(330,95);
                var strip=new ToolStrip { Dock=DockStyle.Fill,GripStyle=ToolStripGripStyle.Hidden };
                strip.Items.Add(new ToolStripButton("反坎绘制",null,(s,e)=>RebarPalette.Queue("WDTUPFORM")));
                strip.Items.Add(new ToolStripButton("板上反坎",null,(s,e)=>RebarPalette.Queue("WDTUPSLABDRAW")));
                strip.Items.Add(new ToolStripButton("高级设置",null,(s,e)=>RebarPalette.Queue("WDTUPSETTINGS")));
                toolbar.Add("工具",strip);
            }
            toolbar.Visible=true;
        }
    }

    internal sealed class UpstandDrawForm : Form
    {
        private readonly TextBox[] fields=new TextBox[6];
        private readonly UpstandPanelValues initial;
        internal UpstandPanelValues Values { get; private set; }
        internal UpstandDrawForm(UpstandPanelValues current)
        {
            initial=current;
            Text="WallDetailTool 反坎绘制"; Width=430; Height=360;
            FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false;
            StartPosition=FormStartPosition.CenterParent;
            var grid=new TableLayoutPanel { Dock=DockStyle.Top,ColumnCount=2,RowCount=6,Height=260,Padding=new Padding(14) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,57));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,43));
            string[] names={"线筋实际直径 (mm)","线筋间距 (mm)",
                "点筋实际直径 (mm)","点筋间距 (mm)","左侧锚固长度 (mm)",
                "右侧锚固长度 (mm，留空同左侧)"};
            double[] values={current.LineDiameter,current.LineSpacing,current.DotDiameter,
                current.DotSpacing,current.LeftAnchor,current.RightAnchor};
            for (int i=0;i<fields.Length;i++)
            {
                grid.Controls.Add(new Label { Text=names[i],AutoSize=true,Anchor=AnchorStyles.Left },0,i);
                fields[i]=new TextBox { Dock=DockStyle.Fill,Text=values[i]>0 ?
                    values[i].ToString("g",CultureInfo.CurrentCulture) : "" };
                grid.Controls.Add(fields[i],1,i);
            }
            var buttons=new FlowLayoutPanel { Dock=DockStyle.Bottom,FlowDirection=FlowDirection.RightToLeft,Height=43 };
            var cancel=new Button { Text="取消",Width=90,DialogResult=DialogResult.Cancel };
            var ok=new Button { Text="确定",Width=90 };
            ok.Click+=(s,e)=>AcceptValues(); buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
            CancelButton=cancel; Controls.Add(grid); Controls.Add(buttons);
        }
        private void AcceptValues()
        {
            try
            {
                var parsed=new double[6];
                for(int i=0;i<parsed.Length;i++)
                {
                    if (i==5 && String.IsNullOrWhiteSpace(fields[i].Text)) { parsed[i]=parsed[4]; continue; }
                    if (!double.TryParse(fields[i].Text,NumberStyles.Float,CultureInfo.CurrentCulture,out parsed[i]) ||
                        double.IsNaN(parsed[i]) || double.IsInfinity(parsed[i]) || parsed[i]<=0)
                        throw new InvalidOperationException("请填写有效毫米参数；锚固长度须手动确认。");
                }
                Values=new UpstandPanelValues { Cover=initial.Cover,LineDiameter=parsed[0],LineSpacing=parsed[1],
                    DotDiameter=parsed[2],DotSpacing=parsed[3],LeftAnchor=parsed[4],RightAnchor=parsed[5],
                    LineDisplayWidth=initial.LineDisplayWidth,DotDisplayDiameter=initial.DotDisplayDiameter,
                    Radius=initial.Radius,TextHeight=initial.TextHeight };
                DialogResult=DialogResult.OK; Close();
            }
            catch (System.Exception ex) { MessageBox.Show(this,ex.Message,"参数无效",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        }
    }

    internal sealed class UpstandSettingsForm : Form
    {
        private readonly TextBox[] numbers=new TextBox[5];
        private readonly TextBox[] layers=new TextBox[4];
        private readonly UpstandPanelValues values;
        private readonly UpstandLayers names;
        internal UpstandSettingsForm()
        {
            values=UpstandPanelValues.ReadUserDefaults(); names=UpstandLayers.Read();
            Text="WallDetailTool 高级设置"; Width=640; Height=390;
            FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false;
            StartPosition=FormStartPosition.CenterParent;
            var columns=new TableLayoutPanel { Dock=DockStyle.Top,ColumnCount=2,RowCount=1,Height=285 };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            var left=Section("参数（mm）",new[]{"保护层 c","线筋显示宽度","点筋显示直径","圆弧中心线半径 R","钢筋文字高度"},numbers);
            var right=Section("新生成图元的图层",new[]{"线筋图层","点筋图层","文字/引线图层","尺寸图层"},layers);
            columns.Controls.Add(left,0,0); columns.Controls.Add(right,1,0);
            numbers[0].Text=values.Cover.ToString("g",CultureInfo.CurrentCulture);
            numbers[1].Text=values.LineDisplayWidth.ToString("g",CultureInfo.CurrentCulture);
            numbers[2].Text=values.DotDisplayDiameter.ToString("g",CultureInfo.CurrentCulture);
            numbers[3].Text=values.Radius.ToString("g",CultureInfo.CurrentCulture);
            numbers[4].Text=values.TextHeight.ToString("g",CultureInfo.CurrentCulture);
            layers[0].Text=names.Line; layers[1].Text=names.Dot;
            layers[2].Text=names.Text; layers[3].Text=names.Dimension;
            var buttons=new FlowLayoutPanel { Dock=DockStyle.Bottom,FlowDirection=FlowDirection.RightToLeft,Height=42 };
            var cancel=new Button { Text="取消",Width=90,DialogResult=DialogResult.Cancel };
            var ok=new Button { Text="确定",Width=90 };
            ok.Click+=(s,e)=>Save(); buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
            CancelButton=cancel; Controls.Add(columns); Controls.Add(buttons);
        }
        private static Control Section(string title,string[] captions,TextBox[] fields)
        {
            var box=new GroupBox { Text=title,Dock=DockStyle.Fill };
            var grid=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=fields.Length };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,53));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,47));
            for(int i=0;i<fields.Length;i++)
            {
                grid.Controls.Add(new Label { Text=captions[i],AutoSize=true,Anchor=AnchorStyles.Left },0,i);
                fields[i]=new TextBox { Dock=DockStyle.Fill };
                grid.Controls.Add(fields[i],1,i);
            }
            box.Controls.Add(grid); return box;
        }
        private void Save()
        {
            try
            {
                var parsed=new double[5];
                for(int i=0;i<5;i++)
                    if (!double.TryParse(numbers[i].Text,NumberStyles.Float,CultureInfo.CurrentCulture,out parsed[i]) ||
                        double.IsNaN(parsed[i]) || double.IsInfinity(parsed[i]) || parsed[i]<(i==0?0:1e-12))
                        throw new InvalidOperationException("绘图参数必须为有效正数。");
                if (parsed[1]>1000 || parsed[2]>1000) throw new InvalidOperationException("CAD 显示尺寸不得超过 1000 mm。");
                var nextNames=new UpstandLayers { Line=layers[0].Text.Trim(),Dot=layers[1].Text.Trim(),
                    Text=layers[2].Text.Trim(),Dimension=layers[3].Text.Trim() };
                nextNames.Validate();
                values.Cover=parsed[0]; values.LineDisplayWidth=parsed[1]; values.DotDisplayDiameter=parsed[2];
                values.Radius=parsed[3]; values.TextHeight=parsed[4];
                values.SaveUserDefaults(); nextNames.Save();
                RebarPalette.UpdateUpstandAdvanced(values);
                DialogResult=DialogResult.OK; Close();
            }
            catch (System.Exception ex) { MessageBox.Show(this,ex.Message,"设置未保存",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        }
    }

    public partial class Commands
    {
        private static UpstandPanelValues pendingDrawValues;
        private static Document pendingDrawDocument;

        [CommandMethod("WDTUPFORM",CommandFlags.Session)]
        public void ShowUpstandDrawForm()
        {
            Document doc=CadApplication.DocumentManager.MdiActiveDocument;
            if (doc==null) return;
            pendingDrawValues=null; pendingDrawDocument=null;
            UpstandPanelValues initial=PanelValues(doc.Database);
            initial.Cover=UpstandPanelValues.ReadUserDefaults().Cover;
            using (var dialog=new UpstandDrawForm(initial))
                if (CadApplication.ShowModalDialog(dialog)==DialogResult.OK)
                {
                    pendingDrawValues=dialog.Values; pendingDrawDocument=doc;
                    doc.SendStringToExecute("WDTUPFORMDRAW ",true,false,false);
                }
        }

        [CommandMethod("WDTUPFORMDRAW",CommandFlags.Modal)]
        public void DrawUpstandFromForm()
        {
            Document doc=CadApplication.DocumentManager.MdiActiveDocument;
            UpstandPanelValues values=pendingDrawValues;
            Document target=pendingDrawDocument;
            pendingDrawValues=null; pendingDrawDocument=null;
            if (values==null || !ReferenceEquals(doc,target)) return;
            DrawUpstandCore(values);
        }

        [CommandMethod("WDTTOOLBAR",CommandFlags.Session)]
        public void ShowUpstandToolbar() { UpstandToolbar.Open(); }

        [CommandMethod("WDTUPSETTINGS",CommandFlags.Session)]
        public void ShowUpstandSettings()
        {
            using (var dialog=new UpstandSettingsForm()) CadApplication.ShowModalDialog(dialog);
        }
    }
}
