using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WallDetailTool
{
    internal sealed class CantileverDotLayout
    {
        // Both rows have exactly the same X coordinates, ordered free end -> beam.
        internal readonly ReadOnlyCollection<CantileverPoint> Upper,Lower;
        internal CantileverDotLayout(List<CantileverPoint> upper,List<CantileverPoint> lower)
        { Upper=upper.AsReadOnly();Lower=lower.AsReadOnly(); }
    }
    internal static class CantileverDots
    {
        internal static CantileverDotLayout Calculate(CantileverFrame frame,CantileverRebarLayout steel,
            CantileverPathInputs p,double diameter,double spacing)
        {
            CantileverNumbers.Positive(diameter);CantileverNumbers.Positive(spacing);
            CantileverNumbers.Finite(spacing*spacing);CantileverNumbers.Finite(diameter*diameter);
            if(spacing<diameter) throw new ArgumentException("点筋间距小于显示直径。");
            var top=steel.Bars[0].Vertices;var bottom=steel.Bars[1].Vertices;
            double sign=(int)frame.Direction;
            double free=(top[top.Count-1].Point.X-frame.SupportX)*sign;
            double topY=top[top.Count-2].Point.Y-frame.TopY+p.Radius;
            double bottomY=bottom[0].Point.Y-frame.TopY;
            double offset=(p.DisplayWidth+diameter)/2,dr=diameter/2;
            if(p.Radius<offset-1e-8) throw new ArgumentException("R不足，无法布置圆弧内缘相切角筋；不会缩小R。");
            double cx=free-p.Radius,rho=Math.Max(0,p.Radius-offset),k=rho/Math.Sqrt(2);
            var local=new List<CantileverPoint>{new CantileverPoint(cx+k,topY-p.Radius+k)};
            double horizontalY=topY-offset,lowerY=bottomY+offset;
            while(local[local.Count-1].X-dr>spacing+1e-8)
            {
                var previous=local[local.Count-1];double dy=previous.Y-horizontalY;
                if(Math.Abs(dy)>=spacing) throw new ArgumentException("圆角点到水平排无法保持输入中心距。");
                double x=previous.X-Math.Sqrt(spacing*spacing-dy*dy);
                if(x<dr-1e-8||x>cx+1e-8) throw new ArgumentException("按间距递推的点筋不在真实水平相切直段内。");
                local.Add(new CantileverPoint(x,horizontalY));
                if(local.Count>5000) throw new ArgumentException("上下点筋总数不能超过10000根。");
            }
            var upper=new List<CantileverPoint>();var lower=new List<CantileverPoint>();
            var all=new List<CantileverPoint>();
            foreach(var q in local)
            {
                double lowerStart=(bottom[0].Point.X-frame.SupportX)*sign;
                double lowerEnd=(bottom[1].Point.X-frame.SupportX)*sign;
                if(q.X<Math.Min(lowerStart,lowerEnd)-1e-8||q.X>Math.Max(lowerStart,lowerEnd)+1e-8)
                    throw new ArgumentException("对齐下排点筋不在底筋真实直段上，无法保持内缘相切。");
                foreach(var point in new[]{q,new CantileverPoint(q.X,lowerY)})
                {
                    if(point.X-dr< -1e-8||point.X+dr>p.Length+1e-8||point.Y-dr< -p.Thickness-1e-8||point.Y+dr>1e-8)
                        throw new ArgumentException("点筋显示轮廓超出悬挑板范围。");
                    var world=frame.ToWorld(point.X,point.Y);
                    foreach(var other in all)
                        if(Distance(world,other)<diameter-1e-8) throw new ArgumentException("点筋显示重叠；不移动角筋或改变R。");
                    foreach(var bar in steel.Bars)
                        for(int i=0;i<bar.Vertices.Count-1;i++)
                        {
                            var a=bar.Vertices[i];var b=bar.Vertices[i+1];
                            var curve=new BeamSideCurve(new ContourPoint(a.Point.X,a.Point.Y),new ContourPoint(b.Point.X,b.Point.Y),a.Bulge);
                            var target=new ContourPoint(world.X,world.Y);
                            double t=curve.Parameter(target);
                            double distance=Math.Min(BeamSideMath.Distance(target,curve.A),BeamSideMath.Distance(target,curve.B));
                            if(t>=0&&t<=1) distance=Math.Min(distance,BeamSideMath.Distance(target,curve.At(t)));
                            if(distance<offset-1e-7) throw new ArgumentException("点筋与线筋显示重叠或无法保持相切。");
                        }
                    all.Add(world);
                }
                upper.Add(frame.ToWorld(q.X,q.Y));lower.Add(frame.ToWorld(q.X,lowerY));
            }
            return new CantileverDotLayout(upper,lower);
        }
        private static double Distance(CantileverPoint a,CantileverPoint b)
        { return CantileverNumbers.Finite(Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y))); }
    }
}
