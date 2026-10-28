using System;
using System.Collections.Generic;

namespace WallDetailTool
{
    internal sealed class BeamSideDotDistribution
    {
        internal readonly List<BeamSideDot> Dots=new List<BeamSideDot>();
        internal readonly List<BeamSideDotSpan> Spans=new List<BeamSideDotSpan>();
    }
    // Internal local-frame helper. Only the two outer corners are mandatory.
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
            var upper=new ContourPoint(cx+k,top-radius+k);var lower=new ContourPoint(cx+k,bottom+radius-k);
            var result=new BeamSideDotDistribution();
            var horizontal=OpenHorizontal(upper,top-offset,dr,cx,diameter,spacing);
            for(int i=horizontal.Count-1;i>=0;i--)
                result.Dots.Add(new BeamSideDot(horizontal[i],BeamSideDotRole.UpperHorizontal));
            result.Dots.Add(new BeamSideDot(upper,BeamSideDotRole.UpperCorner));
            result.Spans.Add(new BeamSideDotSpan(0,result.Dots.Count-1,horizontal.Count));
            AddSection(result,upper,lower,BeamSideDotRole.LowerCorner,
                false,-1,outer-offset,-(top-radius),-(bottom+radius),diameter,spacing);
            int lowerIndex=result.Dots.Count-1;
            foreach(var point in horizontal)
                result.Dots.Add(new BeamSideDot(new ContourPoint(point.X,height-point.Y),BeamSideDotRole.LowerHorizontal));
            result.Spans.Add(new BeamSideDotSpan(lowerIndex,result.Dots.Count-1,horizontal.Count));
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
            if(result.Dots[0].Center.X-dr>spacing+E||result.Dots[result.Dots.Count-1].Center.X-dr>spacing+E)
                throw new InvalidOperationException("靠梁边界至最近实际点筋外缘距离超过输入间距。");
            return result;
        }
        // A boundary clearance is a scalar limit, not a virtual fixed dot.
        // Search from zero. For n>0 the first corner-to-straight reach includes
        // its true perpendicular offset, then n-1 straight centre distances.
        private static List<ContourPoint> OpenHorizontal(ContourPoint corner,double y,double dr,double max,
            double diameter,double spacing)
        {
            var points=new List<ContourPoint>();
            if(corner.X-dr<=spacing+E) return points;
            double h=Math.Abs(corner.Y-y);
            if(h>spacing+E) throw new InvalidOperationException("圆角至水平直段无法满足点筋中心距。");
            double reach=Math.Sqrt(Math.Max(0,spacing*spacing-h*h));
            double near=Math.Sqrt(Math.Max(0,diameter*diameter-h*h));
            for(int n=1;n<=10000;n++)
            {
                double low=Math.Max(dr+(n-1)*diameter,corner.X-reach);
                double high=Math.Min(max,Math.Min(corner.X-near,dr+n*spacing));
                if(low>high+E) continue;
                double first=BeamSideMath.Clamp(corner.X-(corner.X-dr)/(n+1),low,high),last=first;
                if(n>1) last=BeamSideMath.Clamp(dr+(first-dr)/n,
                    Math.Max(dr,first-(n-1)*spacing),Math.Min(dr+spacing,first-(n-1)*diameter));
                for(int i=0;i<n;i++) points.Add(new ContourPoint(n==1?first:first+(last-first)*i/(n-1),y));
                return points;
            }
            throw new InvalidOperationException("无法满足水平点筋的边界距离、相切及不重叠约束，或数量超过限制。");
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
                    result.Dots.Add(new BeamSideDot(horizontal?new ContourPoint(sign*q,fixedCoord):new ContourPoint(fixedCoord,sign*q),BeamSideDotRole.OuterVertical));
                }
                result.Dots.Add(new BeamSideDot(b,role));
                result.Spans.Add(new BeamSideDotSpan(first,result.Dots.Count-1,n));return;
            }
            throw new InvalidOperationException("无法在不重叠的条件下满足点筋间距，或点筋过多。");
        }
    }
}
