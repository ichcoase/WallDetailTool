using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WallDetailTool
{
    // One window implementation, configured as a beam or slab node window.
    // It never reads/writes DWG or starts selection; it only returns a request.
    internal sealed class NodeDrawForm : Form
    {
        private readonly NodeDrawState state;
        private readonly ComboBox input;
        private readonly int unitCode;
        private readonly double[][] advanced;
        private readonly TextBox[] fields;
        private readonly Label hLabel,description;
        private readonly NodeSketch sketch;
        private readonly Button generate;
        private UnifiedNodeUi unified;
        internal NodeDrawRequest Request { get; private set; }

        internal NodeDrawForm(NodeDrawState state,double[] upAdvanced,double[] downAdvanced,int unitCode=-1)
        {
            this.state=state;this.unitCode=unitCode; advanced=new[]{(double[])upAdvanced.Clone(),(double[])downAdvanced.Clone()};
            Name="NodeDrawForm"; ClientSize=new Size(580,state.IsSlab?710:620);
            AutoScaleMode=AutoScaleMode.Dpi;
            FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false;
            StartPosition=FormStartPosition.CenterParent;
            var content=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(16) };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute,78));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute,120));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            content.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            var direction=new ComboBox { Name="Direction",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList };
            direction.Items.AddRange(new object[]{"向上反坎","向下下挂"});
            direction.SelectedIndex=state.Hanging?1:0;
            var header=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            header.Controls.Add(new Label { Text="节点方向",AutoSize=true,Anchor=AnchorStyles.Left },0,0);
            header.Controls.Add(direction,1,0);
            input=new ComboBox { Name="InputMethod",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList };
            input.Items.AddRange(new object[]{"选择已有轮廓","两点矩形（仅局部）"});input.SelectedIndex=0;
            header.RowCount=2;
            header.Controls.Add(new Label { Text="输入方式",AutoSize=true,Anchor=AnchorStyles.Left },0,1);
            header.Controls.Add(input,1,1);content.Controls.Add(header,0,0);
            sketch=new NodeSketch { Name="Sketch",Dock=DockStyle.Fill,IsSlab=state.IsSlab,Hanging=state.Hanging };
            content.Controls.Add(sketch,0,1);
            description=new Label { Name="Description",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft };
            content.Controls.Add(description,0,2);
            fields=new TextBox[state.IsSlab?8:6];
            string[] names=state.IsSlab?
                new[]{"线筋实际直径 (mm)","线筋间距 (mm)","点筋实际直径 (mm)","点筋间距 (mm)",
                    "局部轮廓板厚 h (mm)",state.HLabel,"左水平直段 L左 (mm)","右水平直段 L右 (mm，留空同左)"}:
                new[]{"线筋实际直径 (mm)","线筋间距 (mm)","点筋实际直径 (mm)","点筋间距 (mm)",
                    "左侧锚固长度 (mm)","右侧锚固长度 (mm，留空同左)"};
            var grid=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=fields.Length };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,67));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33));
            for(int i=0;i<fields.Length;i++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Percent,100f/fields.Length));
                var caption=new Label { Text=names[i],AutoSize=true,Anchor=AnchorStyles.Left };
                if(state.IsSlab && i==5) { hLabel=caption; hLabel.Name="HLabel"; }
                grid.Controls.Add(caption,0,i);
                fields[i]=new TextBox { Name="Parameter"+i,Dock=DockStyle.Fill };
                grid.Controls.Add(fields[i],1,i);
            }
            content.Controls.Add(grid,0,3);
            var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,10,0,0) };
            var cancel=new Button { Name="Cancel",Text="取消",Width=90,DialogResult=DialogResult.Cancel };
            generate=new Button { Name="Generate",Width=160 };
            generate.Click+=(s,e)=>AcceptValues(); buttons.Controls.Add(cancel); buttons.Controls.Add(generate);
            content.Controls.Add(buttons,0,4); Controls.Add(content); CancelButton=cancel; AcceptButton=generate;
            direction.SelectedIndexChanged+=(s,e)=>
            {
                state.Switch(direction.SelectedIndex==1,ReadText());
                LoadDirection();
            };
            input.SelectedIndexChanged+=(s,e)=> { state.Capture(ReadText());LoadDirection(); };
            LoadDirection();
            unified=UnifiedNodeUi.Install(this,state.IsSlab?"slab":"beam",fields,state.IsSlab?new[]{"8","150","6","200","200","31","300","300"}:new[]{"8","150","6","200","300","300"},()=>state.Hanging.ToString());
        }
        private string[] ReadText() { return Array.ConvertAll(fields,f=>f.Text); }
        private void LoadDirection()
        {
            string[] text=state.Current;
            for(int i=0;i<fields.Length;i++) fields[i].Text=text[i];
            Text="WallDetailTool "+state.NodeName+"绘制";
            generate.Text="生成"+state.NodeName;
            if(hLabel!=null) hLabel.Text=state.HLabel;
            description.Text=state.IsSlab?
                (state.Hanging?"钢筋向上进入楼板，在板顶附近向外弯折。":"钢筋向下进入楼板，在板底附近向外弯折。")+
                    (input.SelectedIndex==1?"\n两点仅定义局部范围，须填写板厚h；L从切点量起。":"\n完整截面识别板厚；局部轮廓须填写 h。水平直段从圆弧切点量起。"):
                (state.Hanging?"钢筋向上进入梁内，左右锚固长度分别输入。":"钢筋向下进入梁内，左右锚固长度分别输入。");
            sketch.Hanging=state.Hanging; sketch.Invalidate();
        }
        internal NodeDrawRequest CreateRequest()
        {
            state.Capture(ReadText());
            return state.Request(advanced[state.Hanging?1:0],input.SelectedIndex==1,unitCode);
        }
        private void AcceptValues()
        {
            if(unified!=null&&!unified.Validate())return;
            try { Request=CreateRequest(); DialogResult=DialogResult.OK; Close(); }
            catch(Exception ex) { MessageBox.Show(this,ex.Message,"参数无效",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        }
    }

    internal sealed class NodeSketch : Control
    {
        internal bool IsSlab,Hanging;
        internal NodeSketch() { DoubleBuffered=true; BackColor=Color.WhiteSmoke; }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
            // Explanatory diagram only: never used as design geometry.
            float cx=Width*.46f, face=Height*(Hanging?.34f:.60f), sign=Hanging?1:-1;
            float supportOther=face-sign*Height*(IsSlab?.20f:.28f), end=face+sign*Height*.32f;
            using(var fill=new SolidBrush(Color.FromArgb(220,225,230)))
                g.FillRectangle(fill,35,Math.Min(face,supportOther),Width-155,Math.Abs(face-supportOther));
            using(var concrete=new Pen(Color.DimGray,1.4f))
            {
                g.DrawRectangle(concrete,35,Math.Min(face,supportOther),Width-155,Math.Abs(face-supportOther));
                g.DrawLines(concrete,new[]{new PointF(cx-45,face),new PointF(cx-45,end),new PointF(cx+45,end),new PointF(cx+45,face)});
            }
            using(var steel=new Pen(Color.Firebrick,3))
            {
                float anchor=face-sign*16, cap=end-sign*8;
                var path=IsSlab?
                    new[]{new PointF(cx-75,anchor),new PointF(cx-34,anchor),new PointF(cx-34,cap),new PointF(cx+34,cap),new PointF(cx+34,anchor),new PointF(cx+75,anchor)}:
                    new[]{new PointF(cx-34,anchor),new PointF(cx-34,cap),new PointF(cx+34,cap),new PointF(cx+34,anchor)};
                g.DrawLines(steel,path);
            }
            using(var arrow=new Pen(Color.Firebrick,2))
            using(var arrowCap=new AdjustableArrowCap(4,5))
            {
                arrow.CustomEndCap=arrowCap;
                g.DrawLine(arrow,cx+100,face+sign*16,cx+100,face-sign*14);
            }
            g.DrawString(IsSlab?"楼板":"梁",Font,Brushes.DimGray,40,Math.Min(face,supportOther)+3);
            g.DrawString(Hanging?"下挂":"反坎",Font,Brushes.DimGray,cx-18,(face+end)/2-7);
            g.DrawString("示意（非比例）",Font,Brushes.DimGray,Width-125,8);
            g.DrawString(Hanging?"向上入筋":"向下入筋",Font,Brushes.Firebrick,Width-125,55);
            base.OnPaint(e); // Paint subscribers add focused dimension after the diagram.
        }
    }
}
