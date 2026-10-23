using System;
using System.Collections.Generic;

namespace WallDetailTool
{
    // Adjacent dot centre distance is an upper bound. Keep the two existing outer centres.
    internal static class UpstandDotDistribution
    {
        internal static int PerRow(double left,double right,double maximumSpacing,double displayDiameter)
        {
            double distance=right-left;
            if (double.IsNaN(distance)||double.IsInfinity(distance)||distance<displayDiameter-1e-8 ||
                double.IsNaN(maximumSpacing)||double.IsInfinity(maximumSpacing)||maximumSpacing<displayDiameter-1e-8 ||
                maximumSpacing<=0)
                throw new InvalidOperationException("点筋横向空间或中心距无效，无法布置点筋。");
            int intervals=(int)Math.Ceiling(distance/maximumSpacing-1e-10);
            if (intervals<1) intervals=1;
            if (intervals>9999) throw new InvalidOperationException("横向点筋数量超出可处理范围。");
            return intervals+1;
        }

        internal static void AddMiddle(double left,double right,double y,int perRow,List<ContourPoint> target)
        {
            for (int i=1;i<perRow-1;i++)
                target.Add(new ContourPoint(left+(right-left)*i/(perRow-1),y));
        }
    }
}
