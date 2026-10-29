using System;
using System.Linq;
using System.Windows.Forms;
using WallDetailTool;
internal static class RectangleInputTests
{
    static int checks;
    static void Check(bool v,string m) { checks++;if(!v)throw new Exception(m); }
    static void Near(double a,double b) { Check(Math.Abs(a-b)<1e-7,"coordinate mismatch"); }
    static void Reject(Action a) { bool bad=false;try{a();}catch(InvalidOperationException){bad=true;}Check(bad,"expected rejection"); }
    static T Find<T>(Form f,string n) where T:Control { return (T)f.Controls.Find(n,true).Single(); }
    static readonly string[] Layers={"L","D","T","M"};
    static void View(Form f,string name)
    {
        f.Show();Application.DoEvents();
        foreach(string controlName in new[]{"InputMethod","Generate"})
        {
            var c=f.Controls.Find(controlName,true).Single();
            var box=f.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
            Check(f.ClientRectangle.Contains(box),"new input and generate button visible");
        }
        using(var bmp=new System.Drawing.Bitmap(f.Width,f.Height))
        { f.DrawToBitmap(bmp,new System.Drawing.Rectangle(0,0,f.Width,f.Height));bmp.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name+".png")); }
        f.Hide();
    }
    [STAThread]static void Main()
    {
        try
        {
            foreach(double scale in new[]{1.0,.001,1/25.4})foreach(double x in new[]{0.0,-1700,1234})
            foreach(double y in new[]{-900.0,400})foreach(bool reverse in new[]{false,true})foreach(bool diagonal in new[]{false,true})
            {
                var a=new RectanglePoint(x*scale,(y+(diagonal?600:0))*scale,37*scale);
                var b=new RectanglePoint((x+300)*scale,(y+(diagonal?0:600))*scale,37*scale);
                var r=RectangleInputGeometry.Create(reverse?b:a,reverse?a:b,scale);
                Near(r.Left,x);Near(r.Bottom,y);Near(r.Top,y+600);Near(r.Elevation,37);
                var source=BeamSideTest.Rectangle(x,x+300,y,y+600);
                foreach(bool down in new[]{false,true})
                {
                    var segments=down?NodeOrientation.Reflect(source):source;
                    var actual=r.Beam(down);var expected=UpstandRecognizer.Recognize(segments,37).Single();
                    Check(actual.InputKind==UpstandInputKind.ClosedRectangle,"local beam only");
                    Near(NodeOrientation.Y(actual.BeamTop,down),down?y+600:y);
                    var p=new UpstandRebarParameters { LeftAnchor=300,RightAnchor=350 };
                    var aa=UpstandRebarGeometry.Calculate(actual,p);var bb=UpstandRebarGeometry.Calculate(expected,p);
                    Check(aa.Line.SelectMany(v=>new[]{v.X,v.Y,v.Bulge}).SequenceEqual(bb.Line.SelectMany(v=>new[]{v.X,v.Y,v.Bulge})),"beam calculation identical to existing rectangle");
                    var slab=r.Slab(down,200);var old=SlabUpstandRecognizer.Recognize(segments,37,200).Single();
                    Check(!slab.HasSlabBoundaries&&slab.ThicknessIsManual&&slab.InputKind!=UpstandInputKind.FullSection,"no invented slab boundaries");
                    var sp=new SlabUpstandRebarParameters();
                    var sa=SlabUpstandRebarGeometry.Calculate(slab,sp);var sb=SlabUpstandRebarGeometry.Calculate(old,sp);
                    Check(sa.Line.SelectMany(v=>new[]{v.X,v.Y,v.Bulge}).SequenceEqual(sb.Line.SelectMany(v=>new[]{v.X,v.Y,v.Bulge})),"slab calculation identical");
                }
                foreach(var dir in new[]{BeamSideDirection.Left,BeamSideDirection.Right})
                {
                    var side=r.Side(dir);var old=BeamSideContourRecognizer.Recognize(source,37,dir,false).Candidates.Single();
                    Near(side.ConnectionX,dir==BeamSideDirection.Right?x:x+300);
                    Check(side.Beam==null&&side.InputKind==BeamSideInputKind.LocalRectangle,"no invented beam boundary");
                    var p=new BeamSideRebarParameters(400,100);var sa=BeamSideRebarGeometry.Calculate(side,p);var sb=BeamSideRebarGeometry.Calculate(old,p);
                    Check(sa.Line.SelectMany(v=>new[]{v.X,v.Y,v.Bulge}).SequenceEqual(sb.Line.SelectMany(v=>new[]{v.X,v.Y,v.Bulge})),"side calculation identical");
                    Check(sa.BoundaryStatus==BeamSideBoundaryStatus.Unknown,"unknown support boundary");
                    foreach(CantileverSupportMode mode in Enum.GetValues(typeof(CantileverSupportMode)))
                    {
                        var request=new CantileverDrawRequest((CantileverDirection)(int)dir,mode,new[]{300.0,180,250,600,0,200,450,12,150,8,200,16,100,6,200},new[]{25.0,12,12,20,62.5},Layers,scale,4,false,true);
                        var cf=new CantileverFrame(request.Direction,side.ConnectionX,side.Block.Top,300);
                        var of=new CantileverFrame(request.Direction,old.ConnectionX,old.Block.Top,300);
                        var ip=request.Inputs(side.Block.Width,side.Block.Height);
                        var ac=CantileverRebarGeometry.Calculate(cf,request.Specifications,ip);var oc=CantileverRebarGeometry.Calculate(of,request.Specifications,request.Inputs(old.Block.Width,old.Block.Height));
                        Check(ac.Bars.SelectMany(t=>t.Vertices).SelectMany(v=>new[]{v.Point.X,v.Point.Y,v.Bulge}).SequenceEqual(oc.Bars.SelectMany(t=>t.Vertices).SelectMany(v=>new[]{v.Point.X,v.Point.Y,v.Bulge})),"cantilever identical");
                        Near(ac.EffectiveF,oc.EffectiveF);
                        var ad=CantileverDots.Calculate(cf,ac,ip,12,200);var od=CantileverDots.Calculate(of,oc,ip,12,200);
                        Check(ad.Upper.Select(t=>t.X).SequenceEqual(od.Upper.Select(t=>t.X)),"dot coordinates identical");
                    }
                }
            }
            RectangleInputGeometry empty;
            Check(!RectangleInputGeometry.TryCreate(null,null,1,out empty)&&empty==null,"first cancel no geometry");
            Check(!RectangleInputGeometry.TryCreate(new RectanglePoint(0,0,0),null,1,out empty)&&empty==null,"second cancel no geometry");
            foreach(var b in new[]{new RectanglePoint(0,0,0),new RectanglePoint(.001,200,0),new RectanglePoint(200,200,1),new RectanglePoint(double.NaN,200,0)})
                Reject(()=>RectangleInputGeometry.Create(new RectanglePoint(0,0,0),b,1));
            Reject(()=>RectangleInputGeometry.Create(new RectanglePoint(-1e308,0,0),new RectanglePoint(1e308,200,0),1));
            RectangleInputGeometry.ValidateAxes(1,0,0,0,1,0,0,0,1,0);
            Reject(()=>RectangleInputGeometry.ValidateAxes(0,1,0,-1,0,0,0,0,1,0));
            Reject(()=>RectangleInputGeometry.ValidateAxes(1,0,0,0,1,0,0,1,1,0));
            Reject(()=>RectangleInputGeometry.ValidateAxes(1,0,0,0,1,0,0,0,1,.2));
            var target=new object();Check(RectangleInputGeometry.TargetMatches(target,target,4,4),"target match");
            Check(!RectangleInputGeometry.TargetMatches(target,new object(),4,4),"document switched");
            Check(!RectangleInputGeometry.TargetMatches(target,target,4,6),"unit switched");
            Windows();Console.WriteLine("RECTANGLE_INPUT_PASS checks="+checks);
        }
        catch(Exception e) { Console.WriteLine(e);Environment.ExitCode=1; }
    }
    static void Windows()
    {
        var adv=new[]{25.0,12,12,20,62.5};
        foreach(bool slab in new[]{false,true})foreach(bool down in new[]{false,true})
        {
            var values=slab?new[]{"8","150","6","200","200","31","300","350"}:new[]{"8","150","6","200","300","350"};
            using(var f=new NodeDrawForm(new NodeDrawState(slab,down,values,values),adv,adv,4))
            {
                var c=Find<ComboBox>(f,"InputMethod");Check(c.SelectedIndex==0&&!f.CreateRequest().TwoPointRectangle,"node existing default");
                c.SelectedIndex=1;var r=f.CreateRequest();Check(r.TwoPointRectangle&&r.Hanging==down&&r.IsSlab==slab&&r.UnitCode==4,"immutable mode captured");
                c.SelectedIndex=0;Check(r.TwoPointRectangle&&!f.CreateRequest().TwoPointRectangle,"subsequent UI cannot alter request");
                c.SelectedIndex=1;
                View(f,slab?"rectangle-slab":"rectangle-beam");
                if(slab) { Find<TextBox>(f,"Parameter4").Text="";Reject(()=>f.CreateRequest()); }
                f.Close();Check(f.Request==null,"close no request");
            }
        }
        using(var f=new BeamSideDrawForm(new BeamSideDrawState(null,adv,Layers,new[]{8.0,150,6,200}),"mm",4,1,false))
        {
            var mode=Find<ComboBox>(f,"Mode");mode.SelectedIndex=1;
            Find<TextBox>(f,"Parameter4").Text="400";Find<TextBox>(f,"Parameter5").Text="100";
            var input=Find<ComboBox>(f,"InputMethod");input.SelectedIndex=1;
            Check(!mode.Enabled&&mode.SelectedIndex==0,"full mode explicitly disabled");
            var r=f.CreateRequest();Check(r.TwoPointRectangle&&!r.FullSection,"local request");
            input.SelectedIndex=0;Check(mode.Enabled&&mode.SelectedIndex==1,"contour mode draft restored");
            View(f,"rectangle-side");
            f.Close();Check(f.Request==null,"beam window close no request");
        }
        using(var f=new CantileverDrawForm(new CantileverDrawState(null,adv,Layers,new[]{8.0,150,6,200}),"mm",4,1,false))
        {
            Check(!f.CreateRequest().TwoPointRectangle,"cantilever existing default");
            Find<ComboBox>(f,"InputMethod").SelectedIndex=1;
            foreach(int mode in new[]{0,1,2,3}) { Find<ComboBox>(f,"Mode").SelectedIndex=mode;Check(f.CreateRequest().TwoPointRectangle,"cantilever mode retained"); }
            View(f,"rectangle-cantilever");
            f.Close();Check(f.Request==null,"cantilever close no request");
        }
    }
}
