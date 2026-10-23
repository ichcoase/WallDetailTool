using System;
using System.Collections.Generic;

namespace WallDetailTool
{
    // Dots follow the top straight segment of the U; none are placed inside the section.
    internal static class UpstandDotDistribution
    {
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
    }
}
