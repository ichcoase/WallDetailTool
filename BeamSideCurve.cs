using System;
using System.Collections.Generic;
using System.Linq;

namespace WallDetailTool
{
    internal struct BeamSideVertex
    {
        internal readonly double X,Y,Bulge;
        internal BeamSideVertex(double x,double y,double bulge) { X=x;Y=y;Bulge=bulge; }
        internal ContourPoint Point { get { return new ContourPoint(X,Y); } }
    }
    // Analytic finite lines/arcs. Also used for exact displayed-width regions,
    // avoiding tessellation or bounding-box collision guesses at tight bends.
    internal sealed class BeamSideCurve
    {
        private const double E=BeamSideMath.Epsilon;
        internal readonly ContourPoint A,B,Center;
        internal readonly double Radius,Start,Sweep;
        internal bool IsArc { get { return Radius>0; } }
        internal BeamSideCurve(ContourPoint a,ContourPoint b,double bulge)
        {
            A=a;B=b;
            double dx=b.X-a.X,dy=b.Y-a.Y,chord=BeamSideMath.Distance(a,b);
            if(chord<=E) throw new InvalidOperationException("线筋含零长度段。");
            if(Math.Abs(bulge)<=E) { Center=new ContourPoint(); return; }
            double k=(1-bulge*bulge)/(4*bulge);
            Center=new ContourPoint((a.X+b.X)/2-dy*k,(a.Y+b.Y)/2+dx*k);
            Radius=chord*(1+bulge*bulge)/(4*Math.Abs(bulge));
            Start=Math.Atan2(a.Y-Center.Y,a.X-Center.X); Sweep=4*Math.Atan(bulge);
        }
        private BeamSideCurve(ContourPoint center,double radius,double start,double sweep)
        {
            Center=center; Radius=radius;Start=start;Sweep=sweep;
            A=AtAngle(start);B=AtAngle(start+sweep);
        }
        internal BeamSideCurve Concentric(double radius)
        { return new BeamSideCurve(Center,radius,Start,Sweep); }
        private ContourPoint AtAngle(double angle)
        { return new ContourPoint(Center.X+Radius*Math.Cos(angle),Center.Y+Radius*Math.Sin(angle)); }
        internal ContourPoint At(double t)
        { return IsArc?AtAngle(Start+t*Sweep):new ContourPoint(A.X+t*(B.X-A.X),A.Y+t*(B.Y-A.Y)); }
        internal double Length { get { return IsArc?Radius*Math.Abs(Sweep):BeamSideMath.Distance(A,B); } }
        internal ContourPoint Tangent(double t)
        {
            if(!IsArc) { double length=Length;return new ContourPoint((B.X-A.X)/length,(B.Y-A.Y)/length); }
            double angle=Start+t*Sweep,sign=Math.Sign(Sweep);
            return new ContourPoint(-sign*Math.Sin(angle),sign*Math.Cos(angle));
        }
        private static double Normalize(double angle)
        { double v=angle%(2*Math.PI);return v<0?v+2*Math.PI:v; }
        internal double Parameter(ContourPoint point)
        {
            if(!IsArc)
            {
                double dx=B.X-A.X,dy=B.Y-A.Y;
                return ((point.X-A.X)*dx+(point.Y-A.Y)*dy)/(dx*dx+dy*dy);
            }
            double angle=Math.Atan2(point.Y-Center.Y,point.X-Center.X);
            double delta=Normalize(Math.Sign(Sweep)*(angle-Start));
            if(2*Math.PI-delta<E/Radius) delta=0;
            return delta/Math.Abs(Sweep);
        }
        internal bool Contains(ContourPoint point)
        {
            double t=Parameter(point),slack=E/Length;
            if(t< -slack || t>1+slack) return false;
            if(IsArc) return Math.Abs(BeamSideMath.Distance(point,Center)-Radius)<=E;
            var p=At(t);return BeamSideMath.Distance(p,point)<=E;
        }
        private static double Cross(double x,double y,double xx,double yy) { return x*yy-y*xx; }
        internal static List<ContourPoint> Intersections(BeamSideCurve a,BeamSideCurve b)
        {
            var result=new List<ContourPoint>();
            Action<ContourPoint> add=p=> { if(a.Contains(p)&&b.Contains(p)&&!result.Any(q=>BeamSideMath.Distance(p,q)<=E)) result.Add(p); };
            foreach(var p in new[]{a.A,a.B,b.A,b.B}) add(p);
            if(!a.IsArc&&!b.IsArc)
            {
                double dx=a.B.X-a.A.X,dy=a.B.Y-a.A.Y,ex=b.B.X-b.A.X,ey=b.B.Y-b.A.Y;
                double cross=Cross(dx,dy,ex,ey);
                if(Math.Abs(cross)>E*Math.Max(a.Length,b.Length))
                { double t=Cross(b.A.X-a.A.X,b.A.Y-a.A.Y,ex,ey)/cross;add(a.At(t)); }
            }
            else if(a.IsArc&&b.IsArc)
            {
                double dx=b.Center.X-a.Center.X,dy=b.Center.Y-a.Center.Y,d=Math.Sqrt(dx*dx+dy*dy);
                if(d>E && d<=a.Radius+b.Radius+E && d>=Math.Abs(a.Radius-b.Radius)-E)
                {
                    double x=(a.Radius*a.Radius-b.Radius*b.Radius+d*d)/(2*d);
                    double h=Math.Sqrt(Math.Max(0,a.Radius*a.Radius-x*x));
                    double px=a.Center.X+x*dx/d,py=a.Center.Y+x*dy/d;
                    add(new ContourPoint(px-h*dy/d,py+h*dx/d));add(new ContourPoint(px+h*dy/d,py-h*dx/d));
                }
            }
            else
            {
                BeamSideCurve line=a.IsArc?b:a,arc=a.IsArc?a:b;
                double dx=line.B.X-line.A.X,dy=line.B.Y-line.A.Y;
                double fx=line.A.X-arc.Center.X,fy=line.A.Y-arc.Center.Y;
                double aa=dx*dx+dy*dy,bb=2*(fx*dx+fy*dy),cc=fx*fx+fy*fy-arc.Radius*arc.Radius;
                double discriminant=bb*bb-4*aa*cc;
                if(discriminant>=-E*aa)
                { double root=Math.Sqrt(Math.Max(0,discriminant));add(line.At((-bb-root)/(2*aa)));add(line.At((-bb+root)/(2*aa))); }
            }
            return result;
        }
        internal List<ContourPoint> Extrema()
        {
            var points=new List<ContourPoint>{A,B};
            if(IsArc) for(int i=0;i<4;i++) { var p=AtAngle(i*Math.PI/2);if(Contains(p)) points.Add(p); }
            return points;
        }
    }
    internal sealed class BeamSideStroke
    {
        private const double E=BeamSideMath.Epsilon;
        internal readonly BeamSideCurve Axis;
        internal readonly double HalfWidth;
        internal readonly List<BeamSideCurve> Boundary=new List<BeamSideCurve>();
        internal BeamSideStroke(BeamSideCurve curve,double width)
        {
            Axis=curve;HalfWidth=width/2;
            if(curve.IsArc)
            {
                if(curve.Radius<=HalfWidth) throw new InvalidOperationException("线筋显示宽度超过圆弧可容纳空间。");
                var inner=curve.Concentric(curve.Radius-HalfWidth);var outer=curve.Concentric(curve.Radius+HalfWidth);
                Boundary.Add(inner);Boundary.Add(outer);
                Boundary.Add(new BeamSideCurve(inner.A,outer.A,0));Boundary.Add(new BeamSideCurve(inner.B,outer.B,0));
            }
            else
            {
                var t=curve.Tangent(0);double x=-t.Y*HalfWidth,y=t.X*HalfWidth;
                var points=new[]{new ContourPoint(curve.A.X+x,curve.A.Y+y),new ContourPoint(curve.B.X+x,curve.B.Y+y),
                    new ContourPoint(curve.B.X-x,curve.B.Y-y),new ContourPoint(curve.A.X-x,curve.A.Y-y)};
                for(int i=0;i<4;i++) Boundary.Add(new BeamSideCurve(points[i],points[(i+1)%4],0));
            }
        }
        private bool Interior(ContourPoint p)
        {
            double t=Axis.Parameter(p),slack=E/Axis.Length;
            if(t<=slack || t>=1-slack) return false;
            if(Axis.IsArc) return Math.Abs(BeamSideMath.Distance(p,Axis.Center)-Axis.Radius)<HalfWidth-E;
            var foot=Axis.At(t);return BeamSideMath.Distance(p,foot)<HalfWidth-E;
        }
        internal bool Overlaps(BeamSideStroke other)
        {
            if(Interior(other.Axis.At(.5)) || other.Interior(Axis.At(.5))) return true;
            return BoundaryInside(this,other) || BoundaryInside(other,this);
        }
        private static bool BoundaryInside(BeamSideStroke a,BeamSideStroke b)
        {
            foreach(var edge in a.Boundary)
            {
                var cuts=new List<double>{0,1};
                foreach(var target in b.Boundary)
                    foreach(var p in BeamSideCurve.Intersections(edge,target)) cuts.Add(BeamSideMath.Clamp(edge.Parameter(p),0,1));
                cuts.Sort();
                for(int i=1;i<cuts.Count;i++)
                    if(cuts[i]-cuts[i-1]>E/edge.Length && b.Interior(edge.At((cuts[i]+cuts[i-1])/2))) return true;
            }
            return false;
        }
        internal void CheckInside(BeamSideRect rect,string name)
        {
            foreach(var curve in Boundary) foreach(var p in curve.Extrema())
                if(p.X<rect.Left-E||p.X>rect.Right+E||p.Y<rect.Bottom-E||p.Y>rect.Top+E)
                    throw new InvalidOperationException(name+"钢筋显示外轮廓超出梁边界，位置（"+p.X.ToString("g")+","+p.Y.ToString("g")+"）。");
        }
    }
}
