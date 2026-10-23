using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using CadApplication=Autodesk.AutoCAD.ApplicationServices.Application;

namespace WallDetailTool
{
    internal sealed class SlabDrawValues
    {
        internal double LineDiameter,LineSpacing,DotDiameter,DotSpacing,LocalThickness,H,LeftLength,RightLength;
    }

    internal sealed class SlabDrawForm : Form
    {
        private readonly TextBox[] fields=new TextBox[8];
        internal SlabDrawValues Values { get; private set; }
        internal SlabDrawForm(SlabDrawValues initial)
        {
            Text="WallDetailTool 板上反坎绘制"; Width=470; Height=440;
            FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false;
            StartPosition=FormStartPosition.CenterParent;
            var grid=new TableLayoutPanel { Dock=DockStyle.Top,ColumnCount=2,RowCount=8,Height=340,Padding=new Padding(14) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,62));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));
            string[] labels={"线筋实际直径 (mm)","线筋间距 (mm)","点筋实际直径 (mm)","点筋间距 (mm)",
                "局部轮廓板厚 h (mm)","板底至水平筋 H (mm)","左水平直段 L左 (mm)","右水平直段 L右 (mm，留空同左)"};
            double[] numbers={initial.LineDiameter,initial.LineSpacing,initial.DotDiameter,initial.DotSpacing,
                initial.LocalThickness,initial.H,initial.LeftLength,initial.RightLength};
            for(int i=0;i<fields.Length;i++)
            {
                grid.Controls.Add(new Label { Text=labels[i],AutoSize=true,Anchor=AnchorStyles.Left },0,i);
                fields[i]=new TextBox { Dock=DockStyle.Fill,Text=numbers[i]>0?
                    numbers[i].ToString("g",CultureInfo.CurrentCulture):"" };
                if (i==7 && Math.Abs(initial.RightLength-initial.LeftLength)<1e-9) fields[i].Text="";
                grid.Controls.Add(fields[i],1,i);
            }
            Controls.Add(new Label { Dock=DockStyle.Top,Height=26,Padding=new Padding(14,2,0,0),
                Text="完整截面的板厚从轮廓识别；局部轮廓使用此处 h。" });
            Controls.Add(grid);
            var buttons=new FlowLayoutPanel { Dock=DockStyle.Bottom,FlowDirection=FlowDirection.RightToLeft,Height=43 };
            var cancel=new Button { Text="取消",Width=90,DialogResult=DialogResult.Cancel };
            var ok=new Button { Text="确定",Width=90 };
            ok.Click+=(s,e)=>AcceptValues(); buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
            CancelButton=cancel; Controls.Add(buttons);
        }
        private void AcceptValues()
        {
            try
            {
                double[] v=new double[8];
                for(int i=0;i<v.Length;i++)
                {
                    if (i==7 && String.IsNullOrWhiteSpace(fields[i].Text)) { v[i]=v[6]; continue; }
                    if (i==4 && String.IsNullOrWhiteSpace(fields[i].Text)) { v[i]=0; continue; }
                    if (!double.TryParse(fields[i].Text,NumberStyles.Float,CultureInfo.CurrentCulture,out v[i]) ||
                        double.IsNaN(v[i]) || double.IsInfinity(v[i]) || v[i]<=0)
                        throw new InvalidOperationException("请填写有效的正数毫米参数；局部轮廓还须填写板厚 h。");
                }
                Values=new SlabDrawValues { LineDiameter=v[0],LineSpacing=v[1],DotDiameter=v[2],DotSpacing=v[3],
                    LocalThickness=v[4],H=v[5],LeftLength=v[6],RightLength=v[7] };
                DialogResult=DialogResult.OK; Close();
            }
            catch (System.Exception ex) { MessageBox.Show(this,ex.Message,"参数无效",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        }
    }

    public partial class Commands
    {
        private static SlabDrawValues pendingSlabValues;
        private static Document pendingSlabDocument;

        [CommandMethod("WDTUPSLABDRAW",CommandFlags.Session)]
        public void ShowSlabDrawForm()
        {
            Document doc=CadApplication.DocumentManager.MdiActiveDocument;
            if (doc==null) return;
            pendingSlabValues=null; pendingSlabDocument=null;
            UpstandPanelValues saved=UpstandPanelValues.ReadUserDefaults();
            var p=new SlabUpstandRebarParameters { Cover=saved.Cover,LineDisplayWidth=saved.LineDisplayWidth };
            double localThickness=0;
            ReadSlabLast(doc.Database,p,out localThickness);
            var initial=new SlabDrawValues { LineDiameter=saved.LineDiameter,LineSpacing=saved.LineSpacing,
                DotDiameter=saved.DotDiameter,DotSpacing=saved.DotSpacing,LocalThickness=localThickness,
                H=p.EffectiveH,LeftLength=p.LeftLength,RightLength=p.RightLength };
            using (var dialog=new SlabDrawForm(initial))
                if (CadApplication.ShowModalDialog(dialog)==DialogResult.OK)
                {
                    pendingSlabValues=dialog.Values; pendingSlabDocument=doc;
                    doc.SendStringToExecute("WDTUPSLABFORMDRAW ",true,false,false);
                }
        }

        [CommandMethod("WDTUPSLABFORMDRAW",CommandFlags.Modal)]
        public void DrawSlabFromForm()
        {
            Document doc=CadApplication.DocumentManager.MdiActiveDocument;
            SlabDrawValues values=pendingSlabValues; Document target=pendingSlabDocument;
            pendingSlabValues=null; pendingSlabDocument=null;
            if (values==null || !ReferenceEquals(doc,target)) return;
            DrawSlabCore(values);
        }
    }
}
