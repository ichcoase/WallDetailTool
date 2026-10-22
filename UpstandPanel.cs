using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace WallDetailTool
{
    internal sealed class UpstandPanelValues
    {
        internal double Cover=25, LineDiameter=8, LineSpacing=150, DotDiameter=6, DotSpacing=200;
        internal double LeftAnchor, RightAnchor, Radius=20, TextHeight=62.5;
        internal double LineDisplayWidth=PluginSettings.DefaultLineWidthMillimetres;
        internal double DotDisplayDiameter=PluginSettings.DefaultDotDiameterMillimetres;

        internal static UpstandPanelValues ReadUserDefaults()
        {
            var result=new UpstandPanelValues();
            result.LineDisplayWidth=PluginSettings.ReadLineWidthMillimetres();
            result.DotDisplayDiameter=PluginSettings.ReadDotDiameterMillimetres();
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WallDetailTool","upstand-defaults.ini");
            if (!File.Exists(path)) return result;
            string[] parts=File.ReadAllText(path,Encoding.UTF8).Split('|');
            if (parts.Length!=9) return result;
            double[] numbers=new double[9];
            for (int i=0;i<parts.Length;i++)
                if (!double.TryParse(parts[i],NumberStyles.Float,CultureInfo.InvariantCulture,out numbers[i])) return result;
            result.Cover=numbers[0]; result.LineDiameter=numbers[1]; result.LineSpacing=numbers[2];
            result.DotDiameter=numbers[3]; result.DotSpacing=numbers[4]; result.LeftAnchor=numbers[5];
            result.RightAnchor=numbers[6]; result.Radius=numbers[7]; result.TextHeight=numbers[8];
            return result;
        }

        internal void SaveUserDefaults()
        {
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WallDetailTool","upstand-defaults.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            double[] values={Cover,LineDiameter,LineSpacing,DotDiameter,DotSpacing,LeftAnchor,RightAnchor,Radius,TextHeight};
            File.WriteAllText(path,string.Join("|",Array.ConvertAll(values,x=>x.ToString("R",CultureInfo.InvariantCulture))),
                new UTF8Encoding(false));
            PluginSettings.SaveLineWidthMillimetres(LineDisplayWidth);
            PluginSettings.SaveDotDiameterMillimetres(DotDisplayDiameter);
        }
    }

    internal sealed class UpstandPanelView : UserControl
    {
        private readonly TextBox[] fields=new TextBox[11];
        private readonly Label status=new Label { AutoSize=true,MaximumSize=new Size(265,0) };
        private static readonly string[] labels={"保护层 (mm)","线筋实际直径 (mm)","线筋间距 (mm)",
            "点筋实际直径 (mm)","点筋间距 (mm)","左侧锚固长度 (mm，须手动输入)",
            "右侧锚固长度 (mm，可同左侧)","线筋 CAD 显示宽度 (mm)","点筋 CAD 显示直径 (mm)",
            "顶部中心线半径 R (mm)","钢筋文字高度 (mm)"};
        internal UpstandPanelView()
        {
            var all=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,
                WrapContents=false,AutoScroll=true,Padding=new Padding(12) };
            all.Controls.Add(new Label { Text="梁上反坎 · 1 CAD 单位 = 1 mm",AutoSize=true });
            for (int i=0;i<7;i++) AddField(all,i);
            for (int i=7;i<fields.Length;i++) fields[i]=new TextBox();
            var generate=new Button { Text="按这些参数选择轮廓并生成",Width=245,Height=34 };
            generate.Click+=(s,e)=>{ try { ReadValues(); RebarPalette.Queue("WDTUPPANELDRAW"); }
                catch (System.Exception ex) { status.Text=ex.Message; } };
            all.Controls.Add(generate);
            var save=new Button { Text="保存常用参数为用户默认",Width=245 };
            save.Click+=(s,e)=>{ try { var current=ReadValues(); var stored=UpstandPanelValues.ReadUserDefaults();
                CopyCommon(current,stored); stored.SaveUserDefaults();
                status.Text="常用参数已保存；当前图纸的上次确认参数只在生成后更新。"; }
                catch (System.Exception ex) { status.Text="保存失败："+ex.Message; } };
            all.Controls.Add(save);
            var reset=new Button { Text="恢复常用参数默认值",Width=245 };
            reset.Click+=(s,e)=>{ try { var defaults=new UpstandPanelValues(); var stored=UpstandPanelValues.ReadUserDefaults();
                CopyCommon(defaults,stored); stored.SaveUserDefaults(); SetValues(stored);
                status.Text="常用参数已恢复默认值；锚固长度仍须手动输入。"; }
                catch (System.Exception ex) { status.Text="恢复默认值失败："+ex.Message; } };
            all.Controls.Add(reset); all.Controls.Add(status); Controls.Add(all);
            SetValues(UpstandPanelValues.ReadUserDefaults());
        }
        private void AddField(Control parent,int i)
        {
            parent.Controls.Add(new Label { Text=labels[i],AutoSize=true });
            fields[i]=new TextBox { Width=190 };
            parent.Controls.Add(fields[i]);
        }
        private static string Display(double value) { return value<=0 ? "" : value.ToString("g",CultureInfo.CurrentCulture); }
        private static void CopyCommon(UpstandPanelValues source,UpstandPanelValues target)
        {
            target.Cover=source.Cover; target.LineDiameter=source.LineDiameter;
            target.LineSpacing=source.LineSpacing; target.DotDiameter=source.DotDiameter;
            target.DotSpacing=source.DotSpacing; target.LeftAnchor=source.LeftAnchor;
            target.RightAnchor=source.RightAnchor;
        }
        internal void SetValues(UpstandPanelValues v)
        {
            double[] a={v.Cover,v.LineDiameter,v.LineSpacing,v.DotDiameter,v.DotSpacing,v.LeftAnchor,
                v.RightAnchor,v.LineDisplayWidth,v.DotDisplayDiameter,v.Radius,v.TextHeight};
            for (int i=0;i<a.Length;i++) fields[i].Text=i==0 ? a[i].ToString("g",CultureInfo.CurrentCulture) : Display(a[i]);
        }
        internal void UpdateAdvanced(UpstandPanelValues v)
        {
            fields[7].Text=Display(v.LineDisplayWidth);
            fields[8].Text=Display(v.DotDisplayDiameter);
            fields[9].Text=Display(v.Radius);
            fields[10].Text=Display(v.TextHeight);
        }
        internal UpstandPanelValues ReadValues()
        {
            double[] a=new double[fields.Length];
            for (int i=0;i<a.Length;i++)
            {
                if (i==6 && string.IsNullOrWhiteSpace(fields[i].Text)) { a[i]=a[5]; continue; }
                if (!double.TryParse(fields[i].Text,NumberStyles.Float,CultureInfo.CurrentCulture,out a[i]) ||
                    double.IsNaN(a[i]) || double.IsInfinity(a[i]) || a[i]<(i==0?0:1e-12))
                    throw new InvalidOperationException(labels[i]+"请输入有效毫米值；锚固长度必须由您确认。");
            }
            if (a[7]>1000 || a[8]>1000) throw new InvalidOperationException("CAD 显示宽度和直径不得超过 1000 mm。");
            return new UpstandPanelValues { Cover=a[0],LineDiameter=a[1],LineSpacing=a[2],DotDiameter=a[3],
                DotSpacing=a[4],LeftAnchor=a[5],RightAnchor=a[6],LineDisplayWidth=a[7],
                DotDisplayDiameter=a[8],Radius=a[9],TextHeight=a[10] };
        }
    }

    public partial class Commands
    {
        [CommandMethod("WDTUPPANELDRAW",CommandFlags.Modal)]
        public void DrawUpstandFromPanel()
        {
            Document doc=CadApplication.DocumentManager.MdiActiveDocument;
            if (doc==null) return;
            try { DrawUpstandCore(RebarPalette.ReadUpstandPanel()); }
            catch (System.Exception ex) { doc.Editor.WriteMessage("\n反坎面板参数无效："+ex.Message); }
        }
    }
}
