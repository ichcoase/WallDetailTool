using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
namespace WallDetailTool
{
    // UI-only adapter. Does not own engineering requests, CAD objects or settings.
    internal sealed class UnifiedNodeUi
    {
        readonly Form form;readonly string kind;readonly TextBox[] fields;readonly string[] baseline,presets;
        readonly Func<string> key;readonly Dictionary<string,string[]> drafts=new Dictionary<string,string[]>();
        string active;bool switching;readonly ErrorProvider errors;readonly Label details;
        readonly Dictionary<TextBox,Control> rows=new Dictionary<TextBox,Control>();
        readonly Control sketch;readonly Button generate;readonly ComboBox input;
        readonly TableLayoutPanel specs;
        internal static UnifiedNodeUi Install(Form form,string kind,TextBox[] fields,string[] presets,Func<string> key)
        { return new UnifiedNodeUi(form,kind,fields,presets,key); }
        internal static double[] MergeDefaults(double[] shared,double[] record)
        {
            var values=(double[])shared.Clone();
            for(int i=0;i<Math.Min(values.Length,record.Length);i++)
                if(!double.IsNaN(record[i])&&!double.IsInfinity(record[i])&&(i==0?record[i]>=0:record[i]>0))values[i]=record[i];
            return values;
        }
        static IEnumerable<Control> All(Control c)
        { foreach(Control child in c.Controls) { yield return child;foreach(var nested in All(child))yield return nested; } }
        static Label Caption(string t) { return new Label { Text=t,AutoSize=true,Margin=new Padding(3,8,3,3) }; }
        static TableLayoutPanel Stack() {
            var p=new TableLayoutPanel { AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,ColumnCount=1 };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            p.ControlAdded+=(s,e)=> { int row=p.Controls.Count-1;p.SetCellPosition(e.Control,new TableLayoutPanelCellPosition(0,row));p.SetColumnSpan(e.Control,1);p.SetRowSpan(e.Control,1); };
            return p;
        }
        UnifiedNodeUi(Form form,string kind,TextBox[] fields,string[] presets,Func<string> key)
        {
            this.form=form;this.kind=kind;this.fields=fields;this.presets=presets;this.key=key;
            baseline=fields.Select(f=>f.Text).ToArray();active=key();drafts[active]=(string[])baseline.Clone();
            var controls=All(form).ToArray();sketch=controls.First(c=>c.Name=="Sketch");
            generate=(Button)controls.First(c=>c.Name=="Generate");var cancel=(Button)controls.First(c=>c.Name=="Cancel");
            input=(ComboBox)controls.First(c=>c.Name=="InputMethod");
            var selectors=controls.OfType<ComboBox>().Where(c=>c.Name!="Units").ToArray();
            var unit=controls.OfType<ComboBox>().FirstOrDefault(c=>c.Name=="Units");
            var unitText=controls.OfType<Label>().FirstOrDefault(c=>c.Text.StartsWith("当前图纸"));
            var auto=controls.FirstOrDefault(c=>c.Name=="AutomaticF");
            var rule=controls.FirstOrDefault(c=>c.Name=="Description"||c.Name=="Explanation");
            var hCaption=controls.OfType<Label>().FirstOrDefault(c=>c.Name=="HLabel");
            var names=fields.Select(f=>f.Parent==null?"":((TableLayoutPanel)f.Parent).GetControlFromPosition(0,((TableLayoutPanel)f.Parent).GetRow(f)).Text).ToArray();
            var retained=selectors.Cast<Control>().Concat(fields).Concat(new[]{sketch,generate,cancel}).ToList();
            if(unit!=null)retained.Add(unit);if(unitText!=null)retained.Add(unitText);if(auto!=null)retained.Add(auto);
            if(rule!=null)retained.Add(rule);if(hCaption!=null)retained.Add(hCaption);
            foreach(var c in retained)if(c.Parent!=null)c.Parent.Controls.Remove(c);
            var old=form.Controls.Cast<Control>().ToArray();form.Controls.Clear();foreach(var c in old)c.Dispose();
            form.FormBorderStyle=FormBorderStyle.Sizable;form.MaximizeBox=true;form.MinimumSize=new Size(640,440);
            form.ClientSize=new Size(930,680);form.AutoScaleMode=AutoScaleMode.Dpi;
            var root=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var header=new FlowLayoutPanel { Dock=DockStyle.Fill,AutoSize=true,WrapContents=true };
            foreach(string name in new[]{"Direction","InputMethod","Mode"})
            {
                var c=selectors.FirstOrDefault(s=>s.Name==name);if(c==null)continue;
                header.Controls.Add(Caption(name=="Direction"?"方向":name=="Mode"?"构造模式":"输入方式"));c.Dock=DockStyle.None;c.Width=name=="Mode"?265:180;header.Controls.Add(c);
            }
            root.Controls.Add(header,0,0);
            var body=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,54));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,46));
            var scroll=new Panel { Dock=DockStyle.Fill,AutoScroll=true };var left=Stack();scroll.Controls.Add(left);body.Controls.Add(scroll,0,0);
            left.Controls.Add(Caption("钢筋规格　（直径、间距单位：mm）"));
            specs=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=false,Height=200,ColumnCount=3 };
            specs.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
            specs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));specs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));specs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
            specs.Controls.Add(Caption("组别"),0,0);specs.Controls.Add(Caption("直径"),1,0);specs.Controls.Add(Caption("间距"),2,0);
            var pairs=kind=="cantilever"?new[]{7,11,9,13}:new[]{0,2};int row=1;
            foreach(int i in pairs)
            {
                var line=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=false,Height=38,ColumnCount=3 };
                specs.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
                line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
                line.Controls.Add(Caption(kind=="cantilever"?(i==7?"面筋 / 伸出筋":i==11?"附加筋":i==9?"底筋":"上下点筋"):(i==0?"线筋":"点筋")),0,0);
                line.Controls.Add(fields[i],1,0);line.Controls.Add(fields[i+1],2,0);specs.Controls.Add(line,0,row++);specs.SetColumnSpan(line,3);
                rows[fields[i]]=line;rows[fields[i+1]]=line;
            }
            left.Controls.Add(specs);left.Controls.Add(Caption("构造尺寸　（mm）"));
            for(int i=0;i<fields.Length;i++)
            {
                if(rows.ContainsKey(fields[i])||(kind=="cantilever"&&i==4))continue;
                var line=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=false,Height=40,ColumnCount=2 };
                line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70));line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
                var caption=kind=="slab"&&i==5&&hCaption!=null?hCaption:Caption(names[i].Replace(" (mm)","").Replace(" (mm，", "（"));
                if(kind=="slab"&&i==5)caption.Name="HLabel";
                line.Controls.Add(caption,0,0);line.Controls.Add(fields[i],1,0);left.Controls.Add(line);rows[fields[i]]=line;
            }
            if(auto!=null) { auto.Dock=DockStyle.Top;auto.Height=46;left.Controls.Add(auto); }
            var low=Stack();low.Visible=false;var expand=new Button { Text="显示 / 标注与低频选项 ▸",AutoSize=true };
            expand.Click+=(s,e)=>low.Visible=!low.Visible;left.Controls.Add(expand);left.Controls.Add(low);
            foreach(var c in selectors.Where(s=>s.Name!="Direction"&&s.Name!="Mode"&&s.Name!="InputMethod"))
            { low.Controls.Add(Caption(c.Name=="TopBend"?"上端弯折":c.Name=="BottomBend"?"下端弯折":"规格标注侧"));c.Dock=DockStyle.Top;low.Controls.Add(c); }
            low.Controls.Add(Caption("保护层、R、显示尺寸、字高与图层继续使用工具条已有高级设置；本窗口恢复不会重置这些设置。"));
            var right=new Panel { Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(10,0,0,0) };var guide=Stack();right.Controls.Add(guide);
            body.Controls.Add(right,1,0);
            sketch.Dock=DockStyle.Top;sketch.Height=200;guide.Controls.Add(sketch);
            details=new Label { Name="ParameterHelp",Dock=DockStyle.Top,AutoSize=true,MaximumSize=new Size(340,0),Text="选择参数查看起算点；示意不按比例，不是实时CAD预览。",Padding=new Padding(4,12,4,12) };guide.Controls.Add(details);
            if(rule!=null) { rule.Dock=DockStyle.Top;rule.AutoSize=true;rule.MaximumSize=new Size(340,0);guide.Controls.Add(rule); }
            guide.Controls.Add(Caption("1 选择方向、构造和输入方式。\n2 修改参数后生成，选择轮廓或点两个对角点。\n3 核对配筋；试用预设不是规范推荐。"));
            var help=new Button { Name="Help",Text="本节点示例与帮助",AutoSize=true };help.Click+=(s,e)=>ShowHelp();guide.Controls.Add(help);root.Controls.Add(body,0,1);
            var footer=Stack();
            if(unitText!=null) { unitText.Dock=DockStyle.Top;unitText.Height=34;unitText.Text=unitText.Text.Split('\n')[0];footer.Controls.Add(unitText); }
            else footer.Controls.Add(Caption("当前节点沿用 1 CAD 单位＝1 mm；参数输入mm，不修改INSUNITS。"));
            if(unit!=null) { unit.Dock=DockStyle.Top;footer.Controls.Add(unit); }
            var actions=new FlowLayoutPanel { Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,WrapContents=true };
            generate.AutoSize=true;generate.Width=210;generate.Height=32;cancel.Height=32;
            var reset=new Button { Name="RestorePresets",Text="恢复试用预设",AutoSize=true,Height=32 };
            reset.Click+=(s,e)=> { for(int i=0;i<fields.Length;i++)if(fields[i].Enabled)fields[i].Text=presets[i];errors.Clear(); };
            actions.Controls.Add(generate);actions.Controls.Add(cancel);actions.Controls.Add(reset);footer.Controls.Add(actions);root.Controls.Add(footer,0,2);form.Controls.Add(root);
            reset.TabIndex=0;cancel.TabIndex=1;generate.TabIndex=2;
            errors=new ErrorProvider { ContainerControl=form,BlinkStyle=ErrorBlinkStyle.NeverBlink };
            form.Disposed+=(s,e)=>errors.Dispose();
            for(int i=0;i<fields.Length;i++)
            {
                int index=i;var f=fields[i];f.Dock=DockStyle.Fill;f.Margin=new Padding(4,6,22,6);f.TabIndex=i;
                f.EnabledChanged+=(s,e)=>Refresh();f.Enter+=(s,e)=> { details.Text=Explain(kind,index,form.Text);Highlight(index); };
                f.TextChanged+=(s,e)=>errors.SetError(f,"");
            }
            foreach(var c in selectors)c.SelectedIndexChanged+=(s,e)=> { Switch();Refresh(); };
            sketch.Paint+=(s,e)=>PaintFocus(e.Graphics);
            form.Shown+=(s,e)=> { var area=Screen.FromControl(form).WorkingArea;form.MinimumSize=new Size(Math.Min(form.MinimumSize.Width,area.Width-20),Math.Min(form.MinimumSize.Height,area.Height-30));form.Size=new Size(Math.Min(form.Width,area.Width-20),Math.Min(form.Height,area.Height-30));Refresh(); };Refresh();
            form.FontChanged+=(s,e)=>Refresh();
        }
        void Switch()
        {
            if(switching||kind=="beam"||kind=="slab")return; // their directional state already owns drafts
            var next=key();if(next==active)return;switching=true;
            drafts[active]=fields.Select(f=>f.Text).ToArray();string[] value;
            if(!drafts.TryGetValue(next,out value))value=(string[])baseline.Clone();
            for(int i=0;i<fields.Length;i++)fields[i].Text=value[i];active=next;switching=false;
        }
        void Refresh()
        { foreach(var kv in rows)kv.Value.Visible=kv.Key.Enabled;generate.Text=input.SelectedIndex==1?"点两个对角点并生成":"选择轮廓并生成";
            int lineHeight=Math.Max(38,form.Font.Height+30);
            int total=34;foreach(var row in rows.Values.Distinct().Where(r=>r.Parent==specs)) { int height=row.Controls.OfType<TextBox>().All(f=>f.Enabled)?lineHeight:0;row.Height=height;specs.RowStyles[specs.GetRow(row)].Height=height;total+=height; }specs.Height=total;
            foreach(var row in rows.Values.Distinct().Where(r=>r.Parent!=specs))row.Height=Math.Max(40,lineHeight);
            if(kind=="cantilever") { var label=rows[fields[7]].Controls.OfType<Label>().First();var mode=(ComboBox)form.Controls.Find("Mode",true).First();label.Text=mode.SelectedIndex>=2?"内跨伸出筋":"面筋"; }
        }
        internal bool Validate()
        {
            errors.Clear();bool ok=true;
            foreach(var c in new[]{"Units","Mode"}.SelectMany(n=>form.Controls.Find(n,true)).OfType<ComboBox>())
                if(c.Enabled&&c.SelectedIndex<0) { errors.SetError(c,"请选择本次"+(c.Name=="Units"?"单位解释":"构造模式"));ok=false; }
            foreach(var f in fields.Where(f=>f.Enabled&&rows.ContainsKey(f)))
            {
                // Preserve accepted legacy blank-right=left and optional slab h.
                int i=Array.IndexOf(fields,f);
                if(String.IsNullOrWhiteSpace(f.Text)&&((kind=="beam"&&i==5)||(kind=="slab"&&(i==7||(i==4&&input.SelectedIndex==0)))))continue;
                double n;if(!double.TryParse(f.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out n)||double.IsInfinity(n)||double.IsNaN(n)||n<=0)
                { errors.SetError(f,"请输入有效正数（mm）");ok=false; }
            }
            if(!ok) { var first=fields.FirstOrDefault(f=>!String.IsNullOrEmpty(errors.GetError(f)));if(first!=null)first.Focus(); }return ok;
        }
        void Highlight(int index)
        { focused=index;sketch.Invalidate(); }
        int focused=-1;
        object SketchValue(string name) { return sketch.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(sketch); }
        bool Flag(string name) { return (bool)SketchValue(name); }
        void PaintFocus(Graphics g)
        {
            if(focused<0)return;
            foreach(var path in FocusSegments(focused))using(var pen=new Pen(Color.DarkOrange,3))g.DrawLines(pen,path);
            string caption=Explain(kind,focused,form.Text);caption=caption.Contains("：")?caption.Split('：')[0]:"规格";
            g.DrawString(caption+" · 选中参数",form.Font,Brushes.DarkOrange,8,sketch.Height-18);
        }
        // Explanatory coordinates follow the native sketches, never CAD geometry.
        internal PointF[][] FocusSegments(int index)
        {
            int focused=index;
            float a=30,b=140,y=35,z=110;bool vertical=false;string symbol="规格";
            bool c=kind=="cantilever",n=kind=="beam"||kind=="slab",side=kind=="side";
            if(c&&focused<7)
            {
                var starts=new[]{110f,130,130,20,0,140,65};var ends=new[]{170f,170,130,110,0,170,110};
                a=starts[focused];b=ends[focused];symbol=new[]{"b","A","V","E","F自动","B","Lin"}[focused];
                if(focused==2){vertical=true;y=72;z=Convert.ToInt32(SketchValue("Mode"))==1?22:155;}
                if(Flag("Leftward")){a=sketch.Width-a;b=sketch.Width-b;}
            }
            else if(n&&focused>=4)
            {
                float cx=sketch.Width*.46f,face=sketch.Height*(Flag("Hanging")?.34f:.60f),sign=Flag("Hanging")?1:-1;
                if((kind=="slab")&&focused>=6){a=cx+(focused==6?-75:34);b=cx+(focused==6?-34:75);y=face-sign*16;symbol=focused==6?"L左":"L右";}
                else if(kind=="slab") { a=cx-90;vertical=true;y=face-sign*sketch.Height*.2f;z=focused==4?face:face-sign*16;symbol=focused==4?"h":"H"; }
                else {a=cx+(focused==4?-34:34);vertical=true;y=face;z=face-sign*16;symbol=focused==4?"左锚固":"右锚固";}
            }
            else if(side&&focused>=4)
            {
                a=sketch.Width*.48f-55;b=sketch.Width*.48f;y=38;z=y+(Flag("TopUp")?-20:20);
                if(!Flag("ExtendsRight")){a=sketch.Width-a;b=sketch.Width-b;}
                return new[]{focused==4?new[]{new PointF(b,y),new PointF(a,y),new PointF(a,z)}:new[]{new PointF(a,y),new PointF(a,z)}};
            }
            if(symbol=="规格")return new PointF[0][];
            return new[]{vertical?new[]{new PointF(a,y),new PointF(a,z)}:new[]{new PointF(a,y),new PointF(b,y)}};
        }
        internal static string Explain(string kind,int i,string title)
        {
            if(kind=="cantilever")return new[]{"b：两梁面水平宽度，仅定位，不生成b尺寸。","A：悬挑侧梁面至支座竖筋中心线的水平投影。","V：面筋水平中心线至支座端点的竖向投影，含圆弧。","E：内跨侧梁面至伸出筋绘制终点的水平长度。","F：自动，端头高于实际底筋水平中心线10mm。","B：悬挑侧梁面至底筋支座端头的水平长度。","Lin：内跨侧梁面至附加筋截止，不含梁宽b；符号26×26不混入长度。"}.ElementAtOrDefault(i)??"本组实际直径 / 间距；显示尺寸独立。模式4两组面筋同路径、身份独立。";
            if(i<4)return "本组实际直径或输入间距，显示宽度/大小独立；点筋按本节点已确认规则计算。";
            if(kind=="side")return i==4?"L：梁内水平段＋90°圆弧中心线展开长度＋竖直直段V。":"V：圆弧切点至梁内端头的竖直直段，不含R或圆弧。";
            if(kind=="beam")return (i==4?"左":"右")+"端锚固直段：从连接面至端头，按本节点原定义，不自动算La。";
            if(i==4)return "h：局部楼板厚度；完整截面可从轮廓取得，两点矩形必须输入。";
            if(i==5)return title.Contains("下")?"H：从板顶向下至水平筋中心线。":"H：从板底向上至水平筋中心线。";
            return (i==6?"L左":"L右")+"：水平圆弧切点至端头直段，不含圆弧。";
        }
        void ShowHelp()
        {
            using(var window=new Form { Text=form.Text+" · 本地帮助",Size=new Size(620,520),StartPosition=FormStartPosition.CenterParent })
            {
                var text=new TextBox { Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,
                    Text=form.Text+"\r\n\r\n"+Example(kind)+"\r\n\r\n"+string.Join("\r\n\r\n",Enumerable.Range(0,fields.Length).Where(i=>fields[i].Enabled).Select(i=>Explain(kind,i,form.Text)))+
                    "\r\n\r\n选择已有局部矩形/开放U轮廓，或选择两点矩形后依次点对角点。两点仅局部，不提供完整梁/板边界。仅WCS正交二维；旧梁/板固定毫米。不能容纳时修改参数，不自动缩R或改长度。示意及试用值不等于工程审核。" };
                using(var image=new Bitmap(Math.Max(1,sketch.Width),Math.Max(1,sketch.Height))) {
                    sketch.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));
                    var picture=new PictureBox { Dock=DockStyle.Top,Height=170,SizeMode=PictureBoxSizeMode.Zoom,Image=image };
                    window.Controls.Add(text);window.Controls.Add(picture);window.ShowDialog(form);
                }
            }
        }
        internal static string Example(string kind)
        { return kind=="cantilever"?"普通等厚悬挑板：试用1500×200局部矩形；b300、A180、V250、E600、B200、Lin450；按当前模式启用，F自动。":kind=="side"?"梁侧局部挂板：试用300×600矩形，L400、V100，方向及上下弯折由窗口确定。":kind=="slab"?"板上/下局部节点：试用300×400矩形，板厚200、H31、两端L300；方向选上或下。":"梁上/下局部节点：试用300×400矩形，两端锚固300；方向选上或下。"; }
    }
}
