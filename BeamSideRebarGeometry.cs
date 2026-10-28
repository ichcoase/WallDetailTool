using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace WallDetailTool
{
    internal sealed class BeamSideRebarParameters
    {
        internal readonly double Length,Vertical,Cover,LineDiameter,LineSpacing,DotDiameter,DotSpacing;
        internal readonly double LineDisplayWidth,DotDisplayDiameter,Radius,TextHeight;
        internal readonly bool TopUp,BottomUp,AnnotationRight;
        internal BeamSideRebarParameters(double length,double vertical,double cover=25,double lineDiameter=8,
            double lineSpacing=150,double dotDiameter=6,double dotSpacing=200,double lineDisplayWidth=12,
            double dotDisplayDiameter=12,double radius=20,bool topUp=false,bool bottomUp=true,
            bool annotationRight=true,double textHeight=62.5)
        {
            BeamSideMath.Finite(length,vertical,cover,lineDiameter,lineSpacing,dotDiameter,dotSpacing,
                lineDisplayWidth,dotDisplayDiameter,radius,textHeight);
            if(length<=0||vertical<=0||cover<0||lineDiameter<=0||lineSpacing<=0||dotDiameter<=0||dotSpacing<=0||
                lineDisplayWidth<=0||dotDisplayDiameter<=0||radius<=0||textHeight<=0)
                throw new InvalidOperationException("长度、直径、间距、显示尺寸、R 和字高须为正数，保护层不能为负数。");
            Length=length;Vertical=vertical;Cover=cover;LineDiameter=lineDiameter;LineSpacing=lineSpacing;
            DotDiameter=dotDiameter;DotSpacing=dotSpacing;LineDisplayWidth=lineDisplayWidth;
            DotDisplayDiameter=dotDisplayDiameter;Radius=radius;TopUp=topUp;BottomUp=bottomUp;
            AnnotationRight=annotationRight;TextHeight=textHeight;
        }
    }
    [Flags]
    internal enum BeamSideDotRole { UpperHorizontal=1, UpperCorner=2, LowerCorner=4, LowerHorizontal=8, OuterVertical=16 }
    internal struct BeamSideDot
    {
        internal readonly ContourPoint Center;
        internal readonly BeamSideDotRole Role;
        internal BeamSideDot(ContourPoint center,BeamSideDotRole role) { Center=center;Role=role; }
    }
    internal sealed class BeamSideDotSpan
    {
        internal readonly int First,Last,MinimumAdditionalCount;
        internal BeamSideDotSpan(int first,int last,int count) { First=first;Last=last;MinimumAdditionalCount=count; }
    }
    internal sealed class BeamSideRebarLayout
    {
        internal const string NodeType="BeamSideBlock";
        internal readonly BeamSideGeometry Geometry;
        internal readonly BeamSideRebarParameters Parameters;
        internal readonly ReadOnlyCollection<BeamSideVertex> Line;
        internal readonly ReadOnlyCollection<BeamSideDot> Dots;
        internal readonly ReadOnlyCollection<BeamSideDotSpan> DotSpans;
        internal readonly BeamSideBoundaryStatus BoundaryStatus;
        internal readonly string BoundaryMessage,LineSpecification,DotSpecification;
        internal readonly ContourPoint WidthA,WidthB,HeightA,HeightB,LineLabelAnchor,DotLabelAnchor;
        internal readonly double HorizontalInsideLength,HorizontalDepth,TopInsideLength,BottomInsideLength;
        internal double LineWidth { get { return Parameters.LineDisplayWidth; } }
        internal double DotDisplayDiameter { get { return Parameters.DotDisplayDiameter; } }
        internal double Width { get { return Geometry.Block.Width; } }
        internal double Height { get { return Geometry.Block.Height; } }
        internal double Elevation { get { return Geometry.Elevation; } }
        internal BeamSideRebarLayout(BeamSideGeometry geometry,BeamSideRebarParameters parameters,
            List<BeamSideVertex> line,List<BeamSideDot> dots,List<BeamSideDotSpan> spans,double a,
            double topLength,double bottomLength)
        {
            Geometry=geometry;Parameters=parameters;
            Line=new List<BeamSideVertex>(line).AsReadOnly();Dots=new List<BeamSideDot>(dots).AsReadOnly();
            DotSpans=new List<BeamSideDotSpan>(spans).AsReadOnly();
            HorizontalInsideLength=a;HorizontalDepth=a+parameters.Radius;
            TopInsideLength=topLength;BottomInsideLength=bottomLength;
            BoundaryStatus=geometry.Beam==null?BeamSideBoundaryStatus.Unknown:BeamSideBoundaryStatus.Checked;
            BoundaryMessage=geometry.Beam==null?"梁边界未知，梁内端部需人工审核。":"已检查梁内显示轮廓几何容纳；不代表锚固或结构规范校核。";
            LineSpecification=Spec(parameters.LineDiameter,parameters.LineSpacing);
            DotSpecification=Spec(parameters.DotDiameter,parameters.DotSpacing);
            var block=geometry.Block;
            WidthA=new ContourPoint(block.Left,block.Top);WidthB=new ContourPoint(block.Right,block.Top);
            double heightX=geometry.Direction==BeamSideDirection.Right?block.Right:block.Left;
            HeightA=new ContourPoint(heightX,block.Bottom);HeightB=new ContourPoint(heightX,block.Top);
            // Prefer a real dot nearest the block mid-height, then move only
            // the line contact upwards on its true outer vertical straight.
            double mid=(line[5].Y+line[6].Y)/2,margin=36/(2*Math.Sqrt(2))+5;
            double low=line[6].Y+margin,high=line[5].Y-margin;
            double separation=1.6*parameters.TextHeight+Math.Max(4,.08*parameters.TextHeight);
            var choices=dots.Select(dot=>new { Dot=dot.Center,Y=Math.Max(low,Math.Max(mid,dot.Center.Y+separation)) })
                .Where(c=>low<=high&&c.Y<=high+1e-8)
                .OrderBy(c=>Math.Round(Math.Abs(c.Dot.Y-mid),8)).ThenBy(c=>c.Dot.Y).ToList();
            LineLabelAnchor=new ContourPoint(line[5].X,choices.Count>0?choices[0].Y:mid);
            DotLabelAnchor=choices.Count>0?choices[0].Dot:dots.OrderBy(d=>Math.Abs(d.Center.Y-mid)).ThenBy(d=>d.Center.Y).First().Center;
        }
        private static string Spec(double diameter,double spacing)
        { return "%%132"+diameter.ToString("g",CultureInfo.InvariantCulture)+"@"+spacing.ToString("g",CultureInfo.InvariantCulture); }
    }
    internal static class BeamSideRebarGeometry
    {
        private const double E=BeamSideMath.Epsilon;
        internal static BeamSideRebarLayout Calculate(BeamSideGeometry geometry,BeamSideRebarParameters p)
        {
            if(geometry==null||p==null) throw new InvalidOperationException("缺少梁侧挂板几何或参数。");
            BeamSideMath.Finite(geometry.Block.Width*geometry.Block.Width,geometry.Block.Height*geometry.Block.Height,
                p.Length*p.Length,p.Vertical*p.Vertical,p.Radius*p.Radius);
            double inset=p.Cover+p.LineDisplayWidth/2;
            double top=geometry.Block.Height-inset,bottom=inset,outer=geometry.Block.Width-inset;
            double a=p.Length-p.Vertical-Math.PI*p.Radius/2;
            if(a<=E) throw new InvalidOperationException("L-V-πR/2 必须大于 0；不会自动调整设计值。");
            if(outer-p.Radius<=E || top-bottom-2*p.Radius<=E)
                throw new InvalidOperationException("块体空间不足以容纳固定 R 及有效直段。");
            double t=p.TopUp?1:-1,b=p.BottomUp?1:-1,r=p.Radius,q=Math.Tan(Math.PI/8);
            // Explicit connection vertices make each L measurable independently
            // of the block's horizontal run. Y bends always refer to WCS up/down.
            var local=new List<BeamSideVertex> {
                new BeamSideVertex(-a-r,top+t*(r+p.Vertical),0),
                new BeamSideVertex(-a-r,top+t*r,t*q),
                new BeamSideVertex(-a,top,0),new BeamSideVertex(0,top,0),
                new BeamSideVertex(outer-r,top,-q),new BeamSideVertex(outer,top-r,0),
                new BeamSideVertex(outer,bottom+r,-q),new BeamSideVertex(outer-r,bottom,0),
                new BeamSideVertex(0,bottom,0),new BeamSideVertex(-a,bottom,-b*q),
                new BeamSideVertex(-a-r,bottom+b*r,0),
                new BeamSideVertex(-a-r,bottom+b*(r+p.Vertical),0) };
            var world=new List<BeamSideVertex>();
            foreach(var vertex in local)
            {
                var point=geometry.ToWorld(vertex.Point);
                BeamSideMath.Finite(point.X,point.Y);
                world.Add(new BeamSideVertex(point.X,point.Y,(int)geometry.Direction*vertex.Bulge));
            }
            var curves=new List<BeamSideCurve>();var strokes=new List<BeamSideStroke>();
            for(int i=0;i<world.Count-1;i++)
            {
                var c=new BeamSideCurve(world[i].Point,world[i+1].Point,world[i].Bulge);
                if(c.IsArc && (Math.Abs(c.Radius-r)>E || Math.Abs(Math.Abs(c.Sweep)-Math.PI/2)>E))
                    throw new InvalidOperationException("圆弧半径或弯折角度校验失败。");
                if(i>0)
                {
                    var previous=curves[i-1].Tangent(1);var next=c.Tangent(0);
                    if(Math.Abs(previous.X-next.X)>E||Math.Abs(previous.Y-next.Y)>E)
                        throw new InvalidOperationException("线筋在切点不相切。");
                }
                curves.Add(c);strokes.Add(new BeamSideStroke(c,p.LineDisplayWidth));
            }
            for(int i=0;i<curves.Count;i++) for(int j=0;j<i-1;j++)
            {
                if(BeamSideCurve.Intersections(curves[i],curves[j]).Count>0)
                    throw new InvalidOperationException("线筋中心线自交、接触或重叠（段 "+j+" / "+i+"）。");
                if(strokes[i].Overlaps(strokes[j]))
                    throw new InvalidOperationException("线筋显示轮廓重叠（段 "+j+" / "+i+"）。");
            }
            if(geometry.Beam!=null)
                foreach(int i in new[]{0,1,2,8,9,10})
                    strokes[i].CheckInside(geometry.Beam,i<3?"上端":"下端");
            var distribution=BeamSideDots.Calculate(geometry.Block.Width,geometry.Block.Height,p.Cover,
                p.LineDisplayWidth,p.DotDisplayDiameter,p.Radius,p.DotSpacing);
            var dots=new List<BeamSideDot>();
            foreach(var dot in distribution.Dots) dots.Add(new BeamSideDot(geometry.ToWorld(dot.Center),dot.Role));
            double topLength=curves.Take(3).Sum(c=>c.Length),bottomLength=curves.Skip(8).Sum(c=>c.Length);
            if(Math.Abs(topLength-p.Length)>E || Math.Abs(bottomLength-p.Length)>E)
                throw new InvalidOperationException("梁内 L 展开长度校验失败。");
            return new BeamSideRebarLayout(geometry,p,world,dots,distribution.Spans,a,topLength,bottomLength);
        }
    }
}
