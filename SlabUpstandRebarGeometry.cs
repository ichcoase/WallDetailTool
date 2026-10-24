using System;

namespace WallDetailTool
{
    internal sealed class SlabUpstandRebarParameters
    {
        internal double Cover=25, LineDiameter=8, LineSpacing=150, DotDiameter=6, DotSpacing=200;
        internal double LineDisplayWidth=12, DotDisplayDiameter=12, Radius=20;
        internal double? H;
        internal double LeftLength=300, RightLength=300;
        internal double EffectiveH { get { return H ?? (Cover+LineDisplayWidth/2); } }
    }

    internal sealed class SlabUpstandRebarLayout
    {
        internal readonly System.Collections.Generic.List<RebarVertex> Line=new System.Collections.Generic.List<RebarVertex>();
        internal readonly System.Collections.Generic.List<ContourPoint> LeftDots=new System.Collections.Generic.List<ContourPoint>();
        internal readonly System.Collections.Generic.List<ContourPoint> RightDots=new System.Collections.Generic.List<ContourPoint>();
        internal readonly System.Collections.Generic.List<ContourPoint> TopDots=new System.Collections.Generic.List<ContourPoint>();
        internal readonly System.Collections.Generic.List<ContourPoint> CornerDots=new System.Collections.Generic.List<ContourPoint>();
        internal double LineWidth, DotDiameter, Elevation, LeftOverrun, RightOverrun;
        internal bool HasSlabBoundaries;
    }

    internal static class SlabUpstandRebarGeometry
    {
        private const double E=1e-8;
        private static void Positive(double value,string name)
        { if (double.IsNaN(value)||double.IsInfinity(value)||value<=0) throw new InvalidOperationException(name+"必须为有效正数。"); }
        private static void Nonnegative(double value,string name)
        { if (double.IsNaN(value)||double.IsInfinity(value)||value<0) throw new InvalidOperationException(name+"不能为负数。"); }

        internal static SlabUpstandRebarLayout Calculate(SlabUpstandGeometry g,SlabUpstandRebarParameters p)
        {
            if (g==null||p==null) throw new InvalidOperationException("缺少板上反坎几何或参数。");
            Positive(g.Width,"反坎宽度"); Positive(g.Height,"反坎高度"); Positive(g.SlabThickness,"板厚 h");
            Nonnegative(p.Cover,"保护层"); Positive(p.LineDiameter,"线筋实际直径");
            Positive(p.LineSpacing,"线筋间距"); Positive(p.DotDiameter,"点筋实际直径");
            Positive(p.DotSpacing,"点筋间距"); Positive(p.LineDisplayWidth,"线筋显示宽度");
            Positive(p.DotDisplayDiameter,"点筋显示直径"); Positive(p.Radius,"中心线半径 R");
            Positive(p.LeftLength,"L左"); Positive(p.RightLength,"L右");
            double h=p.EffectiveH; Positive(h,"H");
            if (h>=g.SlabThickness) throw new InvalidOperationException("H 必须小于板厚 h。");
            if (p.DotSpacing+E<p.DotDisplayDiameter) throw new InvalidOperationException("点筋间距小于显示直径。");
            double inset=p.Cover+p.LineDisplayWidth/2;
            double xl=g.Left+inset, xr=g.Right-inset, yt=g.Top-inset, yh=g.SlabBottom+h;
            if (xr-xl<=2*p.Radius+E) throw new InvalidOperationException("反坎过窄：顶部两个 R 圆弧无直线段。");
            if (xr-xl-2*p.Radius<p.DotDisplayDiameter-E) throw new InvalidOperationException("顶部线筋直段不足以容纳点筋显示直径。");
            if (yt-p.Radius<=yh+p.Radius+E) throw new InvalidOperationException("竖直段不足以连接上下 R 圆弧；不会缩小 R。");
            double dotOffset=UpstandDotDistribution.InnerOffset(p.LineDisplayWidth,p.DotDisplayDiameter);
            if (p.Radius<=dotOffset+E) throw new InvalidOperationException("R 不足以在顶部转角内侧布置相切点筋。");
            double first=UpstandDotDistribution.SideFirst(yt,p.Radius,dotOffset,
                p.DotDisplayDiameter,p.DotSpacing,g.SlabTop);
            double raw=(first-p.DotDisplayDiameter/2-g.SlabTop)/p.DotSpacing;
            if (raw>9999) throw new InvalidOperationException("点筋排数超限。");
            int rows=1+(int)Math.Floor(raw+E);
            var q=new SlabUpstandRebarLayout { LineWidth=p.LineDisplayWidth,DotDiameter=p.DotDisplayDiameter,
                Elevation=g.Elevation,HasSlabBoundaries=g.HasSlabBoundaries };
            UpstandDotDistribution.AddCorners(xl,xr,yt,p.Radius,dotOffset,q.CornerDots);
            UpstandDotDistribution.AddTop(xl+p.Radius,xr-p.Radius,yt-dotOffset,p.DotSpacing,
                p.DotDisplayDiameter,q.CornerDots,q.TopDots);
            double leftEnd=xl-p.Radius-p.LeftLength, rightEnd=xr+p.Radius+p.RightLength;
            if (g.HasSlabBoundaries)
            {
                q.LeftOverrun=Math.Max(0,g.SlabLeft.Value-leftEnd);
                q.RightOverrun=Math.Max(0,rightEnd-g.SlabRight.Value);
            }
            double arc=Math.Tan(Math.PI/8);
            q.Line.Add(new RebarVertex(leftEnd,yh,0));
            q.Line.Add(new RebarVertex(xl-p.Radius,yh,arc));
            q.Line.Add(new RebarVertex(xl,yh+p.Radius,0));
            q.Line.Add(new RebarVertex(xl,yt-p.Radius,-arc));
            q.Line.Add(new RebarVertex(xl+p.Radius,yt,0));
            q.Line.Add(new RebarVertex(xr-p.Radius,yt,-arc));
            q.Line.Add(new RebarVertex(xr,yt-p.Radius,0));
            q.Line.Add(new RebarVertex(xr,yh+p.Radius,arc));
            q.Line.Add(new RebarVertex(xr+p.Radius,yh,0));
            q.Line.Add(new RebarVertex(rightEnd,yh,0));
            for (int i=0;i<rows;i++)
            {
                double y=first-i*p.DotSpacing;
                q.LeftDots.Add(new ContourPoint(xl+dotOffset,y));
                q.RightDots.Add(new ContourPoint(xr-dotOffset,y));
            }
            UpstandDotDistribution.CheckCorners(q.CornerDots,q.TopDots,
                q.LeftDots[0],q.RightDots[0],p.DotDisplayDiameter);
            return q;
        }

        internal static bool MayGenerate(SlabUpstandRebarLayout layout,bool userContinues)
        {
            if (layout==null) return false;
            return !layout.HasSlabBoundaries || (layout.LeftOverrun<=E && layout.RightOverrun<=E) || userContinues;
        }
    }
}
