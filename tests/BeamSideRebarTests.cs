using System;
using System.Linq;
namespace WallDetailTool
{
    internal static class BeamSideRebarTests
    {
        static void Main() { try { Run(); } catch(Exception ex) { Console.WriteLine("FAIL: "+ex.Message);Environment.ExitCode=1; } }
        static void Run()
        {
            foreach(var direction in new[]{BeamSideDirection.Right,BeamSideDirection.Left})
            foreach(bool top in new[]{false,true})
            foreach(bool bottom in new[]{false,true})
            foreach(var origin in new[]{new ContourPoint(0,0),new ContourPoint(1234,-789),new ContourPoint(-1500,876)})
            foreach(bool full in new[]{false,true})
            {
                var g=BeamSideTest.Geometry(x:origin.X,y:origin.Y,direction:direction,full:full);
                var p=new BeamSideRebarParameters(400,100,topUp:top,bottomUp:bottom);
                var r=BeamSideRebarGeometry.Calculate(g,p);
                BeamSideTest.Check(r.Line.Count==12,"continuous polyline vertex count");
                BeamSideTest.Check(r.BoundaryStatus==(full?BeamSideBoundaryStatus.Checked:BeamSideBoundaryStatus.Unknown),"boundary status");
                BeamSideTest.Check(r.BoundaryMessage.Length>0,"boundary message");
                BeamSideTest.Near(r.TopInsideLength,400);BeamSideTest.Near(r.BottomInsideLength,400);
                double a=400-100-Math.PI*20/2,s=(int)direction;
                var v=r.Line.Select(q=>g.ToLocal(q.Point)).ToArray();
                BeamSideTest.Near(v[0].X,-a-20);BeamSideTest.Near(v[0].Y,569+(top?120:-120));
                BeamSideTest.Near(v[11].Y,31+(bottom?120:-120));
                BeamSideTest.Near(v[3].X,0);BeamSideTest.Near(v[8].X,0);
                BeamSideTest.Near(r.HorizontalDepth,a+20);BeamSideTest.Near(r.HorizontalInsideLength,a);
                foreach(int i in new[]{1,4,6,9})
                {
                    BeamSideTest.Near(Math.Abs(r.Line[i].Bulge),Math.Tan(Math.PI/8),"quarter bulge");
                    double dx=r.Line[i+1].X-r.Line[i].X,dy=r.Line[i+1].Y-r.Line[i].Y;
                    BeamSideTest.Near(Math.Sqrt(dx*dx+dy*dy)/Math.Sqrt(2),20,"independent arc radius");
                    var arc=new BeamSideCurve(r.Line[i].Point,r.Line[i+1].Point,r.Line[i].Bulge);
                    var prev=r.Line[i-1].Point;var after=r.Line[i+2].Point;
                    double cross0=(r.Line[i].X-prev.X)*(r.Line[i].X-arc.Center.X)+(r.Line[i].Y-prev.Y)*(r.Line[i].Y-arc.Center.Y);
                    double cross1=(after.X-r.Line[i+1].X)*(r.Line[i+1].X-arc.Center.X)+(after.Y-r.Line[i+1].Y)*(r.Line[i+1].Y-arc.Center.Y);
                    BeamSideTest.Near(cross0,0,"start tangent dot product");BeamSideTest.Near(cross1,0,"end tangent dot product");
                }
                BeamSideTest.Near(r.Line[1].Bulge,s*(top?1:-1)*Math.Tan(Math.PI/8));
                BeamSideTest.Near(r.Line[9].Bulge,-s*(bottom?1:-1)*Math.Tan(Math.PI/8));
                BeamSideTest.Near(r.WidthB.X-r.WidthA.X,300);BeamSideTest.Near(r.HeightB.Y-r.HeightA.Y,600);
                BeamSideTest.Check(r.Dots.Any(d=>BeamSideMath.Distance(d.Center,r.DotLabelAnchor)<1e-8),"leader targets a real dot");
                BeamSideTest.Near(r.LineLabelAnchor.X,r.Line[5].X,"line leader on actual vertical run");
                BeamSideTest.Check(r.LineSpecification=="%%1328@150" && r.DotSpecification=="%%1326@200","specifications");
                BeamSideTest.Near(r.Elevation,37);
                BeamSideTest.Near(g.ToLocal(g.ToWorld(new ContourPoint(-10,44))).Y,44,"coordinate roundtrip");
            }
            var unknown=BeamSideTest.Geometry();
            foreach(double bad in new[]{0.0,-1.0,Double.NaN,Double.PositiveInfinity})
                BeamSideTest.Reject(()=>new BeamSideRebarParameters(bad,100),"invalid L");
            BeamSideTest.Reject(()=>BeamSideRebarGeometry.Calculate(unknown,new BeamSideRebarParameters(100,100)),"a negative");
            BeamSideTest.Reject(()=>BeamSideRebarGeometry.Calculate(unknown,new BeamSideRebarParameters(100+Math.PI*10,100)),"a zero");
            BeamSideTest.Reject(()=>BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(height:302),new BeamSideRebarParameters(400,100)),"tail endpoints touch");
            BeamSideTest.Reject(()=>BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(height:290),new BeamSideRebarParameters(400,100)),"tail overlap");
            BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(height:302.001),new BeamSideRebarParameters(400,100));
            BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(height:190),new BeamSideRebarParameters(400,100,bottomUp:false));
            // With a narrower display, same-direction tails can pass the other
            // horizontal elevation without hitting its arc or display contour.
            BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(height:167),new BeamSideRebarParameters(400,100,lineDisplayWidth:2,bottomUp:false));
            double displayCritical=151+11+Math.Sqrt(26*26-14*14);
            BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(height:displayCritical+1e-4),new BeamSideRebarParameters(400,100,bottomUp:false));
            BeamSideTest.Reject(()=>BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(height:displayCritical-1e-4),new BeamSideRebarParameters(400,100,bottomUp:false)),"arc/flat-cap display critical");
            BeamSideTest.Reject(()=>BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(height:172),new BeamSideRebarParameters(400,100,bottomUp:false)),"display stroke meets opposite arc");
            BeamSideTest.Reject(()=>BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(height:162),new BeamSideRebarParameters(400,100,bottomUp:false)),"tail touches opposite tail");

            foreach(var dir in new[]{BeamSideDirection.Right,BeamSideDirection.Left})
            foreach(double y in new[]{-1700.0,430.0})
            {
                var equal=BeamSideTest.Geometry(x:-800,y:y,direction:dir,full:true,depth:400,margin:0);
                BeamSideRebarGeometry.Calculate(equal,new BeamSideRebarParameters(400,100));
                BeamSideTest.Reject(()=>BeamSideRebarGeometry.Calculate(equal,new BeamSideRebarParameters(400,100,topUp:true)),"upper outward beyond beam");
                BeamSideTest.Reject(()=>BeamSideRebarGeometry.Calculate(equal,new BeamSideRebarParameters(400,100,bottomUp:false)),"lower outward beyond beam");
                double exactDepth=400-100-Math.PI*10+20+6;
                BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(x:900,y:y,direction:dir,full:true,depth:exactDepth,margin:0),new BeamSideRebarParameters(400,100));
                BeamSideTest.Reject(()=>BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(x:900,y:y,direction:dir,full:true,depth:exactDepth-1e-4,margin:0),new BeamSideRebarParameters(400,100)),"display back boundary critical");
            }
            var original=BeamSideRebarGeometry.Calculate(unknown,new BeamSideRebarParameters(400,100));
            var textOnly=BeamSideRebarGeometry.Calculate(unknown,new BeamSideRebarParameters(400,100,lineDiameter:32,dotDiameter:16,annotationRight:false));
            BeamSideTest.Check(textOnly.DotSpecification=="%%13216@200","actual dot diameter independent");
            for(int i=0;i<original.Line.Count;i++) { BeamSideTest.Near(original.Line[i].X,textOnly.Line[i].X);BeamSideTest.Near(original.Line[i].Y,textOnly.Line[i].Y); }
            BeamSideTest.Near(original.Dots.Count,textOnly.Dots.Count);
            Console.WriteLine("BEAM_SIDE_REBAR_PASS checks="+BeamSideTest.Checks);
        }
    }
}
