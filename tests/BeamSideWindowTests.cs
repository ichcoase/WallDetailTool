using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace WallDetailTool
{
    internal static class BeamSideWindowTests
    {
        static readonly string[] Layers={"WDT-REBAR-LINE","WDT-REBAR-DOT","S-TEXT","s-floor_de_dim"};
        static readonly double[] Advanced={25,12,12,20,62.5};
        static readonly string[] Text={"8","150","6","200","400","100"};
        static T Find<T>(Form f,string name) where T:Control { return (T)f.Controls.Find(name,true).Single(); }
        static BeamSideDrawState State(BeamSideDrawRequest saved=null)
        { return new BeamSideDrawState(saved,Advanced,Layers,new[]{8.0,150,6,200}); }
        static void Fill(Form form)
        { Find<ComboBox>(form,"Mode").SelectedIndex=0;for(int i=0;i<Text.Length;i++) Find<TextBox>(form,"Parameter"+i).Text=Text[i]; }
        static void Render(Form f,string name)
        {
            f.Show();Application.DoEvents();f.PerformLayout();
            using(var bitmap=new Bitmap(f.Width,f.Height))
            {
                f.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));int red=0,blue=0;
                for(int y=0;y<bitmap.Height;y+=2) for(int x=0;x<bitmap.Width;x+=2)
                { var c=bitmap.GetPixel(x,y);if(c.R>100&&c.G<100&&c.B<100)red++;if(c.B>80&&c.B>c.R*1.2)blue++; }
                BeamSideTest.Check(red>25&&blue>5,"native steel and leader sketch painted");
                bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name+".png"));
            }
            f.Hide();
        }
        [STAThread]
        static void Main() { try { Run(); } catch(Exception ex) { Console.WriteLine("FAIL: "+ex.Message);Environment.ExitCode=1; } }
        static void Run()
        {
            Application.EnableVisualStyles();
            var advanced=(double[])Advanced.Clone();var layers=(string[])Layers.Clone();
            var state=new BeamSideDrawState(null,advanced,layers,new[]{8.0,150,6,200});advanced[0]=999;layers[0]="changed";
            BeamSideTest.Check(state.InitialText[4]==""&&state.InitialText[5]=="","L/V require explicit first input");
            BeamSideDrawRequest captured;
            using(var f=new BeamSideDrawForm(state,"毫米",4,1,false))
            {
                BeamSideTest.Check(Find<ComboBox>(f,"Mode").SelectedIndex==-1,"first opening cannot default a full rectangle to local");
                BeamSideTest.Reject(()=>f.CreateRequest(),"mode required");
                Find<ComboBox>(f,"Mode").SelectedIndex=0;BeamSideTest.Reject(()=>f.CreateRequest(),"blank L/V");
                Find<TextBox>(f,"Parameter4").Text="400";BeamSideTest.Reject(()=>f.CreateRequest(),"blank V");Fill(f);
                captured=f.CreateRequest();BeamSideTest.Near(captured.Parameters.Cover,25);BeamSideTest.Check(captured.Layer(0)==Layers[0],"layer snapshot");
                Render(f,"beam_side_default");
                foreach(int d in new[]{0,1}) foreach(int t in new[]{0,1}) foreach(int b in new[]{0,1}) foreach(int a in new[]{0,1})
                {
                    Find<ComboBox>(f,"Direction").SelectedIndex=d;Find<ComboBox>(f,"TopBend").SelectedIndex=t;
                    Find<ComboBox>(f,"BottomBend").SelectedIndex=b;Find<ComboBox>(f,"AnnotationSide").SelectedIndex=a+1;
                    var r=f.CreateRequest();var sketch=Find<BeamSideSketch>(f,"Sketch");
                    BeamSideTest.Check(r.Direction==(d==0?BeamSideDirection.Right:BeamSideDirection.Left),"direction request");
                    BeamSideTest.Check(r.Parameters.TopUp==(t==1)&&r.Parameters.BottomUp==(b==0)&&r.Parameters.AnnotationRight==(a==0),"independent world choices");
                    BeamSideTest.Check(sketch.ExtendsRight==(d==0)&&sketch.TopUp==(t==1)&&sketch.BottomUp==(b==0)&&sketch.AnnotationRight==(a==0),"sketch follows choices");
                }
                Find<ComboBox>(f,"Mode").SelectedIndex=1;BeamSideTest.Check(f.CreateRequest().FullSection,"full not reinterpreted as local");
                BeamSideTest.Check(Find<Label>(f,"Description").Text.Contains("连接面 X"),"split choice explained");
                Render(f,"beam_side_left");Find<ComboBox>(f,"Direction").SelectedIndex=0;Render(f,"beam_side_right");
                foreach(string bad in new[]{"", "bad", "NaN", "Infinity", "-1", "0"})
                { Find<TextBox>(f,"Parameter4").Text=bad;BeamSideTest.Reject(()=>f.CreateRequest(),"invalid L"); }
                Find<TextBox>(f,"Parameter4").Text="900";BeamSideTest.Near(captured.Parameters.Length,400,"accepted request cannot be overwritten");
                f.Close();BeamSideTest.Check(f.Request==null,"close submits nothing");
            }
            using(var f=new BeamSideDrawForm(State(),"米",6,.001,true))
            {
                Fill(f);BeamSideTest.Reject(()=>f.CreateRequest(),"non-mm explicit choice required");
                Render(f,"beam_side_units_required");
                Find<ComboBox>(f,"Units").SelectedIndex=0;BeamSideTest.Near(f.CreateRequest().DrawingUnitsPerMillimetre,.001);
                Find<ComboBox>(f,"Units").SelectedIndex=1;BeamSideTest.Check(f.CreateRequest().MillimetreOverride,"explicit mm override");
                BeamSideTest.Near(f.CreateRequest().DrawingUnitsPerMillimetre,1);
                f.Show();Application.DoEvents();Find<Button>(f,"Cancel").PerformClick();
                BeamSideTest.Check(f.Request==null&&f.DialogResult==DialogResult.Cancel,"cancel cannot start command");
            }
            using(var f=new BeamSideDrawForm(State(),"unsupported",9,0,true))
            { Fill(f);Find<ComboBox>(f,"Units").SelectedIndex=0;BeamSideTest.Check(f.CreateRequest().MillimetreOverride,"unsupported unit manual interpretation"); }
            using(var f=new BeamSideDrawForm(State(),"Undefined",0,1,false))
            { Fill(f);BeamSideTest.Near(f.CreateRequest().DrawingUnitsPerMillimetre,1); }
            using(var f=new BeamSideDrawForm(State(captured),"Millimeters",4,1,false))
            { BeamSideTest.Near(f.CreateRequest().Parameters.Length,400,"reopen successful record only");f.Show();Application.DoEvents();Find<Button>(f,"Generate").PerformClick();BeamSideTest.Check(f.Request!=null&&f.DialogResult==DialogResult.OK,"button submits"); }
            var target=new object();BeamSideTest.Check(BeamSideDrawRequest.TargetMatches(target,target,4,4),"same document");
            BeamSideTest.Check(!BeamSideDrawRequest.TargetMatches(target,new object(),4,4)&&!BeamSideDrawRequest.TargetMatches(target,target,4,6)&&!BeamSideDrawRequest.TargetMatches(target,null,4,4),"document/unit changes rejected");
            foreach(var direction in new[]{BeamSideDirection.Left,BeamSideDirection.Right}) foreach(bool full in new[]{false,true})
            foreach(bool top in new[]{false,true}) foreach(bool bottom in new[]{false,true}) foreach(bool annotation in new[]{false,true})
            foreach(double scale in new[]{1.0,.001,1/25.4})
            {
                var p=new BeamSideRebarParameters(400,100,topUp:top,bottomUp:bottom,annotationRight:annotation);
                var r=new BeamSideDrawRequest(direction,full,p,scale,4,false,Layers);
                var values=BeamSideDrawRecord.Encode(r);var decoded=BeamSideDrawRecord.Decode(values,Layers);values[7]=999;
                BeamSideTest.Near(decoded.Parameters.Length,400);BeamSideTest.Check(decoded.Direction==direction&&decoded.FullSection==full&&decoded.Parameters.TopUp==top&&decoded.Parameters.BottomUp==bottom&&decoded.Parameters.AnnotationRight==annotation,"record flags roundtrip");
                var g=BeamSideTest.Geometry(x:-900,y:1700,direction:direction,full:full);
                BeamSideDrawRequest snapRequest;var snapshot=BeamSideDrawRecord.DecodeSnapshot(BeamSideDrawRecord.EncodeSnapshot(r,g),Layers,out snapRequest);
                BeamSideTest.Near(snapshot.BasePoint.X,g.BasePoint.X);BeamSideTest.Near(snapshot.BasePoint.Y,g.BasePoint.Y);BeamSideTest.Near(snapshot.Elevation,g.Elevation);
                var unit=new BeamSideUnitTransform(scale);var bp=unit.ToDrawing(g.BasePoint);var point=unit.ToDrawing(g.ToWorld(new ContourPoint(70,120)));
                BeamSideTest.Near(point.X-bp.X,(int)direction*70*scale,"one local X translation");BeamSideTest.Near(point.Y-bp.Y,120*scale,"one local Y translation");
                var layout=BeamSideRebarGeometry.Calculate(g,p);var labels=BeamSideAnnotationGeometry.Calculate(layout,scale);
                BeamSideTest.Check(labels.DotLabel.Text==layout.DotSpecification&&labels.LineLabel.Text==layout.LineSpecification,"consume actual returned specs");
                BeamSideTest.Near(labels.DotLabel.Leaders[0].A.X,layout.DotLabelAnchor.X*scale);BeamSideTest.Near(labels.DotLabel.Leaders[0].A.Y,layout.DotLabelAnchor.Y*scale);
                BeamSideTest.Check(labels.DotLabel.Rightward==annotation,"annotation side independent of mirror");
                BeamSideTest.Check(labels.DotLabel.TextAnchor.Y>labels.DotLabel.Leaders.Last().A.Y,"TEXT above underline");
                BeamSideTest.Near(labels.WidthB.X-labels.WidthA.X,g.Block.Width*scale);BeamSideTest.Near(labels.HeightB.Y-labels.HeightA.Y,g.Block.Height*scale);
                BeamSideTest.Check(labels.LineLabel.TextAnchor.Y-labels.DotLabel.TextAnchor.Y>=1.6*p.TextHeight*scale-1e-8,"line specification strictly above dot specification");
                double mid=(g.Block.Bottom+g.Block.Top)/2;
                BeamSideTest.Check(layout.Dots.All(d=>Math.Abs(layout.DotLabelAnchor.Y-mid)<=Math.Abs(d.Center.Y-mid)+1e-8),"real dot nearest midheight selected before moving line contact");
                foreach(var label in new[]{labels.LineLabel,labels.DotLabel})
                {
                    foreach(var leader in label.Leaders)BeamSideTest.Near(leader.A.Y,leader.B.Y,"every leader horizontal");
                    BeamSideTest.Near(label.TextAnchor.Y-label.Leaders[0].A.Y,Math.Max(4,.08*p.TextHeight)*scale,"text above own horizontal line");
                }
                BeamSideTest.Check(layout.Dots.Any(d=>BeamSideMath.Distance(d.Center,layout.DotLabelAnchor)<1e-8),"actual dot contact");
                BeamSideTest.Near(labels.LineLabel.Leaders[0].A.X,layout.Line[5].X*scale,"line contact on true vertical straight");
                BeamSideTest.Check(labels.LineLabel.HasSlash,"intersection slash retained");
                bool right=direction==BeamSideDirection.Right;double sign=right?1:-1,edge=right?g.Block.Right:g.Block.Left;
                BeamSideTest.Check(sign*(labels.HeightLine.X-edge*scale)>0,"height side follows extension");
                BeamSideTest.Near(labels.HeightA.X,edge*scale,"height extension at outer edge");
                double measuredRight=(edge+sign*1200)*scale;
                var outside=BeamSideAnnotationGeometry.HeightLineOutsideLabels(labels,measuredRight,scale,right);
                BeamSideTest.Check(sign*(outside.X-measuredRight)>=Math.Max(50,1.5*p.TextHeight)*scale-1e-8,"dimension beyond measured TEXT on either side");
                BeamSideTest.Near(outside.Y,labels.HeightLine.Y,"height placement preserves world Y");
                var unchanged=BeamSideAnnotationGeometry.HeightLineOutsideLabels(labels,(edge-sign*900)*scale,scale,right);
                BeamSideTest.Near(unchanged.X,labels.HeightLine.X,"left labels cannot pull dimension towards block");
                BeamSideTest.Check(labels.WidthLine.Y>g.Block.Top*scale,"width above block");
            }
            foreach(var direction in new[]{BeamSideDirection.Left,BeamSideDirection.Right})
            foreach(bool annotation in new[]{false,true}) foreach(double width in new[]{100.0,600.0})
            {
                var p=new BeamSideRebarParameters(400,100,annotationRight:annotation);
                var g=BeamSideTest.Geometry(width:width,height:800,x:450,y:-1700,direction:direction);
                var steel=BeamSideRebarGeometry.Calculate(g,p);var labels=BeamSideAnnotationGeometry.Calculate(steel,1);
                BeamSideTest.Check(steel.DotSpans[0].MinimumAdditionalCount==(width==100?0:2),"narrow zero/wide minimum horizontal dots");
                BeamSideTest.Near(labels.DotLabel.Leaders[0].A.Y,labels.DotLabel.Leaders[0].B.Y,"narrow high actual dot horizontal contact");
            }
            var shortSteel=BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(height:190),new BeamSideRebarParameters(400,100,bottomUp:false,textHeight:200));
            BeamSideTest.Reject(()=>BeamSideAnnotationGeometry.Calculate(shortSteel,1),"impossible label separation rejected, no blank contact");
            foreach(double spacing in new[]{100.0,200.0,1000.0})
            {
                var g=BeamSideTest.Geometry();var p=new BeamSideRebarParameters(400,100,dotSpacing:spacing);
                var r=BeamSideRebarGeometry.Calculate(g,p);var labels=BeamSideAnnotationGeometry.Calculate(r,1);
                BeamSideTest.Check(r.Dots.Any(d=>BeamSideMath.Distance(d.Center,r.DotLabelAnchor)<1e-8),"sparse/dense labels never create or move a dot");
                BeamSideTest.Check(labels.LineLabel.Leaders[0].A.Y>labels.DotLabel.Leaders[0].A.Y,"line row remains above selected sparse/dense dot");
                if(spacing==200) { BeamSideTest.Near(r.DotLabelAnchor.Y,215.114381916836,"300 default dot middle-near real centre");BeamSideTest.Near(r.LineLabelAnchor.Y,320.114381916836,"line contact adjusted upwards"); }
            }
            MirrorAndSideTests();
            foreach(int index in new[]{0,1,2,7,9,11,18,19})
            {
                var values=BeamSideDrawRecord.Encode(captured);values[index]=index==7?0:99;
                if(index==18) values[index]=1.5;
                BeamSideTest.Reject(()=>BeamSideDrawRecord.Decode(values,Layers),"bad record cannot partially restore");
            }
            BeamSideTest.Reject(()=>BeamSideDrawRecord.Decode(new double[19],Layers),"truncated record");
            BeamSideTest.Reject(()=>new BeamSideDrawRequest(BeamSideDirection.Right,false,new BeamSideRebarParameters(400,100),1,40000,false,Layers),"unit code cannot create an unreadable record");
            BeamSideTest.Reject(()=>BeamSideDrawRecord.EncodeSnapshot(captured,BeamSideTest.Geometry(direction:BeamSideDirection.Left)),"snapshot direction mismatch");
            BeamSideTest.Reject(()=>BeamSideDrawRecord.DecodeSnapshot(new double[30],Layers,out captured),"truncated block snapshot");
            BeamSideTest.Check(new[]{"WDT_UPSTAND_LAST","WDT_BEAM_HANGING_LAST","WDT_SLAB_UPSTAND_LAST","WDT_SLAB_HANGING_LAST"}.All(k=>k!=BeamSideDrawRecord.LastKey),"dedicated type key");
            Console.WriteLine("BEAM_SIDE_WINDOW_RECORD_PASS checks="+BeamSideTest.Checks);
        }
        static void MirrorAndSideTests()
        {
            using(var f=new BeamSideDrawForm(State(),"毫米",4,1,false))
            {
                Fill(f);var side=Find<ComboBox>(f,"AnnotationSide");var dir=Find<ComboBox>(f,"Direction");
                BeamSideTest.Check(side.SelectedIndex==0&&!f.CreateRequest().AnnotationSideManual,"fresh automatic");
                dir.SelectedIndex=1;BeamSideTest.Check(!f.CreateRequest().Parameters.AnnotationRight,"auto follows left");
                side.SelectedIndex=1;dir.SelectedIndex=0;dir.SelectedIndex=1;
                var manual=f.CreateRequest();BeamSideTest.Check(manual.AnnotationSideManual&&manual.Parameters.AnnotationRight,"explicit right survives direction switch");
                using(var reopened=new BeamSideDrawForm(State(BeamSideDrawRecord.Decode(BeamSideDrawRecord.Encode(manual),Layers)),"毫米",4,1,false))
                    BeamSideTest.Check(reopened.CreateRequest().AnnotationSideManual&&reopened.CreateRequest().Parameters.AnnotationRight,"manual roundtrip");
                side.SelectedIndex=0;var auto=f.CreateRequest();
                using(var reopened=new BeamSideDrawForm(State(BeamSideDrawRecord.Decode(BeamSideDrawRecord.Encode(auto),Layers)),"毫米",4,1,false))
                { Find<ComboBox>(reopened,"Direction").SelectedIndex=0;BeamSideTest.Check(!reopened.CreateRequest().AnnotationSideManual&&reopened.CreateRequest().Parameters.AnnotationRight,"auto roundtrip follows new direction"); }
                var old=BeamSideDrawRecord.Encode(manual);old[0]=1;old[11]=1;
                var legacy=BeamSideDrawRecord.Decode(old,Layers);
                BeamSideTest.Check(!legacy.AnnotationSideManual&&!legacy.Parameters.AnnotationRight,"legacy right is not presumed manual");
            }
            foreach(double scale in new[]{1.0,.001}) foreach(bool full in new[]{false,true})
            {
                const double cx=450;
                var r=BeamSideAnnotationGeometry.Calculate(BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(x:cx,y:-700,full:full),new BeamSideRebarParameters(400,100)),scale);
                var l=BeamSideAnnotationGeometry.Calculate(BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(x:cx,y:-700,full:full,direction:BeamSideDirection.Left),new BeamSideRebarParameters(400,100,annotationRight:false)),scale);
                foreach(var pair in new[]{new[]{r.LineLabel.TextAnchor,l.LineLabel.TextAnchor},new[]{r.DotLabel.TextAnchor,l.DotLabel.TextAnchor},new[]{r.HeightLine,l.HeightLine},new[]{r.HeightA,l.HeightA},new[]{r.LineLabel.SlashA,l.LineLabel.SlashA}})
                { BeamSideTest.Near(pair[0].X+pair[1].X,2*cx*scale,"mirror X");BeamSideTest.Near(pair[0].Y,pair[1].Y,"mirror Y"); }
                foreach(var pair in new[]{new[]{r.LineLabel,l.LineLabel},new[]{r.DotLabel,l.DotLabel}})
                { BeamSideTest.Check(pair[0].Rightward&&!pair[1].Rightward,"upright text alignment differs"); for(int i=0;i<pair[0].Leaders.Count;i++){ BeamSideTest.Near(pair[0].Leaders[i].A.X+pair[1].Leaders[i].A.X,2*cx*scale);BeamSideTest.Near(pair[0].Leaders[i].B.X+pair[1].Leaders[i].B.X,2*cx*scale); } }
            }
        }
    }
}
