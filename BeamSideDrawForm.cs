using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WallDetailTool
{
    internal sealed class BeamSideDrawForm : Form
    {
        private readonly BeamSideDrawState state;
        private readonly TextBox[] fields=new TextBox[6];
        private readonly ComboBox direction,mode,top,bottom,annotation,units;
        private readonly BeamSideSketch sketch;
        private readonly double normalScale;
        private readonly int unitCode;
        private readonly bool requiresChoice;
        private readonly Label description;
        internal BeamSideDrawRequest Request { get; private set; }
        internal BeamSideDrawForm(BeamSideDrawState state,string unitName,int unitCode,double normalScale,bool requiresChoice)
        {
            this.state=state;this.unitCode=unitCode;this.normalScale=normalScale;this.requiresChoice=requiresChoice;
            Text="WallDetailTool 梁侧挂板";Name="BeamSideDrawForm";ClientSize=new Size(660,770);
            AutoScaleMode=AutoScaleMode.Dpi;FormBorderStyle=FormBorderStyle.FixedDialog;
            MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
            var content=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Padding=new Padding(16) };
            foreach(float height in new[]{72f,124f,70f,204f,108f,78f,48f}) content.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            var header=Grid(2);
            direction=Choice("Direction",new[]{"向右伸出","向左伸出"},state.Saved!=null&&state.Saved.Direction==BeamSideDirection.Left?1:0);
            mode=Choice("Mode",new[]{"局部轮廓（矩形 / 开放 U）","完整梁与块体截面"},state.Saved==null?-1:state.Saved.FullSection?1:0);
            Row(header,0,"块体伸出方向",direction);Row(header,1,"轮廓识别模式",mode);content.Controls.Add(header,0,0);
            sketch=new BeamSideSketch { Name="Sketch",Dock=DockStyle.Fill };content.Controls.Add(sketch,0,1);
            description=new Label { Name="Description",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft };content.Controls.Add(description,0,2);
            var numbers=Grid(6);
            var names=new[]{"线筋实际直径 (mm)","线筋间距 (mm)","点筋实际直径 (mm)","点筋最大中心距 s (mm)","梁内总长 L (mm，沿中心线)","竖直直段 V (mm，圆弧切点至端头)"};
            for(int i=0;i<fields.Length;i++)
            {
                fields[i]=new TextBox { Name="Parameter"+i,Dock=DockStyle.Fill,Text=state.InitialText[i] };
                Row(numbers,i,names[i],fields[i]);
            }
            content.Controls.Add(numbers,0,3);
            var options=Grid(3);var saved=state.Saved==null?null:state.Saved.Parameters;
            top=Choice("TopBend",new[]{"向下（世界坐标）","向上（世界坐标）"},saved!=null&&saved.TopUp?1:0);
            bottom=Choice("BottomBend",new[]{"向上（世界坐标）","向下（世界坐标）"},saved!=null&&!saved.BottomUp?1:0);
            annotation=Choice("AnnotationSide",new[]{"右侧引出","左侧引出"},saved!=null&&!saved.AnnotationRight?1:0);
            Row(options,0,"上端弯折",top);Row(options,1,"下端弯折",bottom);Row(options,2,"点筋整组标注",annotation);content.Controls.Add(options,0,4);
            var unitPanel=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2 };
            unitPanel.RowStyles.Add(new RowStyle(SizeType.Absolute,38));unitPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            unitPanel.Controls.Add(new Label { Name="UnitDescription",Text="当前图纸单位："+unitName+"；参数为毫米，不修改图纸单位。"+
                (requiresChoice?"\n请明确选择本次单位解释。":""),Dock=DockStyle.Fill },0,0);
            units=new ComboBox { Name="Units",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList };
            if(requiresChoice)
            {
                if(normalScale>0) units.Items.Add("按当前图纸单位换算（1 mm = "+normalScale.ToString("g")+" CAD 单位）");
                units.Items.Add("明确按 1 CAD 单位 = 1 mm 解释本次轮廓");
                units.SelectedIndex=-1;
            }
            else { units.Items.Add(unitCode==0?"无单位图纸按毫米处理":"毫米图纸，1 CAD 单位 = 1 mm");units.SelectedIndex=0;units.Enabled=false; }
            unitPanel.Controls.Add(units,0,1);content.Controls.Add(unitPanel,0,5);
            var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,10,0,0) };
            var cancel=new Button { Name="Cancel",Text="取消",Width=90,DialogResult=DialogResult.Cancel };
            var generate=new Button { Name="Generate",Text="选择轮廓并生成",Width=165 };
            generate.Click+=(s,e)=>
            {
                try { Request=CreateRequest();DialogResult=DialogResult.OK;Close(); }
                catch(Exception ex) { MessageBox.Show(this,ex.Message,"参数无效",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
            };
            buttons.Controls.Add(cancel);buttons.Controls.Add(generate);content.Controls.Add(buttons,0,6);
            Controls.Add(content);AcceptButton=generate;CancelButton=cancel;
            foreach(var selector in new[]{direction,mode,top,bottom,annotation}) selector.SelectedIndexChanged+=(s,e)=>RefreshSketch();
            RefreshSketch();
        }
        private static ComboBox Choice(string name,string[] items,int index)
        { var c=new ComboBox { Name=name,Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList };c.Items.AddRange(items);c.SelectedIndex=index;return c; }
        private static TableLayoutPanel Grid(int rows)
        {
            var panel=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=rows };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
            for(int i=0;i<rows;i++) panel.RowStyles.Add(new RowStyle(SizeType.Percent,100f/rows));return panel;
        }
        private static void Row(TableLayoutPanel p,int row,string label,Control input)
        { p.Controls.Add(new Label { Text=label,AutoSize=true,Anchor=AnchorStyles.Left },0,row);p.Controls.Add(input,1,row); }
        private void RefreshSketch()
        {
            sketch.ExtendsRight=direction.SelectedIndex==0;sketch.TopUp=top.SelectedIndex==1;
            sketch.BottomUp=bottom.SelectedIndex==0;sketch.AnnotationRight=annotation.SelectedIndex==0;sketch.Full=mode.SelectedIndex==1;sketch.Invalidate();
            description.Text=(mode.SelectedIndex<0?"请明确选择局部或完整模式；完整等高外框不能按局部处理。":mode.SelectedIndex==0?"局部：梁边界未知，梁内端部需人工审核。":"完整：选择梁及块体；等高外框无连接缝时还须点取连接面 X。")+
                "\nL、V 为上下端共用输入；L 含梁内水平段、90°圆弧和竖段，V 不含圆弧。\n保护层、显示尺寸、R、字高和图层使用共享高级设置。";
        }
        internal BeamSideDrawRequest CreateRequest()
        {
            if(mode.SelectedIndex<0) throw new InvalidOperationException("请明确选择局部轮廓或完整截面；完整等高外框不能默认按局部处理。");
            if(units.SelectedIndex<0) throw new InvalidOperationException("当前图纸不是毫米，请明确选择本次单位解释。");
            bool mmOverride=requiresChoice&&(normalScale<=0||units.SelectedIndex==1);
            double scale=mmOverride||!requiresChoice?1:normalScale;
            return state.Create(Array.ConvertAll(fields,t=>t.Text),direction.SelectedIndex==0?BeamSideDirection.Right:BeamSideDirection.Left,
                mode.SelectedIndex==1,top.SelectedIndex==1,bottom.SelectedIndex==0,annotation.SelectedIndex==0,scale,unitCode,mmOverride);
        }
    }
    internal sealed class BeamSideSketch : Control
    {
        internal bool ExtendsRight=true,TopUp,BottomUp=true,AnnotationRight=true,Full;
        internal BeamSideSketch() { DoubleBuffered=true;BackColor=Color.WhiteSmoke; }
        private PointF P(float x,float y) { return new PointF(ExtendsRight?x:Width-x,y); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            float c=Width*.48f,o=c+78,t=28,b=Height-22;
            using(var fill=new SolidBrush(Full?Color.FromArgb(218,224,230):Color.FromArgb(237,237,237)))
                g.FillRectangle(fill,ExtendsRight?c-150:Width-c,17,150,Height-24);
            using(var concrete=new Pen(Color.DimGray,1.3f))
            {
                g.DrawLines(concrete,new[]{P(c,t),P(o,t),P(o,b),P(c,b)});
                g.DrawLine(concrete,P(c,17),P(c,Height-7));
                if(Full) g.DrawLines(concrete,new[]{P(c,17),P(c-150,17),P(c-150,Height-7),P(c,Height-7)});
            }
            using(var steel=new Pen(Color.Firebrick,2.5f))
                g.DrawLines(steel,new[]{P(c-55,t+10+(TopUp?-20:20)),P(c-55,t+10),P(o-10,t+10),P(o-10,b-10),P(c-55,b-10),P(c-55,b-10+(BottomUp?-20:20))});
            foreach(var p in new[]{P(o-18,t+18),P(o-18,(t+b)/2),P(o-18,b-18)})
                g.FillEllipse(Brushes.Firebrick,p.X-2.5f,p.Y-2.5f,5,5);
            var anchor=P(o-18,b-18);var lineAnchor=P(o-10,t+35);float end=AnnotationRight?Width-15:15;
            using(var leader=new Pen(Color.SteelBlue,1))
            { g.DrawLine(leader,anchor,new PointF(end,anchor.Y));g.DrawLine(leader,lineAnchor,new PointF(end,lineAnchor.Y)); }
            g.DrawString("梁",Font,Brushes.DimGray,P(c-110,22));
            g.DrawString("L / V 示意（非比例）",Font,Brushes.DimGray,8,3);
        }
    }
}
