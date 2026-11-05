using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace WallDetailTool
{
    internal sealed class TopSlabForm : Form
    {
        internal static readonly string[] Names={"外侧连续筋直径","外侧连续筋间距","内侧竖筋直径","内侧竖筋间距","飘板下筋直径","飘板下筋间距",
            "反坎点筋直径","反坎点筋间距","飘板点筋直径","飘板点筋间距","A 顶部水平直段（自动）","B 向下弯折直段","外侧底部锚固 / L","内侧底部锚固 / L","底部楼板厚 h","板底至水平筋中心 H"};
        private readonly TextBox[] fields=new TextBox[16];
        private readonly Label[] captions=new Label[16];
        private readonly string[][] drafts;
        private readonly ComboBox type=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Width=180 };
        private readonly Label dimensions=new Label { AutoSize=true,MaximumSize=new Size(430,0) };
        private readonly Label explanation=new Label { Dock=DockStyle.Fill };
        private readonly Panel sketch=new Panel { Dock=DockStyle.Top,Height=230 };
        private readonly ErrorProvider errors=new ErrorProvider();
        private int active,focus=-1;
        private bool updating,beamAnchorFollows;
        internal TopSlabGeometry Geometry;
        internal bool Selecting;
        internal TopSlabBase Base { get { return (TopSlabBase)(type.SelectedIndex+1); } }
        internal double[] Values { get; private set; }
        internal string[] Current { get { return fields.Select(f=>f.Text).ToArray(); } }
        internal TopSlabForm(string[][] initial,int units)
        {
            drafts=initial.Select(a=>(string[])a.Clone()).ToArray();
            beamAnchorFollows=string.IsNullOrWhiteSpace(drafts[0][13])||drafts[0][13]==drafts[0][12];
            if(beamAnchorFollows)drafts[0][13]=drafts[0][12];
            Text="反坎带飘板";ClientSize=new Size(960,700);MinimumSize=new Size(740,500);StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.Dpi;
            var root=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var header=new FlowLayoutPanel { Dock=DockStyle.Fill,AutoSize=true };
            header.Controls.Add(new Label { Text="底部连接",AutoSize=true });type.Items.AddRange(new object[]{"梁上","板上"});type.SelectedIndex=0;header.Controls.Add(type);
            header.Controls.Add(dimensions);root.Controls.Add(header,0,0);
            var body=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2 };body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));
            var scroll=new Panel { Dock=DockStyle.Fill,AutoScroll=true };var grid=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,ColumnCount=2 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
            for(int i=0;i<fields.Length;i++)
            {
                int index=i;captions[i]=new Label { Name="Caption"+i,Text=Names[i]+" (mm)",AutoSize=true,Margin=new Padding(3,10,3,3) };grid.Controls.Add(captions[i],0,i);
                fields[i]=new TextBox { Name="Parameter"+i,Text=drafts[0][i],Dock=DockStyle.Fill,Margin=new Padding(3,8,3,3) };grid.Controls.Add(fields[i],1,i);
                fields[i].Enter+=(s,e)=> { focus=index;explanation.Text=ParameterExplanation(index);sketch.Invalidate();fields[index].SelectAll(); };
            }
            scroll.Controls.Add(grid);body.Controls.Add(scroll,0,0);
            var right=new Panel { Dock=DockStyle.Fill,Padding=new Padding(15) };right.Controls.Add(explanation);right.Controls.Add(sketch);
            var help=new Button { Text="本地帮助",Dock=DockStyle.Bottom,Height=34 };
            help.Click+=(s,e)=>MessageBox.Show(this,"先填写参数，再选择单个轻量多段线或首尾相接的一组LINE：支持完整顶部L形闭环，或仅缺反坎底部封口边的连续开放链，不支持混选。闭合输入直接生成；开放输入显示临时虚线底边，回车确认生成，也可重选；Esc返回窗口保留草稿。原轮廓不补线、不修改Closed属性。只支持WCS二维、毫米或无单位。\n尺寸及方向由轮廓读取；取消选择保留当前参数。\n三根线筋独立；A自动计算，B填写工程设计值。外侧筋自由端沿用悬挑板下弯，端头比实际板下筋中心线高10mm。\n梁上底部为两端独立直锚，内侧默认跟随外侧且可独立修改；板上为板内弯折，L从水平切点量起。底部梁板不绘制。\n三筋交接按各自计算直径定位，显示尺寸独立。试用值须按工程设计调整，结果须工程师审核。普通块可原生BEDIT；本版不支持参数驱动重生成。","反坎带飘板 · 本地帮助");
            right.Controls.Add(help);body.Controls.Add(right,1,0);root.Controls.Add(body,0,1);
            var footer=new FlowLayoutPanel { Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft };
            var generate=new Button { Name="Generate",Text="选择轮廓并生成",AutoSize=true };var cancel=new Button { Text="取消",DialogResult=DialogResult.Cancel,AutoSize=true };
            footer.Controls.Add(cancel);footer.Controls.Add(generate);footer.Controls.Add(new Label { Text=units==0?"当前单位：未指定，按毫米":"当前单位：毫米",AutoSize=true });root.Controls.Add(footer,0,2);Controls.Add(root);CancelButton=cancel;
            fields[12].TextChanged+=(s,e)=> { if(!updating&&active==0&&beamAnchorFollows) { updating=true;fields[13].Text=fields[12].Text;updating=false; } };
            fields[13].TextChanged+=(s,e)=> { if(!updating&&active==0)beamAnchorFollows=fields[13].Text==fields[12].Text; };
            type.SelectedIndexChanged+=(s,e)=> { drafts[active]=Current;active=type.SelectedIndex;updating=true;for(int i=0;i<16;i++) fields[i].Text=drafts[active][i];updating=false;EnableFields(); };
            generate.Click+=(s,e)=>
            {
                errors.Clear();
                var v=new double[16];bool ok=true;
                for(int i=0;i<16;i++)if(fields[i].Enabled&&!(Base==TopSlabBase.Slab&&i==13&&string.IsNullOrWhiteSpace(fields[i].Text))&&(!double.TryParse(fields[i].Text,NumberStyles.Float,CultureInfo.CurrentCulture,out v[i])||double.IsNaN(v[i])||double.IsInfinity(v[i])||v[i]<=0))
                { errors.SetError(fields[i],"请输入有效正数；B需工程设计值。");ok=false; }
                if(Base==TopSlabBase.Slab&&string.IsNullOrWhiteSpace(fields[13].Text))v[13]=v[12];
                if(Base==TopSlabBase.Slab&&ok&&v[15]>=v[14]) { errors.SetError(fields[15],"H必须小于底部板厚h。");ok=false; }
                if(!ok)return;Values=v;Selecting=true;DialogResult=DialogResult.Retry;Close();
            };
            sketch.Paint+=(s,e)=>PaintSketch(e.Graphics);EnableFields();errors.ContainerControl=this;explanation.Text=Explain(10);
            FormClosed+=(s,e)=>drafts[active]=Current;
        }
        internal string[][] Drafts() { drafts[active]=Current;return drafts.Select(a=>(string[])a.Clone()).ToArray(); }
        internal void RefreshGeometry()
        {
            if(Geometry==null) dimensions.Text="未选择轮廓";
            else dimensions.Text=string.Format("{0}飘　反坎厚{1:g} / 板厚{2:g} / 总高{3:g} / 飘出{4:g} mm",Geometry.Direction==1?"右":"左",Geometry.WallThickness,Geometry.SlabThickness,Geometry.Height,Geometry.Projection);
            sketch.Invalidate();
        }
        internal void SetBase(TopSlabBase value) { type.SelectedIndex=(int)value-1; }
        private void EnableFields()
        {
            fields[10].Enabled=false;fields[10].ReadOnly=true;fields[10].Text="自动";
            bool slab=Base==TopSlabBase.Slab;var grid=(TableLayoutPanel)fields[0].Parent;
            for(int i=12;i<16;i++)
            {
                int row=slab?(i>=14?i-2:i+2):i;
                grid.SetRow(fields[i],row);grid.SetRow(captions[i],row);
                fields[i].Enabled=i<14||slab;fields[i].Visible=captions[i].Visible=i<14||slab;
            }
            captions[12].Text=slab?"外侧水平直段 L外 (mm)":"外侧底部锚固 (mm)";
            captions[13].Text=slab?"内侧水平直段 L内 (mm，留空同外侧)":"内侧底部锚固 (mm)";
            captions[14].Text="底部板厚 h (mm)";captions[15].Text="板底至水平筋中心线距离 H (mm)";
        }
        internal static double[] TrialValues(TopSlabBase type,double lineDiameter,double lineSpacing,double dotDiameter,double dotSpacing,double cover,double lineWidth)
        {
            return new[]{lineDiameter,lineSpacing,lineDiameter,lineSpacing,lineDiameter,lineSpacing,dotDiameter,dotSpacing,dotDiameter,dotSpacing,
                0,60,type==TopSlabBase.Slab?150:300,type==TopSlabBase.Slab?150:300,type==TopSlabBase.Slab?120:0,type==TopSlabBase.Slab?cover+lineWidth/2:0};
        }
        private string ParameterExplanation(int index)
        {
            if(Base!=TopSlabBase.Slab||index<12)return Explain(index);
            return new[]{"L外：外侧底部弯折圆弧的水平切点至端头，和板上反坎的水平直段L定义相同。",
                "L内：内侧底部弯折圆弧的水平切点至端头；留空同L外，可单独覆盖。",
                "h：底部连接板厚，首次试用120mm；不从顶部飘板厚度读取，底部板不绘制。",
                "H：底部板底至水平筋中心线距离，须小于h；默认沿用板上反坎的保护层＋线筋显示宽度/2。"}[index-12];
        }
        internal static string Explain(int i)
        {
            if(i<10)return Names[i]+"：计算直径及规格文字与图形显示大小独立。点筋按显示边缘内侧相切定位。";
            return new[]{"A：切点至端头的水平直段自动计算；红蓝水平筋图面净距10mm，蓝色端头至外侧点筋圆外缘净距10mm。左角点贴外侧竖段，顶排同标高。空间不足拒绝生成。","B：飘板下筋在反坎端向下弯折，圆弧切点至端头竖直直段。",
                "外侧底部：梁上为底部封口面至端头直锚；板上为向外弯折的水平切点至端头L。","内侧底部：梁上为底部封口面至端头直锚；板上为向内侧外弯的水平切点至端头L。",
                "h：底部连接楼板厚度，不是顶部飘板厚度。底部楼板不绘制。","H：底部连接板底至水平筋中心线，0 < H < h。"}[i-10];
        }
        private void PaintSketch(Graphics graphics)
        {
            bool left=Geometry!=null&&Geometry.Direction<0;Func<float,float> x=a=>left?sketch.Width-a:a;
            using(var outline=new Pen(Color.DimGray,2))
            {
                graphics.DrawLines(outline,new[]{new PointF(x(35),210),new PointF(x(35),30),new PointF(x(320),30),new PointF(x(320),95),new PointF(x(115),95),new PointF(x(115),210),new PointF(x(35),210)});
                using(var bar=new Pen(focus==0||focus==1?Color.DarkOrange:Color.Firebrick,3))graphics.DrawLines(bar,new[]{new PointF(x(50),195),new PointF(x(50),45),new PointF(x(300),45),new PointF(x(300),68)});
                using(var bar=new Pen(focus==2||focus==3?Color.DarkOrange:Color.Peru,3))graphics.DrawLines(bar,new[]{new PointF(x(100),195),new PointF(x(100),60),new PointF(x(65),60)});
                using(var bar=new Pen(focus==4||focus==5?Color.DarkOrange:Color.Purple,3))graphics.DrawLines(bar,new[]{new PointF(x(300),80),new PointF(x(70),80),new PointF(x(70),125)});
                using(var highlight=new Pen(Color.DarkOrange,5))
                {
                    if(focus==10)graphics.DrawLine(highlight,x(65),60,x(90),60);
                    if(focus==11)graphics.DrawLine(highlight,x(70),90,x(70),125);
                    if(focus>=12)graphics.DrawLine(highlight,x(focus==12?50:100),165,x(focus==12?50:100),195);
                }
                for(int i=0;i<3;i++)foreach(float xx in new[]{58f,92f})graphics.FillEllipse(focus==6||focus==7?Brushes.DarkOrange:Brushes.Teal,x(xx)-3,140+i*20,6,6);
                for(int i=0;i<4;i++)foreach(float yy in new[]{53f,72f})graphics.FillEllipse(focus==8||focus==9?Brushes.DarkOrange:Brushes.Teal,x(145+i*40)-3,yy,6,6);
            }
            graphics.DrawString("关系示意，无比例；尺寸以所选轮廓为准",Font,Brushes.DimGray,5,5);
        }
        protected override void Dispose(bool disposing) { if(disposing)errors.Dispose();base.Dispose(disposing); }
    }
}
