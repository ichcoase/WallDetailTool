using System;
using System.Collections.Generic;

namespace WallDetailTool
{
    internal sealed class UpstandRebarParameters
    {
        internal double Cover = 25;
        internal double LineDiameter = 8, LineSpacing = 150;
        internal double DotDiameter = 6, DotSpacing = 200;
        internal double LineDisplayWidth = 12, DotDisplayDiameter = 12;
        internal double Radius = 20;
        internal double LeftAnchor, RightAnchor;
    }

    internal struct RebarVertex
    {
        internal double X, Y, Bulge;
        internal RebarVertex(double x,double y,double bulge) { X=x; Y=y; Bulge=bulge; }
    }

    internal sealed class UpstandRebarLayout
    {
        internal readonly List<RebarVertex> Line = new List<RebarVertex>();
        internal readonly List<ContourPoint> LeftDots = new List<ContourPoint>();
        internal readonly List<ContourPoint> RightDots = new List<ContourPoint>();
        internal double LineWidth, DotDiameter, Elevation;
    }

    // Pure geometry: no AutoCAD objects, INSUNITS conversions, or source identifiers.
    internal static class UpstandRebarGeometry
    {
        private const double Epsilon=1e-8;
        internal static UpstandRebarLayout Calculate(UpstandGeometry g,UpstandRebarParameters p)
        {
            if (g==null || p==null) throw new InvalidOperationException("缺少反坎几何或钢筋参数。");
            Positive(g.Width,"反坎宽度"); Positive(g.Height,"反坎高度");
            Nonnegative(p.Cover,"保护层");
            Positive(p.LineDiameter,"线筋实际直径"); Positive(p.LineSpacing,"线筋间距");
            Positive(p.DotDiameter,"点筋实际直径"); Positive(p.DotSpacing,"点筋间距");
            Positive(p.LineDisplayWidth,"线筋显示宽度"); Positive(p.DotDisplayDiameter,"点筋显示直径");
            Positive(p.Radius,"中心线圆弧半径 R");
            Positive(p.LeftAnchor,"左侧锚固长度"); Positive(p.RightAnchor,"右侧锚固长度");
            if (p.DotSpacing+Epsilon<p.DotDisplayDiameter)
                throw new InvalidOperationException("点筋间距小于显示直径，点筋会重叠。");
            double e=p.Cover+p.LineDisplayWidth/2;
            double xl=g.Left+e, xr=g.Right-e, yt=g.Top-e;
            if (xr-xl<=2*p.Radius+Epsilon)
                throw new InvalidOperationException("反坎过窄：两个 R 圆弧间没有顶部水平直段；不会自动缩小 R。");
            if (yt-p.Radius<=g.BeamTop+Epsilon)
                throw new InvalidOperationException("反坎过矮：R 圆弧下方没有竖直线筋段；不会自动缩小 R。");
            double xDotL=xl+p.LineDisplayWidth/2+p.DotDisplayDiameter/2;
            double xDotR=xr-p.LineDisplayWidth/2-p.DotDisplayDiameter/2;
            if (xDotR-xDotL<p.DotDisplayDiameter-Epsilon)
                throw new InvalidOperationException("反坎过窄：左右点筋相交，空间不足。");
            double yFirst=yt-p.LineDisplayWidth/2-p.DotDisplayDiameter/2;
            if (yFirst-p.DotDisplayDiameter/2<g.BeamTop-Epsilon)
                throw new InvalidOperationException("反坎过矮：第一排点筋会越过梁顶。");
            double available=yFirst-p.DotDisplayDiameter/2-g.BeamTop;
            double raw=available/p.DotSpacing;
            if (raw>9999) throw new InvalidOperationException("点筋排数超出可处理范围。");
            int rows=1+(int)Math.Floor(raw+Epsilon);
            var layout=new UpstandRebarLayout { LineWidth=p.LineDisplayWidth,
                DotDiameter=p.DotDisplayDiameter,Elevation=g.Elevation };
            double bulge=-Math.Tan(Math.PI/8);
            layout.Line.Add(new RebarVertex(xl,g.BeamTop-p.LeftAnchor,0));
            layout.Line.Add(new RebarVertex(xl,g.BeamTop,0));
            layout.Line.Add(new RebarVertex(xl,yt-p.Radius,bulge));
            layout.Line.Add(new RebarVertex(xl+p.Radius,yt,0));
            layout.Line.Add(new RebarVertex(xr-p.Radius,yt,bulge));
            layout.Line.Add(new RebarVertex(xr,yt-p.Radius,0));
            layout.Line.Add(new RebarVertex(xr,g.BeamTop,0));
            layout.Line.Add(new RebarVertex(xr,g.BeamTop-p.RightAnchor,0));
            for (int i=0;i<rows;i++)
            {
                double y=yFirst-i*p.DotSpacing;
                layout.LeftDots.Add(new ContourPoint(xDotL,y));
                layout.RightDots.Add(new ContourPoint(xDotR,y));
            }
            return layout;
        }
        private static void Positive(double v,string name)
        {
            if (Double.IsNaN(v) || Double.IsInfinity(v) || v<=0)
                throw new InvalidOperationException(name+"必须为有效正数。");
        }
        private static void Nonnegative(double v,string name)
        {
            if (Double.IsNaN(v) || Double.IsInfinity(v) || v<0)
                throw new InvalidOperationException(name+"不能为负数。");
        }
    }
}
