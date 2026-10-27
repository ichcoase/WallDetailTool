using System;
using System.Collections.Generic;

namespace WallDetailTool
{
    internal sealed class BeamSideDotDistribution
    {
        internal readonly List<BeamSideDot> Dots=new List<BeamSideDot>();
        internal readonly List<BeamSideDotSpan> Spans=new List<BeamSideDotSpan>();
    }
    // Internal local-frame helper. Fixed corner points use the bisector of the
    // inner circular locus, except a proven endpoint/corner merge at tangency.
    internal static class BeamSideDots
    {
        private const double E=BeamSideMath.Epsilon;
        internal static BeamSideDotDistribution Calculate(double width,double height,double cover,double lineWidth,
            double diameter,double radius,double spacing)
        {
            BeamSideMath.Finite(width,height,cover,lineWidth,diameter,radius,spacing);
            BeamSideMath.Finite(width*width,height*height,diameter*diameter,radius*radius,spacing*spacing);
            if(width<=0||height<=0||cover<0||lineWidth<=0||diameter<=0||radius<=0||spacing<diameter-E)
                throw new InvalidOperationException("点筋参数无效或输入中心距小于显示直径。");
            double inset=cover+lineWidth/2,top=height-inset,bottom=inset,outer=width-inset;
            double offset=(lineWidth+diameter)/2,dr=diameter/2;
            if(radius<offset-E || top-bottom<=2*radius+E || outer<=radius)
                throw new InvalidOperationException("R 或块体空间不足，不能布置相切点筋。");
            double rho=Math.Max(0,radius-offset),cx=outer-radius,k=rho/Math.Sqrt(2);
            var start=new ContourPoint(dr,top-offset);var end=new ContourPoint(dr,bottom+offset);
            var upper=new ContourPoint(cx+k,top-radius+k);var lower=new ContourPoint(cx+k,bottom+radius-k);
            // A flush endpoint must touch the actual straight, not its extension.
            if(dr>cx+E) throw new InvalidOperationException("连接面端点超出水平直段，无法同时满足位置与相切。");
            bool merge=Math.Abs(cx-dr)<=E;
            if(merge) { upper=start;lower=end; }
            else if(BeamSideMath.Distance(start,upper)<diameter-E || BeamSideMath.Distance(lower,end)<diameter-E)
                throw new InvalidOperationException("端部点筋与必需角点过近，且不能由一根点筋同时满足位置及相切；拒绝合并。");
            var result=new BeamSideDotDistribution();
            result.Dots.Add(new BeamSideDot(start,BeamSideDotRole.UpperEndpoint|(merge?BeamSideDotRole.UpperCorner:0)));
            if(!merge) AddSection(result,start,upper,BeamSideDotRole.UpperCorner,true,1,top-offset,dr,cx,diameter,spacing);
            else result.Spans.Add(new BeamSideDotSpan(0,0,0));
            AddSection(result,upper,lower,BeamSideDotRole.LowerCorner|(merge?BeamSideDotRole.LowerEndpoint:0),
                false,-1,outer-offset,-(top-radius),-(bottom+radius),diameter,spacing);
            if(!merge) AddSection(result,lower,end,BeamSideDotRole.LowerEndpoint,true,-1,bottom+offset,-cx,-dr,diameter,spacing);
            else result.Spans.Add(new BeamSideDotSpan(result.Dots.Count-1,result.Dots.Count-1,0));
            if(result.Dots.Count>10000) throw new InvalidOperationException("点筋总数超出 10000 根限制。");
            for(int i=0;i<result.Dots.Count;i++)
            {
                var point=result.Dots[i].Center;
                if(point.X-dr< -E||point.X+dr>width+E||point.Y-dr< -E||point.Y+dr>height+E)
                    throw new InvalidOperationException("点筋显示轮廓超出块体。");
                for(int j=0;j<i;j++)
                    if(BeamSideMath.Distance(point,result.Dots[j].Center)<diameter-E)
                        throw new InvalidOperationException("点筋显示轮廓重叠。");
                if(i>0 && BeamSideMath.Distance(point,result.Dots[i-1].Center)>spacing+E)
                    throw new InvalidOperationException("点筋实际中心距超过输入上限。");
            }
            return result;
        }
        // Each section has fixed endpoints and interior points constrained to
        // its true straight tangent locus. Endpoint reach intervals use actual
        // perpendicular offsets; count search is exact within these constraints.
        private static void AddSection(BeamSideDotDistribution result,ContourPoint a,ContourPoint b,
            BeamSideDotRole role,bool horizontal,int sign,double fixedCoord,double straightMin,double straightMax,
            double diameter,double spacing)
        {
            int first=result.Dots.Count-1;
            double direct=BeamSideMath.Distance(a,b);
            if(direct<diameter-E) throw new InvalidOperationException("必需点筋位置重合或显示重叠。");
            if(direct<=spacing+E)
            {
                result.Dots.Add(new BeamSideDot(b,role));result.Spans.Add(new BeamSideDotSpan(first,first+1,0));return;
            }
            double aa=sign*(horizontal?a.X:a.Y),bb=sign*(horizontal?b.X:b.Y);
            double ha=Math.Abs((horizontal?a.Y:a.X)-fixedCoord),hb=Math.Abs((horizontal?b.Y:b.X)-fixedCoord);
            if(ha>spacing+E||hb>spacing+E) throw new InvalidOperationException("圆角过渡无法满足点筋中心距上限。");
            double lo1=Math.Max(straightMin,aa+Math.Sqrt(Math.Max(0,diameter*diameter-ha*ha)));
            double hi1=Math.Min(straightMax,aa+Math.Sqrt(Math.Max(0,spacing*spacing-ha*ha)));
            double lo2=Math.Max(straightMin,bb-Math.Sqrt(Math.Max(0,spacing*spacing-hb*hb)));
            double hi2=Math.Min(straightMax,bb-Math.Sqrt(Math.Max(0,diameter*diameter-hb*hb)));
            if(lo1>hi1+E||lo2>hi2+E) throw new InvalidOperationException("直段不足以连接固定角点。");
            for(int n=1;n<=10000;n++)
            {
                double start,last;
                if(n==1)
                {
                    double low=Math.Max(lo1,lo2),high=Math.Min(hi1,hi2);
                    if(low>high+E) continue;
                    start=last=BeamSideMath.Clamp((aa+bb)/2,low,high);
                }
                else
                {
                    double gapLow=Math.Max((n-1)*diameter,lo2-hi1);
                    double gapHigh=Math.Min((n-1)*spacing,hi2-lo1);
                    if(gapLow>gapHigh+E) continue;
                    double ideal=(bb-aa)/(n+1);
                    double gap=BeamSideMath.Clamp((n-1)*ideal,gapLow,gapHigh);
                    double low=Math.Max(lo1,lo2-gap),high=Math.Min(hi1,hi2-gap);
                    if(low>high+E) continue;
                    start=BeamSideMath.Clamp((aa+ideal+bb-ideal-gap)/2,low,high);last=start+gap;
                }
                if(result.Dots.Count+n+1>10000) throw new InvalidOperationException("点筋总数超出 10000 根限制。");
                for(int i=0;i<n;i++)
                {
                    double q=n==1?start:start+(last-start)*i/(n-1);
                    result.Dots.Add(new BeamSideDot(horizontal?new ContourPoint(sign*q,fixedCoord):new ContourPoint(fixedCoord,sign*q),BeamSideDotRole.Interior));
                }
                result.Dots.Add(new BeamSideDot(b,role));
                result.Spans.Add(new BeamSideDotSpan(first,result.Dots.Count-1,n));return;
            }
            throw new InvalidOperationException("无法在不重叠的条件下满足点筋间距，或点筋过多。");
        }
    }
}
