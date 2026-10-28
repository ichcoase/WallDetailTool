using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WallDetailTool
{
    internal sealed class CantileverLabel
    {
        internal readonly string Name;
        internal readonly UpstandLabelLayout Layout;
        internal CantileverLabel(string name,UpstandLabelLayout layout) { Name=name;Layout=layout; }
    }
    internal sealed class CantileverAnnotationResult
    {
        internal readonly List<CantileverLabel> Labels=new List<CantileverLabel>();
        internal readonly UpstandAnnotationLayout Dimensions=new UpstandAnnotationLayout();
        internal CantileverLinDimension Lin;
        internal AnnotationPoint LinLine;
    }
    internal static class CantileverAnnotationGeometry
    {
        internal static CantileverAnnotationResult Calculate(CantileverFrame frame,CantileverDrawRequest request,
            CantileverPathInputs p,CantileverRebarLayout steel,CantileverDotLayout dots,double elevation)
        {
            var result=new CantileverAnnotationResult();double scale=request.Scale;
            double h=request.Advanced(4),offset=Math.Max(50,1.5*h),sign=(int)frame.Direction;
            var points=steel.Bars.SelectMany(b=>b.Vertices).Select(v=>v.Point).ToArray();
            double left=Math.Min(frame.SupportX,points.Min(q=>q.X))-offset;
            double right=Math.Max(frame.SupportX,points.Max(q=>q.X))+offset;
            double highest=Math.Max(frame.TopY,points.Max(q=>q.Y));
            double lowest=Math.Min(frame.TopY-p.Thickness,points.Min(q=>q.Y));
            foreach(var bar in steel.Bars)
            {
                bool outward=bar.Specification.Role!=CantileverBarRole.Additional;
                bool rightward=outward?sign>0:sign<0;
                var v=bar.Vertices;
                var a=v.Count==2?v[0].Point:v[v.Count-4].Point;
                var b=v.Count==2?v[1].Point:v[v.Count-3].Point;
                bool isBottom=bar.Specification.Role==CantileverBarRole.Bottom;
                double fraction=bar.Specification.Role==CantileverBarRole.Additional?.25:.75;
                var touch=new CantileverPoint(a.X+(b.X-a.X)*fraction,a.Y+(b.Y-a.Y)*fraction);
                if(!isBottom)
                {
                    // Keep the two top anchors ordered within their common
                    // actual straight span, even for very different E and Lin.
                    double start=(a.X-frame.SupportX)*sign,end=(b.X-frame.SupportX)*sign;
                    if(request.Mode==CantileverSupportMode.InnerWithAdditional)
                        start=Math.Max((steel.Bars[0].Vertices[0].Point.X-frame.SupportX)*sign,
                            (steel.Bars[2].Vertices[0].Point.X-frame.SupportX)*sign);
                    if(end>0) start=Math.Max(start,0);
                    touch=frame.ToWorld(start+(end-start)*fraction,touch.Y-frame.TopY);
                }
                string prefix=bar.Specification.Role==CantileverBarRole.Additional?"附加筋 ":bar.Specification.Role==CantileverBarRole.InnerExtension?"内跨伸出筋 ":bar.Specification.Role==CantileverBarRole.Bottom?"底筋 ":"面筋 ";
                var label=Label(prefix+Spec(bar.Specification.Diameter,bar.Specification.Spacing),touch,rightward?right:left,
                    isBottom?touch.Y:highest+offset,rightward,h,scale,!isBottom);
                double slash=36/(2*Math.Sqrt(2));label.HasSlash=true;
                label.SlashCenter=Point(touch,scale);label.SlashA=Point(new CantileverPoint(touch.X-sign*slash,touch.Y+slash),scale);
                label.SlashB=Point(new CantileverPoint(touch.X+sign*slash,touch.Y-slash),scale);label.SlashWidth=10*scale;
                result.Labels.Add(new CantileverLabel(prefix.Trim(),label));
            }
            bool dotRight=sign<0;var dot=dots.Lower[dots.Lower.Count-1];
            int total=dots.Upper.Count+dots.Lower.Count;
            string dotSpec=total==2?"2φ"+request.Value(13).ToString("0.###",CultureInfo.InvariantCulture):Spec(request.Value(13),request.Value(14));
            result.Labels.Add(new CantileverLabel("上下两排点筋",Label("点筋 "+dotSpec,dot,dotRight?right:left,lowest-offset,dotRight,h,scale,true)));
            var dimensions=result.Dimensions;dimensions.TextHeight=h*scale;dimensions.Elevation=elevation*scale;
            dimensions.WidthA=Point(frame.ToWorld(0,0),scale);dimensions.WidthB=Point(frame.ToWorld(p.Length,0),scale);
            dimensions.WidthLine=Point(new CantileverPoint((dimensions.WidthA.X+dimensions.WidthB.X)/scale/2,highest+3*offset),scale);
            dimensions.HeightA=Point(frame.ToWorld(p.Length,-p.Thickness),scale);dimensions.HeightB=Point(frame.ToWorld(p.Length,0),scale);
            dimensions.HeightLine=Point(frame.ToWorld(p.Length+offset,-p.Thickness/2),scale);
            if(request.Mode==CantileverSupportMode.InnerWithAdditional)
            {
                result.Lin=frame.LinDimension(p.Lin,0);
                result.LinLine=Point(new CantileverPoint((result.Lin.BeamFace.X+result.Lin.Cutoff.X)/2,highest+4*offset),scale);
            }
            return result;
        }
        private static AnnotationPoint Point(CantileverPoint p,double scale) { return new AnnotationPoint(p.X*scale,p.Y*scale); }
        internal static string Spec(double diameter,double spacing)
        { return "φ"+diameter.ToString("0.###",CultureInfo.InvariantCulture)+"@"+spacing.ToString("0.###",CultureInfo.InvariantCulture); }
        private static UpstandLabelLayout Label(string text,CantileverPoint touch,double x,double rowY,bool right,double height,double scale,bool folded)
        {
            var label=new UpstandLabelLayout { Text=text,Rightward=right,TextAnchor=new AnnotationPoint(x*scale,(rowY+Math.Max(4,.08*height))*scale) };
            var elbow=new AnnotationPoint(x*scale,rowY*scale);
            label.Leaders.Add(new AnnotationSegment(Point(touch,scale),elbow));
            if(folded) label.Leaders.Add(new AnnotationSegment(elbow,new AnnotationPoint((x+(right?1:-1)*height)*scale,rowY*scale)));
            return label;
        }
    }
}
