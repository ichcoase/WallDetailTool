using System;
using System.Linq;
using WallDetailTool;

internal static class EndLengthAnnotationTests
{
    static int checks;
    static void Check(bool value,string message) { checks++;if(!value)throw new Exception(message); }
    static void Near(double a,double b,string message) { Check(Math.Abs(a-b)<1e-7,message); }
    static readonly string[] Layers={"L","D","T","M"};
    static void Placement(EndLengthNote n)
    {
        double w=(n.Vertical?.8:2)*n.Height,h=(n.Vertical?2:.8)*n.Height;
        var free=EndLengthAnnotationGeometry.Place(n,w,h,b=>false);
        Near(n.Vertical?free.CenterY:free.CenterX,n.Vertical?(n.A.Y+n.B.Y)/2:(n.A.X+n.B.X)/2,"first text beside segment midpoint");
        double edge=n.Vertical?(n.Normal<0?n.A.X-free.Right:free.Left-n.A.X):(n.Normal<0?n.A.Y-free.Top:free.Bottom-n.A.Y);
        Near(edge,n.Gap+n.StrokeWidth/2,"gap clears actual stroke edge");
        var occupied=free.Expand(n.Height*.1);
        var avoided=EndLengthAnnotationGeometry.Place(n,w,h,b=>b.Overlaps(occupied));
        Check(!avoided.Overlaps(occupied),"measured box avoids existing text/dimension obstacle");
        bool failed=false;try { EndLengthAnnotationGeometry.Place(n,w,h,b=>true); }catch(InvalidOperationException){failed=true;}
        Check(failed,"no free place reports failure");
        Near(n.Rotation,n.Vertical?Math.PI/2:0,"upright conventional reading, no mirrored rotation");
    }
    static void Main()
    {
        try
        {
            foreach(double scale in new[]{1.0,.001,1/25.4})foreach(double height in new[]{20.0,62.5,150})
            {
                foreach(var direction in new[]{CantileverDirection.Left,CantileverDirection.Right})
                foreach(CantileverSupportMode mode in Enum.GetValues(typeof(CantileverSupportMode)))
                {
                    var r=new CantileverDrawRequest(direction,mode,new[]{300.0,180,250,600,0,200,450,12,150,8,200,16,100,6,200},new[]{25.0,12,12,20,height},Layers,scale,4,false);
                    var frame=new CantileverFrame(direction,700,-800,300);var p=r.Inputs(1500,200);
                    var steel=CantileverRebarGeometry.Calculate(frame,r.Specifications,p);
                    var before=steel.Bars.SelectMany(b=>b.Vertices).SelectMany(v=>new[]{v.Point.X,v.Point.Y,v.Bulge}).ToArray();
                    var notes=EndLengthAnnotationGeometry.Cantilever(frame,r,p,steel);
                    bool noInner=mode==CantileverSupportMode.NoInnerUp||mode==CantileverSupportMode.NoInnerDown;
                    Check(notes.Count==(noInner?1:0),"only no-inner support V, never auto F or coincident duplicates");
                    if(noInner)
                    {
                        var n=notes.Single();Check(n.Text=="250","V projection, not V-R or auto F");
                        Near(Math.Abs(n.A.Y-n.B.Y),(p.V-p.Radius)*scale,"placement uses actual trimmed straight");
                        Near(n.A.X,steel.Bars[0].Vertices[0].Point.X*scale,"world coordinate converted once");
                        Near(n.Height,height*scale,"cantilever height units");Near(n.Normal,-(int)direction,"cantilever outside support side");
                        Check(n.Vertical,"vertical support note");Placement(n);
                    }
                    Check(before.SequenceEqual(steel.Bars.SelectMany(b=>b.Vertices).SelectMany(v=>new[]{v.Point.X,v.Point.Y,v.Bulge})),"cantilever path untouched");
                }
                foreach(var dir in new[]{BeamSideDirection.Left,BeamSideDirection.Right})
                foreach(bool top in new[]{false,true})foreach(bool bottom in new[]{false,true})
                {
                    var g=BeamSideTest.Geometry(x:1700,y:-300,direction:dir);
                    var p=new BeamSideRebarParameters(400,100,topUp:top,bottomUp:bottom,textHeight:height);
                    var steel=BeamSideRebarGeometry.Calculate(g,p);var notes=EndLengthAnnotationGeometry.BeamSide(steel,scale);
                    Check(notes.Count==2,"both equal V ends each shown once");
                    for(int i=0;i<2;i++)
                    {
                        var n=notes[i];Check(n.Text=="100","beam V is straight, not L or V+R");
                        Near(Math.Abs(n.A.Y-n.B.Y),100*scale,"actual V straight span");
                        Near(n.A.X,steel.Line[i==0?0:10].X*scale,"beam mirrored coordinate units");
                        Near(n.Height,height*scale,"beam height units");Near(n.Normal,-(int)dir,"beam outside tail side");
                        Check(n.Vertical,"beam vertical");Placement(n);
                    }
                }
                foreach(bool hanging in new[]{false,true})
                {
                    var g=new SlabUpstandGeometry { Left=400,Right=700,Top=700,SlabTop=300,SlabBottom=0 };
                    var p=new SlabUpstandRebarParameters { LeftLength=200,RightLength=312.5 };
                    var steel=SlabUpstandRebarGeometry.Calculate(g,p);
                    if(hanging)NodeOrientation.Reflect(steel.Line,steel.LeftDots,steel.RightDots,steel.TopDots,steel.CornerDots);
                    var before=steel.Line.SelectMany(v=>new[]{v.X,v.Y,v.Bulge}).ToArray();
                    var notes=EndLengthAnnotationGeometry.Slab(steel,p,hanging,height,scale);
                    Check(notes.Count==2,"left and right labels remain distinct");
                    for(int i=0;i<2;i++)
                    {
                        var n=notes[i];Check(n.Text==(i==0?"200":"312.5"),"corresponding horizontal L without arc or trailing zeros");
                        Near(Math.Abs(n.A.X-n.B.X),i==0?200:312.5,"slab true tangent-to-end span");
                        Near(n.A.Y,steel.Line[i==0?0:8].Y,"slab already-generated coordinates not scaled twice");
                        Near(n.Height,height*scale,"new slab TEXT height uses current units");
                        Near(n.Normal,hanging?1:-1,"slab down/up mirror side");Check(!n.Vertical,"slab text horizontal");Placement(n);
                    }
                    Check(before.SequenceEqual(steel.Line.SelectMany(v=>new[]{v.X,v.Y,v.Bulge})),"slab geometry not changed by notes");
                }
            }
            Console.WriteLine("END_LENGTH_ANNOTATION_PASS checks="+checks);
        }
        catch(Exception ex) { Console.WriteLine(ex);Environment.ExitCode=1; }
    }
}
