using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WallDetailTool
{
    internal sealed class CantileverDrawForm : Form
    {
        private readonly CantileverDrawState state;
        private readonly TextBox[] fields=new TextBox[15];
        private readonly ComboBox direction,mode,units,input;
        private readonly double normalScale;
        private readonly int unitCode;
        private readonly bool requiresChoice;
        private readonly CantileverSketch sketch;
        private readonly Label explanation;
        internal CantileverDrawRequest Request { get; private set; }
        internal CantileverDrawForm(CantileverDrawState state,string unitName,int unitCode,double scale,bool choice)
        {
            this.state=state;normalScale=scale;this.unitCode=unitCode;requiresChoice=choice;
            Name="CantileverDrawForm";Text="WallDetailTool V0.5 普通悬挑板";
            ClientSize=new Size(960,760);AutoScaleMode=AutoScaleMode.Dpi;
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=4,Padding=new Padding(14) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));
            foreach(float h in new[]{120f,450f,110f,50f}) root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
            var selectors=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=3 };
            selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,75));
            direction=Choice("Direction",new[]{"向右伸出","向左伸出"},state.Saved!=null&&state.Saved.Direction==CantileverDirection.Left?1:0);
            mode=Choice("Mode",new[]{"1 无内跨 · 支座端向上","2 无内跨 · 支座端向下","3 全部由内跨板伸出","4 内跨伸出筋＋独立附加筋"},state.Saved==null?0:(int)state.Saved.Mode-1);
            input=Choice("InputMethod",new[]{"选择已有轮廓","两点矩形（仅局部）"},0);
            Row(selectors,2,"输入方式",input);
            Row(selectors,0,"方向",direction);Row(selectors,1,"支座模式",mode);
            root.Controls.Add(selectors,0,0);root.SetColumnSpan(selectors,2);
            var grid=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=15 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
            string[] names={"梁宽 b（不生成梁宽尺寸）","支座水平投影 A","支座竖向投影 V","内跨绘制长度 E","自由端下弯投影 F","底筋入支座长度 B","附加筋伸入内跨 Lin",
                "面筋 / 内跨伸出筋直径","面筋 / 内跨伸出筋间距","底筋直径","底筋间距","独立附加筋直径","独立附加筋间距","点筋实际直径","点筋最大中心距 s"};
            for(int i=0;i<15;i++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute,30));
                fields[i]=new TextBox { Name="Parameter"+i,Text=state.Initial[i],Dock=DockStyle.Fill };
                if(i==4)
                {
                    fields[i].Enabled=false;
                    Row(grid,i,"自由端下弯",new Label { Name="AutomaticF",Text="自动：底筋中心线以上10 mm",Dock=DockStyle.Fill });continue;
                }
                Row(grid,i,names[i]+" (mm)",fields[i]);
            }
            root.Controls.Add(grid,0,1);
            var right=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(14,0,0,0) };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute,200));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            sketch=new CantileverSketch { Name="Sketch",Dock=DockStyle.Fill };right.Controls.Add(sketch,0,0);
            explanation=new Label { Name="Explanation",Dock=DockStyle.Fill,TextAlign=ContentAlignment.TopLeft };right.Controls.Add(explanation,0,1);root.Controls.Add(right,1,1);
            var unitPanel=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2 };
            unitPanel.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            unitPanel.Controls.Add(new Label { Dock=DockStyle.Fill,Text="当前图纸单位："+unitName+"。输入均为毫米，不修改INSUNITS。\n下一步只选悬挑板局部矩形或向梁开口U形边线；原图只读。" },0,0);
            units=Choice("Units",choice?(scale>0?new[]{"按当前图纸单位换算","本次明确按毫米解释"}:new[]{"本次明确按毫米解释"}):new[]{"毫米 / 无单位按毫米"},choice?-1:0);
            units.Enabled=choice;unitPanel.Controls.Add(units,0,1);root.Controls.Add(unitPanel,0,2);root.SetColumnSpan(unitPanel,2);
            var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft };
            var cancel=new Button { Name="Cancel",Text="取消",Width=90,DialogResult=DialogResult.Cancel };
            var generate=new Button { Name="Generate",Text="选择悬挑轮廓并生成",Width=180 };
            generate.Click+=(s,e)=> { try { Request=CreateRequest();DialogResult=DialogResult.OK;Close(); }
                catch(Exception ex) { MessageBox.Show(this,ex.Message,"参数无效",MessageBoxButtons.OK,MessageBoxIcon.Warning); } };
            buttons.Controls.Add(cancel);buttons.Controls.Add(generate);root.Controls.Add(buttons,0,3);root.SetColumnSpan(buttons,2);
            Controls.Add(root);AcceptButton=generate;CancelButton=cancel;
            input.SelectedIndexChanged+=(s,e)=>generate.Text=input.SelectedIndex==1?"点取两点并生成":"选择悬挑轮廓并生成";
            mode.SelectedIndexChanged+=(s,e)=>RefreshMode();direction.SelectedIndexChanged+=(s,e)=>RefreshMode();RefreshMode();
        }
        private void RefreshMode()
        {
            int m=mode.SelectedIndex+1;
            for(int i=0;i<15;i++)
            {
                bool used=true;if(i==1||i==2) used=m==1||m==2;
                if(i==4) used=false;
                if(i==3) used=m>=3;if(i==6||i==11||i==12) used=m==4;
                fields[i].Enabled=used;
            }
            sketch.Leftward=direction.SelectedIndex==1;sketch.Mode=m;sketch.Invalidate();
            explanation.Text="示意不按比例；数值为可修改的绘图预设。\n\nA：悬挑侧梁面 → 支座竖筋中心线。\nV：面筋水平中心线 → 支座端点。\nE：内跨侧梁面 → 伸出筋绘制终点。\nB：悬挑侧梁面 → 底筋支座端点。\nLin：内跨侧梁面 → 附加筋截止端。\n\n自由端下弯：自动，端头高于底筋中心线10 mm。\nV、F均为含圆弧的竖向投影。\n附加斜勾固定投影26×26，向下。\n上下两排点筋竖向对齐，无竖边中部点筋。\n允许支座筋突出梁，需人工审核。\n预设不是图集推荐或规范合格结论。";
        }
        internal CantileverDrawRequest CreateRequest()
        {
            if(mode.SelectedIndex<0) throw new ArgumentException("请明确选择四种支座模式之一。");
            if(requiresChoice&&units.SelectedIndex<0) throw new ArgumentException("请明确选择本次图纸单位解释。");
            bool mm=requiresChoice&&(normalScale<=0||units.SelectedIndex==1);
            return state.Create(fields.Select(f=>f.Text).ToArray(),direction.SelectedIndex==0?CantileverDirection.Right:CantileverDirection.Left,
                (CantileverSupportMode)(mode.SelectedIndex+1),mm?1:requiresChoice?normalScale:1,unitCode,mm,input.SelectedIndex==1);
        }
        private static ComboBox Choice(string name,string[] items,int selected)
        { var c=new ComboBox { Name=name,Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList };c.Items.AddRange(items);c.SelectedIndex=selected;return c; }
        private static void Row(TableLayoutPanel grid,int index,string caption,Control control)
        { grid.Controls.Add(new Label { Text=caption,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft },0,index);grid.Controls.Add(control,1,index); }
    }
    internal sealed class CantileverSketch : Control
    {
        internal bool Leftward;internal int Mode;
        internal CantileverSketch() { DoubleBuffered=true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;g.Clear(Color.White);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Func<float,float> x=v=>Leftward?Width-v:v;
            using(var concrete=new SolidBrush(Color.FromArgb(230,235,242)))
            using(var line=new Pen(Color.SteelBlue,3)) using(var bottom=new Pen(Color.Sienna,3)) using(var extra=new Pen(Color.Purple,2))
            {
                float edge=Math.Min(Width-20,390),support=170,inner=110;
                g.FillRectangle(concrete,Math.Min(x(inner),x(support)),55,60,115);
                g.FillRectangle(concrete,Math.Min(x(support),x(edge)),55,edge-support,80);
                if(Mode>=3) g.FillRectangle(concrete,Math.Min(x(15),x(inner)),55,inner-15,80);
                float start=Mode>=3?20:130;
                g.DrawLine(line,x(start),72,x(edge-18),72);g.DrawLine(line,x(edge-18),72,x(edge-18),110);
                if(Mode==1||Mode==2) g.DrawLine(line,x(start),72,x(start),Mode==1?22:155);
                g.DrawLine(bottom,x(140),118,x(edge-18),118);
                if(Mode==4) { g.DrawLine(extra,x(65),72,x(edge-18),72);g.DrawLine(extra,x(65),72,x(78),85); }
            }
            g.DrawString("面筋与底筋独立；左右共用计算",Font,Brushes.Black,5,175);
        }
    }
}
