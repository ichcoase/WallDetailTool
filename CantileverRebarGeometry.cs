using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WallDetailTool
{
    internal struct CantileverVertex
    {
        internal readonly CantileverPoint Point;
        internal readonly double Bulge;
        internal CantileverVertex(CantileverPoint p,double bulge) { Point=p;Bulge=CantileverNumbers.Finite(bulge); }
    }
    internal sealed class CantileverBarPath
    {
        internal readonly CantileverSpecification Specification;
        internal readonly ReadOnlyCollection<CantileverVertex> Vertices;
        internal CantileverBarPath(CantileverSpecification spec,List<CantileverVertex> vertices)
        { Specification=spec;Vertices=vertices.AsReadOnly(); }
    }
    internal sealed class CantileverRebarLayout
    {
        internal readonly ReadOnlyCollection<CantileverBarPath> Bars;
        internal CantileverRebarLayout(List<CantileverBarPath> bars) { Bars=bars.AsReadOnly(); }
    }
    // All lengths below are explicit inputs in mm, never engineering defaults.
    // A,V,E,F,B use the user-confirmed projections in V05_LENGTH_ORIGINS.svg.
    internal sealed class CantileverPathInputs
    {
        internal readonly double Length,Thickness,Cover,Radius,DisplayWidth,A,V,E,F,B,Lin;
        internal CantileverPathInputs(double length,double thickness,double cover,double radius,double displayWidth,
            double a,double v,double e,double f,double b,double lin)
        {
            Length=CantileverNumbers.Positive(length);Thickness=CantileverNumbers.Positive(thickness);
            Cover=CantileverNumbers.Finite(cover);if(cover<0) throw new ArgumentException("保护层不能为负。");Radius=CantileverNumbers.Positive(radius);
            DisplayWidth=CantileverNumbers.Positive(displayWidth);
            if(radius<=displayWidth/2) throw new ArgumentException("圆弧半径不能容纳显示宽度。");
            // Unused mode-specific inputs may be zero, but never nonfinite/negative.
            foreach(double value in new[]{a,v,e,f,b,lin})
                if(CantileverNumbers.Finite(value)<0) throw new ArgumentException("控制长度不能为负。");
            A=a;V=v;E=e;F=CantileverNumbers.Positive(f);B=CantileverNumbers.Positive(b);Lin=lin;
        }
    }
    internal static class CantileverRebarGeometry
    {
        internal static CantileverRebarLayout Calculate(CantileverFrame frame,CantileverSpecifications specs,CantileverPathInputs p)
        {
            if(frame==null||specs==null||p==null) throw new ArgumentNullException();
            double top=-p.Cover-specs.Groups[0].Diameter/2;
            double bottom=-p.Thickness+p.Cover+specs.Groups[1].Diameter/2;
            double free=p.Length-p.Cover-specs.Groups[0].Diameter/2;
            if(bottom>=top || free<=0) throw new ArgumentException("板厚或悬挑长度不足。");
            if(top-p.F-p.DisplayWidth/2< -p.Thickness-1e-8 || bottom-p.DisplayWidth/2< -p.Thickness-1e-8)
                throw new ArgumentException("自由端下弯或底筋显示轮廓超出悬挑板厚度。");
            var bars=new List<CantileverBarPath>();
            foreach(var spec in specs.Groups)
            {
                var corners=new List<CantileverPoint>();
                if(spec.Role==CantileverBarRole.Bottom)
                {
                    double end=p.Length-p.Cover-spec.Diameter/2;
                    if(end<=0) throw new ArgumentException("底筋长度不足。");
                    corners.Add(new CantileverPoint(-p.B,bottom));corners.Add(new CantileverPoint(end,bottom));
                }
                else
                {
                    if(specs.Mode==CantileverSupportMode.NoInnerUp||specs.Mode==CantileverSupportMode.NoInnerDown)
                    {
                        CantileverNumbers.Positive(p.A);CantileverNumbers.Positive(p.V);
                        double sign=specs.Mode==CantileverSupportMode.NoInnerUp?1:-1;
                        corners.Add(new CantileverPoint(-p.A,top+sign*p.V));
                        corners.Add(new CantileverPoint(-p.A,top));
                    }
                    else
                    {
                        double start=spec.Role==CantileverBarRole.Additional?frame.CutoffLocalX(p.Lin):
                            CantileverNumbers.Finite(-frame.BeamWidth-CantileverNumbers.Positive(p.E));
                        corners.Add(new CantileverPoint(start,top));
                    }
                    corners.Add(new CantileverPoint(free,top));corners.Add(new CantileverPoint(free,top-p.F));
                }
                bars.Add(new CantileverBarPath(spec,Round(frame,corners,p.Radius)));
            }
            foreach(var bar in bars) ValidateStroke(bar,p.DisplayWidth);
            return new CantileverRebarLayout(bars);
        }
        private static List<BeamSideStroke> Strokes(CantileverBarPath bar,double width)
        {
            var result=new List<BeamSideStroke>();
            for(int i=0;i<bar.Vertices.Count-1;i++)
            {
                var a=bar.Vertices[i];var b=bar.Vertices[i+1];
                result.Add(new BeamSideStroke(new BeamSideCurve(new ContourPoint(a.Point.X,a.Point.Y),new ContourPoint(b.Point.X,b.Point.Y),a.Bulge),width));
            }
            return result;
        }
        private static void ValidateStroke(CantileverBarPath bar,double width)
        {
            var strokes=Strokes(bar,width);
            for(int i=0;i<strokes.Count;i++) for(int j=0;j<i-1;j++)
                if(strokes[i].Overlaps(strokes[j])) throw new ArgumentException("本根钢筋显示轮廓自相交。");
        }
        private static List<CantileverVertex> Round(CantileverFrame frame,List<CantileverPoint> corners,double radius)
        {
            var result=new List<CantileverVertex>();
            Action<CantileverPoint,double> add=(point,bulge)=>result.Add(new CantileverVertex(frame.ToWorld(point.X,point.Y),frame.WorldBulge(bulge)));
            add(corners[0],0);
            for(int i=1;i<corners.Count-1;i++)
            {
                var a=corners[i-1];var c=corners[i];var b=corners[i+1];
                double ax=c.X-a.X,ay=c.Y-a.Y,bx=b.X-c.X,by=b.Y-c.Y;
                double al=Math.Sqrt(ax*ax+ay*ay),bl=Math.Sqrt(bx*bx+by*by);
                CantileverNumbers.Positive(al);CantileverNumbers.Positive(bl);
                ax/=al;ay/=al;bx/=bl;by/=bl;
                if(Math.Abs(ax*bx+ay*by)>1e-8) throw new ArgumentException("仅接受正交弯折。");
                if(al<=radius||bl<=radius) throw new ArgumentException("投影长度不足以容纳相切圆弧。");
                add(new CantileverPoint(c.X-ax*radius,c.Y-ay*radius),Math.Sign(ax*by-ay*bx)*Math.Tan(Math.PI/8));
                add(new CantileverPoint(c.X+bx*radius,c.Y+by*radius),0);
            }
            add(corners[corners.Count-1],0);
            // The original straight directions must survive trimming at BOTH ends.
            for(int i=0;i<result.Count-1;i++)
            {
                var a=result[i].Point;var b=result[i+1].Point;
                double dx=b.X-a.X,dy=b.Y-a.Y;
                if(dx*dx+dy*dy<=1e-16) throw new ArgumentException("钢筋包含零长度段。");
                if(result[i].Bulge==0)
                {
                    bool forward=false;
                    for(int j=0;j<corners.Count-1;j++)
                    {
                        var ca=frame.ToWorld(corners[j].X,corners[j].Y);var cb=frame.ToWorld(corners[j+1].X,corners[j+1].Y);
                        if(Math.Abs((b.X-a.X)*(cb.Y-ca.Y)-(b.Y-a.Y)*(cb.X-ca.X))<1e-8&&
                            dx*(cb.X-ca.X)+dy*(cb.Y-ca.Y)>0) { forward=true;break; }
                    }
                    if(!forward) throw new ArgumentException("圆弧修剪导致直段反向。");
                }
            }
            return result;
        }
    }
}
