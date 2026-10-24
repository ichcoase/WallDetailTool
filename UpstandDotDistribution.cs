using System;
using System.Collections.Generic;

namespace WallDetailTool
{
    // Dot outlines touch the inner edge of the U line, including both quarter-circle bends.
    internal static class UpstandDotDistribution
    {
        internal static double InnerOffset(double lineWidth,double dotDiameter)
        { return (lineWidth+dotDiameter)/2; }

        internal static void AddCorners(double leftLine,double rightLine,double topLine,
            double radius,double innerOffset,List<ContourPoint> target)
        {
            double innerRadius=radius-innerOffset;
            if (innerRadius<=1e-8)
                throw new InvalidOperationException("R 不足以在顶部转角内侧布置相切点筋；不会自动改动 R。");
            double diagonal=innerRadius/Math.Sqrt(2);
            target.Add(new ContourPoint(leftLine+radius-diagonal,topLine-radius+diagonal));
            target.Add(new ContourPoint(rightLine-radius+diagonal,topLine-radius+diagonal));
        }

        internal static double SideFirst(double topLine,double radius,double innerOffset,
            double dotDiameter,double maximumSpacing,double bottom)
        {
            double innerRadius=radius-innerOffset;
            double sideways=innerRadius*(1-1/Math.Sqrt(2));
            double cornerY=topLine-radius+innerRadius/Math.Sqrt(2);
            double belowCorner=sideways>=dotDiameter ? double.PositiveInfinity :
                cornerY-Math.Sqrt(dotDiameter*dotDiameter-sideways*sideways);
            double highest=Math.Min(topLine-radius-dotDiameter/2,belowCorner);
            double first=Math.Min(topLine-maximumSpacing,highest);
            if (first-dotDiameter/2<bottom-1e-8) first=highest;
            if (first-dotDiameter/2<bottom-1e-8)
                throw new InvalidOperationException("竖段空间不足，无法在转角点筋下方布置侧面点筋。");
            return first;
        }

        internal static void AddTop(double leftTangent,double rightTangent,double y,
            double maximumSpacing,double displayDiameter,List<ContourPoint> corners,
            List<ContourPoint> target)
        {
            double length=rightTangent-leftTangent;
            if (double.IsNaN(length)||double.IsInfinity(length)||length<=0 ||
                double.IsNaN(maximumSpacing)||double.IsInfinity(maximumSpacing)||maximumSpacing<=0 ||
                double.IsNaN(displayDiameter)||double.IsInfinity(displayDiameter)||displayDiameter<=0 ||
                corners==null || corners.Count!=2)
                throw new InvalidOperationException("顶部线筋直段或点筋中心距无效。");
            double dx=corners[1].X-corners[0].X;
            double dy=y-corners[0].Y;
            if (dx<=0 || Math.Abs(corners[0].Y-corners[1].Y)>1e-8)
                throw new InvalidOperationException("顶部转角点筋位置无效。");
            int count=0;
            if (dx>maximumSpacing+1e-8)
            {
                if (maximumSpacing<=Math.Abs(dy))
                    throw new InvalidOperationException("点筋中心距过小，角部与顶部点筋无法衔接。");
                double cornerReach=Math.Sqrt(maximumSpacing*maximumSpacing-dy*dy);
                double extra=(dx-2*cornerReach)/maximumSpacing;
                if (extra>9998) throw new InvalidOperationException("顶部点筋数量超出可处理范围。");
                count=extra<=1e-10 ? 1 : 1+(int)Math.Ceiling(extra-1e-10);
                double first=Math.Max(corners[0].X+Math.Min(dx/(count+1),cornerReach),leftTangent);
                double last=corners[1].X-(first-corners[0].X);
                if (first>last+1e-8 || first>rightTangent+1e-8 || last<leftTangent-1e-8)
                    throw new InvalidOperationException("顶部水平直段不足以放置所需点筋。");
                for (int i=0;i<count;i++)
                    target.Add(new ContourPoint(count==1 ? (corners[0].X+corners[1].X)/2 :
                        first+(last-first)*i/(count-1),y));
            }
            ContourPoint previous=corners[0];
            foreach (ContourPoint dot in target)
            {
                CheckDistance(previous,dot,maximumSpacing,displayDiameter);
                previous=dot;
            }
            CheckDistance(previous,corners[1],maximumSpacing,displayDiameter);
        }

        private static void CheckDistance(ContourPoint a,ContourPoint b,double maximum,double diameter)
        {
            double dx=a.X-b.X,dy=a.Y-b.Y;
            double distance=Math.Sqrt(dx*dx+dy*dy);
            if (distance>maximum+1e-8 || distance<diameter-1e-8)
                throw new InvalidOperationException("顶部相邻点筋中心距或显示轮廓不满足布置要求。");
        }

        internal static void CheckCorners(List<ContourPoint> corners,List<ContourPoint> top,
            ContourPoint leftSide,ContourPoint rightSide,double dotDiameter)
        {
            if (corners.Count!=2) throw new InvalidOperationException("缺少顶部转角点筋。");
            CheckPair(corners[0],corners[1],dotDiameter);
            CheckPair(corners[0],leftSide,dotDiameter);
            CheckPair(corners[1],rightSide,dotDiameter);
            foreach (ContourPoint dot in top)
            {
                CheckPair(corners[0],dot,dotDiameter);
                CheckPair(corners[1],dot,dotDiameter);
            }
        }

        private static void CheckPair(ContourPoint a,ContourPoint b,double diameter)
        {
            double dx=a.X-b.X,dy=a.Y-b.Y;
            if (dx*dx+dy*dy<(diameter-1e-8)*(diameter-1e-8))
                throw new InvalidOperationException("顶部转角附近点筋显示轮廓相交，空间不足。");
        }
    }
}
