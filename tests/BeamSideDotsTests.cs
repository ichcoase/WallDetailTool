using System;
using System.Linq;
namespace WallDetailTool
{
    internal static class BeamSideDotsTests
    {
        const double E=1e-7;
        static bool Has(BeamSideDot d,BeamSideDotRole role) { return (d.Role&role)!=0; }
        // Independent interval propagation oracle: one fewer point must be
        // infeasible even after moving ALL remaining intermediates on the straight.
        static bool Feasible(ContourPoint a,ContourPoint b,bool horizontal,int sign,double fixedCoord,
            double low,double high,double diameter,double spacing,int n)
        {
            double dist=BeamSideMath.Distance(a,b);
            if(n==0) return dist>=diameter-E && dist<=spacing+E;
            double aa=sign*(horizontal?a.X:a.Y),bb=sign*(horizontal?b.X:b.Y);
            double ha=Math.Abs((horizontal?a.Y:a.X)-fixedCoord),hb=Math.Abs((horizontal?b.Y:b.X)-fixedCoord);
            if(ha>spacing||hb>spacing) return false;
            double reach=Math.Sqrt(spacing*spacing-ha*ha),minReach=Math.Sqrt(Math.Max(0,diameter*diameter-ha*ha));
            double lo=Math.Max(low,aa+minReach),hi=Math.Min(high,aa+reach);
            if(lo>hi+E) return false;
            for(int i=1;i<n;i++) { lo=Math.Max(low,lo+diameter);hi=Math.Min(high,hi+spacing);if(lo>hi+E)return false; }
            double endLo=bb-Math.Sqrt(spacing*spacing-hb*hb),endHi=bb-Math.Sqrt(Math.Max(0,diameter*diameter-hb*hb));
            return Math.Max(lo,endLo)<=Math.Min(hi,endHi)+E;
        }
        static void Verify(double width,double height,double radius,double spacing)
        {
            const double cover=25,lineWidth=12,d=12;
            var r=BeamSideDots.Calculate(width,height,cover,lineWidth,d,radius,spacing);
            double inset=31,top=height-inset,bottom=inset,outer=width-inset,off=12,rho=radius-off,cx=outer-radius;
            BeamSideTest.Check(r.Dots.Count<=10000,"dot cap");
            foreach(var role in new[]{BeamSideDotRole.UpperEndpoint,BeamSideDotRole.UpperCorner,BeamSideDotRole.LowerCorner,BeamSideDotRole.LowerEndpoint})
                BeamSideTest.Check(r.Dots.Count(p=>Has(p,role))==1,"exactly one required role");
            for(int i=0;i<r.Dots.Count;i++)
            {
                var dot=r.Dots[i];var p=dot.Center;
                BeamSideTest.Check(p.X>=6-E&&p.X<=width-6+E&&p.Y>=6-E&&p.Y<=height-6+E,"inside concrete");
                if(Has(dot,BeamSideDotRole.UpperEndpoint)) { BeamSideTest.Near(p.X,6);BeamSideTest.Near(p.Y,top-off); }
                if(Has(dot,BeamSideDotRole.LowerEndpoint)) { BeamSideTest.Near(p.X,6);BeamSideTest.Near(p.Y,bottom+off); }
                if(Has(dot,BeamSideDotRole.UpperCorner))
                    BeamSideTest.Near(BeamSideMath.Distance(p,new ContourPoint(cx,top-radius)),rho,"upper circular tangency");
                if(Has(dot,BeamSideDotRole.LowerCorner))
                    BeamSideTest.Near(BeamSideMath.Distance(p,new ContourPoint(cx,bottom+radius)),rho,"lower circular tangency");
                if(dot.Role==BeamSideDotRole.Interior)
                    BeamSideTest.Check(Math.Abs(p.Y-(top-off))<E || Math.Abs(p.Y-(bottom+off))<E || Math.Abs(p.X-(outer-off))<E,"interior on actual three straight loci");
                for(int j=0;j<i;j++) BeamSideTest.Check(BeamSideMath.Distance(p,r.Dots[j].Center)>=d-E,"all pairs nonoverlapping");
                if(i>0) BeamSideTest.Check(BeamSideMath.Distance(p,r.Dots[i-1].Center)<=spacing+E,"actual center distance");
                if(dot.Role==BeamSideDotRole.Interior)
                    BeamSideTest.Check(BeamSideMath.Distance(r.Dots[i-1].Center,r.Dots[i+1].Center)>spacing-E,"deleting any intermediate fails");
                BeamSideTest.Check(r.Dots.Any(q=>Math.Abs(q.Center.X-p.X)<E&&Math.Abs(q.Center.Y-(height-p.Y))<E),"symmetric distribution");
            }
            BeamSideTest.Check(r.Spans.Count==3,"open three-sided path");
            for(int side=0;side<3;side++)
            {
                var span=r.Spans[side];
                if(span.First==span.Last) { BeamSideTest.Check(span.MinimumInteriorCount==0,"merged span");continue; }
                BeamSideTest.Check(span.Last-span.First-1==span.MinimumInteriorCount,"interior count metadata");
                var a=r.Dots[span.First].Center;var b=r.Dots[span.Last].Center;
                bool horizontal=side!=1;int sign=side==0?1:-1;
                double fixedCoord=side==0?top-off:side==1?outer-off:bottom+off;
                double low=side==0?6:side==1?-(top-radius):-cx;
                double high=side==0?cx:side==1?-(bottom+radius):-6;
                for(int n=0;n<span.MinimumInteriorCount;n++)
                    BeamSideTest.Check(!Feasible(a,b,horizontal,sign,fixedCoord,low,high,d,spacing,n),"globally fewer straight intermediates infeasible");
            }
        }
        static void Main() { try { Run(); } catch(Exception ex) { Console.WriteLine("FAIL: "+ex.Message);Environment.ExitCode=1; } }
        static void Run()
        {
            foreach(double width in new[]{70.0,100.0,300.0,600.0})
            foreach(double height in new[]{160.0,300.0,600.0,1000.0})
            foreach(double radius in new[]{12.0,20.0})
            foreach(double spacing in new[]{35.0,50.0,99.9,150.0,200.0,250.0,1000.0})
                Verify(width,height,radius,spacing);
            Verify(57,600,20,100); // endpoint lies at the circular tangent: legitimate merge
            Verify(150,600,50,50);
            Verify(600,1000,100,100);
            Verify(49,600,12,100); // R == inner offset, circular locus degenerates to its centre
            var merged=BeamSideDots.Calculate(57,600,25,12,12,20,200);
            BeamSideTest.Check(Has(merged.Dots[0],BeamSideDotRole.UpperEndpoint)&&Has(merged.Dots[0],BeamSideDotRole.UpperCorner),"merged upper roles");
            BeamSideTest.Check(Has(merged.Dots.Last(),BeamSideDotRole.LowerEndpoint)&&Has(merged.Dots.Last(),BeamSideDotRole.LowerCorner),"merged lower roles");
            BeamSideTest.Reject(()=>BeamSideDots.Calculate(57-1e-4,600,25,12,12,20,200),"endpoint beyond tangent");
            BeamSideTest.Reject(()=>BeamSideDots.Calculate(57+1e-4,600,25,12,12,20,200),"near but not identical cannot merge");
            BeamSideTest.Reject(()=>BeamSideDots.Calculate(300,600,25,12,12,12-1e-4,200),"radius too small");
            BeamSideTest.Reject(()=>BeamSideDots.Calculate(300,600,25,12,12,20,12-1e-4),"spacing below display diameter");
            BeamSideTest.Reject(()=>BeamSideDots.Calculate(300,600,25,12,12,100,35),"true arc-to-straight transition cannot reach");
            BeamSideTest.Reject(()=>BeamSideDots.Calculate(300,600,25,12,12,20,Double.PositiveInfinity),"nonfinite spacing");
            var openPath=BeamSideDots.Calculate(300,600,25,12,12,20,35);
            BeamSideTest.Check(BeamSideMath.Distance(openPath.Dots[0].Center,openPath.Dots.Last().Center)>35,"connection face deliberately not closed or filled");
            double k=8/Math.Sqrt(2),dy=8-k;
            double criticalWidth=31+20+6+Math.Sqrt(144-dy*dy)-k;
            Verify(criticalWidth,600,20,200);
            BeamSideTest.Reject(()=>BeamSideDots.Calculate(criticalWidth-1e-4,600,25,12,12,20,200),"fixed dot overlap threshold");
            double transition=Math.Sqrt(Math.Pow(300-31-20+k-6,2)+dy*dy);
            var exact=BeamSideDots.Calculate(300,600,25,12,12,20,transition);
            var tighter=BeamSideDots.Calculate(300,600,25,12,12,20,transition-1e-4);
            BeamSideTest.Check(exact.Spans[0].MinimumInteriorCount==0&&tighter.Spans[0].MinimumInteriorCount==1,"actual circular transition critical spacing");
            foreach(var dir in new[]{BeamSideDirection.Left,BeamSideDirection.Right})
            {
                var g=BeamSideTest.Geometry(width:57,x:-900,y:-800,direction:dir);
                var r=BeamSideRebarGeometry.Calculate(g,new BeamSideRebarParameters(400,100,dotSpacing:200));
                BeamSideTest.Near(g.ToLocal(r.Dots[0].Center).X,6,"world merged endpoint");
            }
            Console.WriteLine("BEAM_SIDE_DOTS_PASS checks="+BeamSideTest.Checks);
        }
    }
}
