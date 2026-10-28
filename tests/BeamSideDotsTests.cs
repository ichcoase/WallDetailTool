using System;
using System.Linq;
using System.Globalization;
namespace WallDetailTool
{
    internal static class BeamSideDotsTests
    {
        const double E=1e-7;
        // Independent interval propagation from a free near-beam centre.
        static bool HorizontalFeasible(ContourPoint c,double y,double max,double d,double s,int n)
        {
            if(n==0)return c.X-d/2<=s+E;
            double lo=d/2,hi=Math.Min(max,d/2+s);
            if(lo>hi+E)return false;
            for(int i=1;i<n;i++){lo+=d;hi=Math.Min(max,hi+s);if(lo>hi+E)return false;}
            double h=Math.Abs(c.Y-y);if(h>s)return false;
            return Math.Max(lo,c.X-Math.Sqrt(s*s-h*h))<=Math.Min(hi,c.X-Math.Sqrt(Math.Max(0,d*d-h*h)))+E;
        }
        static bool VerticalFeasible(ContourPoint a,ContourPoint b,double x,double low,double high,double d,double s,int n)
        {
            if(n==0)return BeamSideMath.Distance(a,b)>=d-E&&BeamSideMath.Distance(a,b)<=s+E;
            double ha=Math.Abs(a.X-x),hb=Math.Abs(b.X-x);if(ha>s||hb>s)return false;
            double lo=Math.Max(low,-a.Y+Math.Sqrt(Math.Max(0,d*d-ha*ha))),hi=Math.Min(high,-a.Y+Math.Sqrt(s*s-ha*ha));
            if(lo>hi+E)return false;
            for(int i=1;i<n;i++){lo=Math.Max(low,lo+d);hi=Math.Min(high,hi+s);if(lo>hi+E)return false;}
            return Math.Max(lo,-b.Y-Math.Sqrt(s*s-hb*hb))<=Math.Min(hi,-b.Y-Math.Sqrt(Math.Max(0,d*d-hb*hb)))+E;
        }
        static void Verify(double w,double height,double radius,double s,double d=12)
        {
            double off=(12+d)/2,dr=d/2;
            var r=BeamSideDots.Calculate(w,height,25,12,d,radius,s);
            double top=height-31,bottom=31,outer=w-31,cx=outer-radius,rho=radius-off;
            var upper=r.Dots.Single(p=>p.Role==BeamSideDotRole.UpperCorner).Center;
            var lower=r.Dots.Single(p=>p.Role==BeamSideDotRole.LowerCorner).Center;
            BeamSideTest.Near(BeamSideMath.Distance(upper,new ContourPoint(cx,top-radius)),rho,"upper circular tangency");
            BeamSideTest.Near(BeamSideMath.Distance(lower,new ContourPoint(cx,bottom+radius)),rho,"lower circular tangency");
            BeamSideTest.Check(r.Dots.Count<=10000&&r.Spans.Count==3,"three open spans");
            for(int i=0;i<r.Dots.Count;i++)
            {
                var dot=r.Dots[i];var p=dot.Center;
                BeamSideTest.Check(p.X>=dr-E&&p.X<=w-dr+E&&p.Y>=dr-E&&p.Y<=height-dr+E,"inside concrete");
                if(dot.Role==BeamSideDotRole.UpperHorizontal){BeamSideTest.Near(p.Y,top-off);BeamSideTest.Check(p.X<=cx+E,"upper true straight");}
                if(dot.Role==BeamSideDotRole.LowerHorizontal){BeamSideTest.Near(p.Y,bottom+off);BeamSideTest.Check(p.X<=cx+E,"lower true straight");}
                if(dot.Role==BeamSideDotRole.OuterVertical){BeamSideTest.Near(p.X,outer-off);BeamSideTest.Check(p.Y>=bottom+radius-E&&p.Y<=top-radius+E,"outer true straight");}
                for(int j=0;j<i;j++)BeamSideTest.Check(BeamSideMath.Distance(p,r.Dots[j].Center)>=d-E,"no duplicates or overlap");
                if(i>0)BeamSideTest.Check(BeamSideMath.Distance(p,r.Dots[i-1].Center)<=s+E,"actual adjacent distance");
                BeamSideTest.Check(r.Dots.Any(q=>Math.Abs(q.Center.X-p.X)<E&&Math.Abs(q.Center.Y-(height-p.Y))<E),"symmetric");
            }
            BeamSideTest.Check(r.Dots.First().Center.X-dr<=s+E&&r.Dots.Last().Center.X-dr<=s+E,"boundary to actual nearest dot edge");
            for(int side=0;side<3;side++)
            {
                var span=r.Spans[side];int count=span.MinimumAdditionalCount;
                BeamSideTest.Check(span.Last-span.First-(side==1?1:0)==count,"span counts without virtual endpoints");
                for(int n=0;n<count;n++)BeamSideTest.Check(side==1?!VerticalFeasible(upper,lower,outer-off,-(top-radius),-(bottom+radius),d,s,n):!HorizontalFeasible(upper,top-off,cx,d,s,n),"fewer dots infeasible after relocating every free point");
            }
        }
        static void Main(){try{Run();}catch(Exception ex){Console.WriteLine("FAIL: "+ex.Message);Environment.ExitCode=1;}}
        static void Report300()
        {
            var g=BeamSideTest.Geometry(width:300,height:600);
            var r=BeamSideRebarGeometry.Calculate(g,new BeamSideRebarParameters(400,100));
            var top=r.Dots.Single(d=>d.Role==BeamSideDotRole.UpperCorner).Center;
            var bottom=r.Dots.Single(d=>d.Role==BeamSideDotRole.LowerCorner).Center;
            double expectedX=249+8/Math.Sqrt(2),expectedY=549+8/Math.Sqrt(2);
            BeamSideTest.Near(top.X,expectedX,"300 corner independent circle geometry");
            BeamSideTest.Near(top.Y,expectedY);BeamSideTest.Near(bottom.Y,600-expectedY);
            foreach(int side in new[]{0,2})
            {
                var span=r.DotSpans[side];var corner=side==0?top:bottom;
                var added=r.Dots.Where(d=>d.Role==(side==0?BeamSideDotRole.UpperHorizontal:BeamSideDotRole.LowerHorizontal)).ToArray();
                BeamSideTest.Check(added.Length==1&&span.MinimumAdditionalCount==1,"300 needs exactly one added dot per horizontal");
                var p=added.Single().Center;
                BeamSideTest.Near(p.X,(expectedX+6)/2,"deterministic free horizontal placement");
                double gap=Math.Sqrt((p.X-corner.X)*(p.X-corner.X)+(p.Y-corner.Y)*(p.Y-corner.Y));
                BeamSideTest.Check(gap<=200&&p.X-6<=200&&expectedX-6>200,"actual spacing and boundary, zero not feasible");
                Console.WriteLine(String.Format(CultureInfo.InvariantCulture,
                    "CASE300 {0} connectionX={1:F9} corner=({2:F9},{3:F9}) D={4:F9} added=1 center=({5:F9},{6:F9}) actualSpacing={7:F9} finalClearance={8:F9}",
                    side==0?"upper":"lower",g.ConnectionX,corner.X,corner.Y,corner.X-g.ConnectionX-6,p.X,p.Y,gap,p.X-g.ConnectionX-6));
            }
            Console.WriteLine("CASE300 units=WCS-mm displayDiameter=12 R=20 lineDisplayWidth=12 cover=25 spacing=200");
            for(int i=0;i<r.Dots.Count;i++)
            {
                var p=r.Dots[i].Center;
                double distance=i==0?0:Math.Sqrt(Math.Pow(p.X-r.Dots[i-1].Center.X,2)+Math.Pow(p.Y-r.Dots[i-1].Center.Y,2));
                Console.WriteLine(String.Format(CultureInfo.InvariantCulture,"CASE300 path[{0}] role={1} center=({2:F9},{3:F9}) previousDistance={4:F9}",i,r.Dots[i].Role,p.X,p.Y,distance));
            }
        }
        static void Run()
        {
            Report300();
            foreach(double w in new[]{57.0,70.0,100.0,300.0,600.0})
            foreach(double h in new[]{160.0,300.0,600.0,1000.0})
            foreach(double r in new[]{12.0,20.0})
            foreach(double s in new[]{35.0,50.0,99.9,150.0,200.0,250.0,1000.0})Verify(w,h,r,s);
            Verify(49,600,12,100);Verify(150,600,50,50);Verify(600,1000,100,100);
            foreach(double diameter in new[]{4.0,8.0,20.0,28.0})
            foreach(double width in new[]{100.0,300.0,600.0})
            foreach(double spacing in new[]{50.0,200.0,550.0}) Verify(width,600,20,spacing,diameter);
            foreach(double diameter in new[]{4.0,20.0,28.0})
            {
                double rho=20-(12+diameter)/2,k=rho/Math.Sqrt(2),perpendicular=rho-k;
                double zeroThreshold=200+diameter/2+51-k;
                double nextThreshold=diameter/2+200+Math.Sqrt(200*200-perpendicular*perpendicular)+51-k;
                foreach(double epsilon in new[]{-1e-4,0.0,1e-4})
                {
                    Verify(zeroThreshold+epsilon,600,20,200,diameter);
                    Verify(nextThreshold+epsilon,600,20,200,diameter);
                    BeamSideTest.Check(BeamSideDots.Calculate(zeroThreshold+epsilon,600,25,12,diameter,20,200).Spans[0].MinimumAdditionalCount==(epsilon<=0?0:1),"display-specific zero threshold");
                    BeamSideTest.Check(BeamSideDots.Calculate(nextThreshold+epsilon,600,25,12,diameter,20,200).Spans[0].MinimumAdditionalCount==(epsilon<=0?1:2),"display-specific next threshold");
                }
            }
            foreach(double w in new[]{57-1e-4,57.0,57+1e-4,100.0})
            {
                var r=BeamSideDots.Calculate(w,600,25,12,12,20,200);
                BeamSideTest.Check(r.Spans[0].MinimumAdditionalCount==0&&r.Spans[2].MinimumAdditionalCount==0,"zero horizontal dots, corners retained");
                BeamSideTest.Check(r.Dots.Count(p=>p.Role==BeamSideDotRole.UpperCorner||p.Role==BeamSideDotRole.LowerCorner)==2,"two unique corners, no endpoint merge");
            }
            double critical=200+6+31+20-8/Math.Sqrt(2);
            var exact=BeamSideDots.Calculate(critical,600,25,12,12,20,200);
            var over=BeamSideDots.Calculate(critical+1e-4,600,25,12,12,20,200);
            BeamSideTest.Check(exact.Spans[0].MinimumAdditionalCount==0&&over.Spans[0].MinimumAdditionalCount==1,"zero-to-one boundary threshold");
            BeamSideTest.Check(over.Dots[0].Center.X>7,"free dot not forced flush");
            Verify(critical-1e-4,600,20,200);Verify(critical,600,20,200);Verify(critical+1e-4,600,20,200);
            double dy=8-8/Math.Sqrt(2),two=6+200+Math.Sqrt(200*200-dy*dy)+31+20-8/Math.Sqrt(2);
            BeamSideTest.Check(BeamSideDots.Calculate(two,600,25,12,12,20,200).Spans[0].MinimumAdditionalCount==1,"one-to-two exact reach threshold");
            BeamSideTest.Check(BeamSideDots.Calculate(two+1e-4,600,25,12,12,20,200).Spans[0].MinimumAdditionalCount==2,"one-to-two strict threshold");
            Verify(two-1e-4,600,20,200);Verify(two,600,20,200);Verify(two+1e-4,600,20,200);
            BeamSideTest.Reject(()=>BeamSideDots.Calculate(300,600,25,12,12,12-1e-4,200),"R too small");
            BeamSideTest.Reject(()=>BeamSideDots.Calculate(300,600,25,12,12,20,12-1e-4),"spacing below display diameter");
            BeamSideTest.Reject(()=>BeamSideDots.Calculate(300,600,25,12,12,100,35),"unreachable arc transition");
            BeamSideTest.Reject(()=>BeamSideDots.Calculate(300,600,25,12,12,20,Double.PositiveInfinity),"nonfinite");
            var open=BeamSideDots.Calculate(100,600,25,12,12,20,200);
            BeamSideTest.Check(BeamSideMath.Distance(open.Dots.First().Center,open.Dots.Last().Center)>200,"connection face not filled");
            foreach(double w in new[]{57.0,100.0,300.0,600.0})
            {
                var left=BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(width:w,x:-900,y:-800,direction:BeamSideDirection.Left),new BeamSideRebarParameters(400,100));
                var right=BeamSideRebarGeometry.Calculate(BeamSideTest.Geometry(width:w,x:-900,y:-800),new BeamSideRebarParameters(400,100));
                BeamSideTest.Check(left.Dots.Count==right.Dots.Count,"mirrored counts");
                for(int i=0;i<left.Dots.Count;i++){BeamSideTest.Near(left.Geometry.ToLocal(left.Dots[i].Center).X,right.Geometry.ToLocal(right.Dots[i].Center).X);BeamSideTest.Near(left.Dots[i].Center.Y,right.Dots[i].Center.Y);}
            }
            foreach(double diameter in new[]{4.0,12.0,28.0}) foreach(var direction in new[]{BeamSideDirection.Left,BeamSideDirection.Right})
            foreach(var origin in new[]{new ContourPoint(450,-1700),new ContourPoint(-900,730)})
            {
                var g=BeamSideTest.Geometry(x:origin.X,y:origin.Y,direction:direction);
                var r=BeamSideRebarGeometry.Calculate(g,new BeamSideRebarParameters(400,100,dotDisplayDiameter:diameter));
                var c=r.Dots.Single(d=>d.Role==BeamSideDotRole.UpperCorner).Center;
                double rho=20-(12+diameter)/2,k=rho/Math.Sqrt(2);
                BeamSideTest.Near(c.X,origin.X+(int)direction*(249+k),"independent mirrored WCS circle centre");
                BeamSideTest.Near(c.Y,origin.Y+549+k);
                double boundaryGap=Math.Abs(c.X-origin.X)-diameter/2;
                BeamSideTest.Check(boundaryGap>200&&r.DotSpans[0].MinimumAdditionalCount==1,"display-radius boundary rule at nonzero origin");
                var nearest=r.Dots.First().Center;
                BeamSideTest.Check(Math.Abs(nearest.X-origin.X)-diameter/2<=200+E,"measured world end clearance");
                BeamSideTest.Check(r.DotSpecification=="%%1326@200","display diameter independent from actual specification");
            }
            Console.WriteLine("BEAM_SIDE_DOTS_PASS checks="+BeamSideTest.Checks);
        }
    }
}
