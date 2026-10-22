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
                toolbar.Size=new Size(290,95);
                var strip=new ToolStrip { Dock=DockStyle.Fill,GripStyle=ToolStripGripStyle.Hidden };
                strip.Items.Add(new ToolStripButton("反坎绘制",null,(s,e)=>RebarPalette.Queue("WDTPANEL")));
                strip.Items.Add(new ToolStripButton("高级设置",null,(s,e)=>RebarPalette.Queue("WDTUPSETTINGS")));
                toolbar.Add("工具",strip);
            }
            toolbar.Visible=true;
        }
    }

    internal sealed class UpstandSettingsForm : Form
    {
        private readonly TextBox[] numbers=new TextBox[4];
        private readonly TextBox[] layers=new TextBox[4];
        private readonly UpstandPanelValues values;
        private readonly UpstandLayers names;
        internal UpstandSettingsForm()
        {
            values=UpstandPanelValues.ReadUserDefaults(); names=UpstandLayers.Read();
            Text="WallDetailTool 高级设置"; Width=640; Height=355;
            FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false;
            StartPosition=FormStartPosition.CenterParent;
            var columns=new TableLayoutPanel { Dock=DockStyle.Top,ColumnCount=2,RowCount=1,Height=250 };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            var left=Section("参数（mm）",new[]{"线筋显示宽度","点筋显示直径","圆弧中心线半径 R","钢筋文字高度"},numbers);
            var right=Section("新生成图元的图层",new[]{"线筋图层","点筋图层","文字/引线图层","尺寸图层"},layers);
            columns.Controls.Add(left,0,0); columns.Controls.Add(right,1,0);
            numbers[0].Text=values.LineDisplayWidth.ToString("g",CultureInfo.CurrentCulture);
            numbers[1].Text=values.DotDisplayDiameter.ToString("g",CultureInfo.CurrentCulture);
            numbers[2].Text=values.Radius.ToString("g",CultureInfo.CurrentCulture);
            numbers[3].Text=values.TextHeight.ToString("g",CultureInfo.CurrentCulture);
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
            var grid=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=4 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,53));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,47));
            for(int i=0;i<4;i++)
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
                var parsed=new double[4];
                for(int i=0;i<4;i++)
                    if (!double.TryParse(numbers[i].Text,NumberStyles.Float,CultureInfo.CurrentCulture,out parsed[i]) ||
                        double.IsNaN(parsed[i]) || double.IsInfinity(parsed[i]) || parsed[i]<=0)
                        throw new InvalidOperationException("绘图参数必须为有效正数。");
                if (parsed[0]>1000 || parsed[1]>1000) throw new InvalidOperationException("CAD 显示尺寸不得超过 1000 mm。");
                var nextNames=new UpstandLayers { Line=layers[0].Text.Trim(),Dot=layers[1].Text.Trim(),
                    Text=layers[2].Text.Trim(),Dimension=layers[3].Text.Trim() };
                nextNames.Validate();
                values.LineDisplayWidth=parsed[0]; values.DotDisplayDiameter=parsed[1];
                values.Radius=parsed[2]; values.TextHeight=parsed[3];
                values.SaveUserDefaults(); nextNames.Save();
                RebarPalette.UpdateUpstandAdvanced(values);
                DialogResult=DialogResult.OK; Close();
            }
            catch (System.Exception ex) { MessageBox.Show(this,ex.Message,"设置未保存",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        }
    }

    public partial class Commands
    {
        [CommandMethod("WDTTOOLBAR",CommandFlags.Session)]
        public void ShowUpstandToolbar() { UpstandToolbar.Open(); }

        [CommandMethod("WDTUPSETTINGS",CommandFlags.Session)]
        public void ShowUpstandSettings()
        {
            using (var dialog=new UpstandSettingsForm()) CadApplication.ShowModalDialog(dialog);
        }
    }
}
