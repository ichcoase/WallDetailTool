using System;
using System.Linq;
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
        static void Verify(double w,double height,double radius,double s)
        {
            const double d=12,off=12;
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
                BeamSideTest.Check(p.X>=6-E&&p.X<=w-6+E&&p.Y>=6-E&&p.Y<=height-6+E,"inside concrete");
                if(dot.Role==BeamSideDotRole.UpperHorizontal){BeamSideTest.Near(p.Y,top-off);BeamSideTest.Check(p.X<=cx+E,"upper true straight");}
                if(dot.Role==BeamSideDotRole.LowerHorizontal){BeamSideTest.Near(p.Y,bottom+off);BeamSideTest.Check(p.X<=cx+E,"lower true straight");}
                if(dot.Role==BeamSideDotRole.OuterVertical){BeamSideTest.Near(p.X,outer-off);BeamSideTest.Check(p.Y>=bottom+radius-E&&p.Y<=top-radius+E,"outer true straight");}
                for(int j=0;j<i;j++)BeamSideTest.Check(BeamSideMath.Distance(p,r.Dots[j].Center)>=d-E,"no duplicates or overlap");
                if(i>0)BeamSideTest.Check(BeamSideMath.Distance(p,r.Dots[i-1].Center)<=s+E,"actual adjacent distance");
                BeamSideTest.Check(r.Dots.Any(q=>Math.Abs(q.Center.X-p.X)<E&&Math.Abs(q.Center.Y-(height-p.Y))<E),"symmetric");
            }
            BeamSideTest.Check(r.Dots.First().Center.X-6<=s+E&&r.Dots.Last().Center.X-6<=s+E,"boundary to actual nearest dot edge");
            for(int side=0;side<3;side++)
            {
                var span=r.Spans[side];int count=span.MinimumAdditionalCount;
                BeamSideTest.Check(span.Last-span.First-(side==1?1:0)==count,"span counts without virtual endpoints");
                for(int n=0;n<count;n++)BeamSideTest.Check(side==1?!VerticalFeasible(upper,lower,outer-off,-(top-radius),-(bottom+radius),d,s,n):!HorizontalFeasible(upper,top-off,cx,d,s,n),"fewer dots infeasible after relocating every free point");
            }
        }
        static void Main(){try{Run();}catch(Exception ex){Console.WriteLine("FAIL: "+ex.Message);Environment.ExitCode=1;}}
        static void Run()
        {
            foreach(double w in new[]{57.0,70.0,100.0,300.0,600.0})
            foreach(double h in new[]{160.0,300.0,600.0,1000.0})
            foreach(double r in new[]{12.0,20.0})
            foreach(double s in new[]{35.0,50.0,99.9,150.0,200.0,250.0,1000.0})Verify(w,h,r,s);
            Verify(49,600,12,100);Verify(150,600,50,50);Verify(600,1000,100,100);
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
            Verify(critical,600,20,200);Verify(critical+1e-4,600,20,200);
            double dy=8-8/Math.Sqrt(2),two=6+200+Math.Sqrt(200*200-dy*dy)+31+20-8/Math.Sqrt(2);
            BeamSideTest.Check(BeamSideDots.Calculate(two,600,25,12,12,20,200).Spans[0].MinimumAdditionalCount==1,"one-to-two exact reach threshold");
            BeamSideTest.Check(BeamSideDots.Calculate(two+1e-4,600,25,12,12,20,200).Spans[0].MinimumAdditionalCount==2,"one-to-two strict threshold");
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
            Console.WriteLine("BEAM_SIDE_DOTS_PASS checks="+BeamSideTest.Checks);
        }
    }
}
