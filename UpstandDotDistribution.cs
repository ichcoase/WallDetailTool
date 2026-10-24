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
            double maximumSpacing,double displayDiameter,List<ContourPoint> target)
        {
            double length=rightTangent-leftTangent;
            if (double.IsNaN(length)||double.IsInfinity(length)||length<=0 ||
                double.IsNaN(maximumSpacing)||double.IsInfinity(maximumSpacing)||maximumSpacing<=0 ||
                double.IsNaN(displayDiameter)||double.IsInfinity(displayDiameter)||displayDiameter<=0)
                throw new InvalidOperationException("顶部线筋直段或点筋中心距无效。");
            double raw=length/maximumSpacing;
            if (raw>9999) throw new InvalidOperationException("顶部点筋数量超出可处理范围。");
            int count=Math.Max(1,(int)Math.Ceiling(raw-1e-10));
            if (length/(count+1)<displayDiameter-1e-8)
                throw new InvalidOperationException("顶部点筋显示直径和中心距使点筋重叠，无法布置。");
            for (int i=0;i<count;i++)
                target.Add(new ContourPoint(leftTangent+length*(i+1)/(count+1),y));
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
