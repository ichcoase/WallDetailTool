using System;
using System.Collections.Generic;
using System.Linq;

namespace WallDetailTool
{
    // New node adapter only. Existing upstand and cantilever rules remain unchanged.
    internal static class TopSlabDots
    {
        private const double E=1e-7;
        internal static void Calculate(TopSlabRequest r,TopSlabLayout result)
        {
            var g=r.Geometry;double w=r.A(1),dd=r.A(2),off=UpstandDotDistribution.InnerOffset(w,dd),radius=r.A(3);
            double outer=r.A(0)+r.V(0)/2,inner=g.WallThickness-r.A(0)-r.V(2)/2,top=g.Height-r.A(0)-r.V(0)/2;
            double spacing=r.V(7);
            if(spacing<dd)throw new ArgumentException("反坎点筋间距小于显示直径。");
            var curves=new List<BeamSideCurve>();
            foreach(var bar in result.Bars)for(int i=0;i<bar.Count-1;i++)
            {
                var a=bar[i];var b=bar[i+1];curves.Add(new BeamSideCurve(new ContourPoint((a.X-g.OuterX)*g.Direction,a.Y-g.Bottom),
                    new ContourPoint((b.X-g.OuterX)*g.Direction,b.Y-g.Bottom),a.Bulge*g.Direction));
            }
            double leftX=outer+off,rightX=inner-off;
            var blue=result.Bars[1];
            double blueStart=(blue.Last().X-g.OuterX)*g.Direction;
            double blueEnd=(blue[blue.Count-2].X-g.OuterX)*g.Direction;
            double common=blue.Last().Y-g.Bottom-off;
            double topLeft=leftX;
            double contactStart=Math.Max(leftX,Math.Min(blueStart,blueEnd));
            double topRight=Math.Min(rightX,Math.Max(blueStart,blueEnd));
            if(common<g.Height-g.SlabThickness+dd/2||topRight-topLeft<dd)
                throw new ArgumentException("蓝色水平直段下方不能容纳共同顶排点筋。");
            FitTopInterval(curves,common,off,ref contactStart,ref topRight);
            int spans=Math.Max(1,(int)Math.Ceiling((topRight-topLeft)/spacing));
            if(spans>9998||(topRight-topLeft)/spans<dd-E)throw new ArgumentException("顶部点筋数量或显示间距无效。");
            result.WallCommonTop=g.Bottom+common;
            int row=0;
            for(double y=common;y>=dd/2-E;y-=spacing,row++)
            {
                if(row>4998)throw new ArgumentException("反坎点筋数量超限。");
                result.WallLeftDots.Add(g.World(row==0?topLeft:leftX,y));
                result.WallRightDots.Add(g.World(row==0?topRight:rightX,y));
            }
            result.WallCornerDots.Add(result.WallLeftDots[0]);result.WallCornerDots.Add(result.WallRightDots[0]);
            if(spans>1)
            {
                double first=Math.Max(contactStart,topLeft+(topRight-topLeft)/spans);
                if(first-topLeft>spacing+E||(topRight-first)/(spans-1)<dd-E)
                    throw new ArgumentException("蓝色端头留空后，顶排点筋无法同时满足相切及输入间距。");
                for(int i=1;i<spans;i++)result.WallTopDots.Add(g.World(first+(topRight-first)*(i-1)/(spans-1),common));
            }
            result.WallDots.AddRange(result.WallLeftDots.Concat(result.WallRightDots).Concat(result.WallTopDots));

            // The support boundary is the inner concrete face, NOT the stem's outer face.
            // Reuse the actual free-corner + exact-s recursion. Last boundary clearance
            // is whatever remains after recursion, never a forced dot at x=0.
            var frame=new CantileverFrame((CantileverDirection)g.Direction,g.InnerX,g.Top,g.WallThickness);
            var upperSpec=new CantileverSpecification(CantileverBarRole.InnerExtension,r.V(0),r.V(1));
            var lowerSpec=new CantileverSpecification(CantileverBarRole.Bottom,r.V(4),r.V(5));
            var paths=new List<CantileverBarPath>();
            foreach(int i in new[]{0,2})paths.Add(new CantileverBarPath(i==0?upperSpec:lowerSpec,
                result.Bars[i].Select(v=>new CantileverVertex(new CantileverPoint(v.X,v.Y),v.Bulge)).ToList()));
            // Adapt the actual three-bar node's top/bottom paths, not a dummy slab
            // with invented support lengths. Inner crossing bar is checked below.
            var steel=new CantileverRebarLayout(paths,0);
            double actualHorizontalReach=g.WallThickness-(result.Bars[2][1].X-g.OuterX)*g.Direction;
            var inputs=new CantileverPathInputs(g.Projection,g.SlabThickness,r.A(0),radius,w,0,0,0,0,actualHorizontalReach,0);
            var slab=CantileverDots.Calculate(frame,steel,inputs,dd,r.V(9));
            result.SlabDots.AddRange(slab.Upper.Select(p=>new ContourPoint(p.X,p.Y)));
            result.SlabDots.AddRange(slab.Lower.Select(p=>new ContourPoint(p.X,p.Y)));
            result.SlabFirstCenterOffset=slab.Upper.Min(p=>(p.X-g.InnerX)*g.Direction);
            result.SlabBoundaryClearance=result.SlabFirstCenterOffset-dd/2;

            var all=result.WallDots.Concat(result.SlabDots).ToList();
            for(int i=0;i<all.Count;i++)
            {
                var p=new ContourPoint((all[i].X-g.OuterX)*g.Direction,all[i].Y-g.Bottom);
                if(p.X-dd/2< -E||p.X+dd/2>g.WallThickness+g.Projection+E||p.Y-dd/2< -E||p.Y+dd/2>g.Height+E||
                    (p.X+dd/2>g.WallThickness+E&&p.Y-dd/2<g.Height-g.SlabThickness-E))
                    throw new ArgumentException("点筋超出混凝土轮廓。");
                if(i<result.WallDots.Count&&p.X+dd/2>g.WallThickness+E)throw new ArgumentException("反坎点筋超出反坎节点域。");
                if(i>=result.WallDots.Count&&p.X-dd/2<g.WallThickness-E)throw new ArgumentException("飘板点筋进入反坎节点域。");
                if(!Safe(p,curves,off))throw new ArgumentException("点筋与真实交叉筋冲突；不删除点筋或改变R/A/B。");
                for(int j=0;j<i;j++)if(BeamSideMath.Distance(all[i],all[j])<dd-E)throw new ArgumentException("交接区点筋显示重叠，不能通过删点掩盖。");
            }
        }
        private static bool Safe(ContourPoint p,List<BeamSideCurve> curves,double off)
        { return curves.All(c=>Distance(c,p)>=off-E); }
        // Partition the fixed-Y track at every finite stroke distance boundary.
        // Endpoint circles and concentric arc circles supply exact roots; Safe
        // classifies each interval, including finite-curve angular limits.
        private static void FitTopInterval(List<BeamSideCurve> curves,double y,double off,ref double left,ref double right)
        {
            double lo=left,hi=right;
            var cuts=new List<double>{lo,hi};
            Action<double> add=x=> { if(x>lo&&x<hi)cuts.Add(x); };
            Action<ContourPoint,double> circle=(center,r)=>
            {
                if(r<0)return;double q=r*r-(y-center.Y)*(y-center.Y);
                if(q>=-E) { double dx=Math.Sqrt(Math.Max(0,q));add(center.X-dx);add(center.X+dx); }
            };
            foreach(var c in curves)
            {
                circle(c.A,off);circle(c.B,off);
                if(c.IsArc) { circle(c.Center,c.Radius+off);circle(c.Center,c.Radius-off); }
                else
                {
                    double dx=c.B.X-c.A.X,dy=c.B.Y-c.A.Y;
                    if(Math.Abs(dy)>E)
                        foreach(double sign in new[]{-1.0,1.0})add(c.A.X+(dx*(y-c.A.Y)+sign*off*c.Length)/dy);
                }
            }
            cuts.Sort();double bestLeft=0,bestRight=0,start=0;bool open=false;
            for(int i=0;i<cuts.Count-1;i++)
            {
                if(cuts[i+1]-cuts[i]<=E)continue;
                bool safe=Safe(new ContourPoint((cuts[i]+cuts[i+1])/2,y),curves,off);
                if(safe)
                {
                    if(!open)start=cuts[i];open=true;
                    if(cuts[i+1]-start>bestRight-bestLeft) { bestLeft=start;bestRight=cuts[i+1]; }
                }
                else open=false;
            }
            if(bestRight-bestLeft<=E||!Safe(new ContourPoint(bestLeft,y),curves,off)||!Safe(new ContourPoint(bestRight,y),curves,off))
                throw new ArgumentException("共同顶部标高处没有可容纳顶排点筋的连续直段区间。");
            left=bestLeft;right=bestRight;
        }
        private static double Distance(BeamSideCurve c,ContourPoint p)
        { double t=c.Parameter(p);return Math.Min(Math.Min(BeamSideMath.Distance(c.A,p),BeamSideMath.Distance(c.B,p)),t>=0&&t<=1?BeamSideMath.Distance(c.At(t),p):double.PositiveInfinity); }
    }
}
