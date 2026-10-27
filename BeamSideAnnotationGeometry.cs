using System;
using System.Linq;

namespace WallDetailTool
{
    // Annotation placement only. Steel positions/specifications come exclusively
    // from the completed calculator; output coordinates are drawing units.
    internal static class BeamSideAnnotationGeometry
    {
        internal static UpstandAnnotationLayout Calculate(BeamSideRebarLayout steel,double drawingScale)
        {
            if(steel==null) throw new InvalidOperationException("缺少梁侧挂板计算结果。");
            var unit=new BeamSideUnitTransform(drawingScale);var p=steel.Parameters;var block=steel.Geometry.Block;
            double left=Math.Min(block.Left,steel.Line.Min(v=>v.X)-steel.LineWidth/2);
            double right=Math.Max(block.Right,steel.Line.Max(v=>v.X)+steel.LineWidth/2);
            double top=block.Top;
            if(steel.Geometry.Beam!=null)
            { left=Math.Min(left,steel.Geometry.Beam.Left);right=Math.Max(right,steel.Geometry.Beam.Right);top=Math.Max(top,steel.Geometry.Beam.Top); }
            double offset=Math.Max(50,1.5*p.TextHeight),side=p.AnnotationRight?1:-1;
            double x=(p.AnnotationRight?right:left)+side*offset;
            double lineY=steel.LineLabelAnchor.Y,dotY=lineY-2.4*p.TextHeight;
            var result=new UpstandAnnotationLayout { TextHeight=unit.LengthToDrawing(p.TextHeight),Elevation=unit.LengthToDrawing(steel.Elevation) };
            result.LineLabel=Label(steel.LineSpecification,steel.LineLabelAnchor,x,lineY,p.AnnotationRight,p.TextHeight,unit);
            result.DotLabel=Label(steel.DotSpecification,steel.DotLabelAnchor,x,dotY,p.AnnotationRight,p.TextHeight,unit);
            double slash=36/(2*Math.Sqrt(2));var touch=steel.LineLabelAnchor;
            result.LineLabel.HasSlash=true;result.LineLabel.SlashCenter=Point(touch,unit);
            result.LineLabel.SlashA=Point(new ContourPoint(touch.X-slash,touch.Y+slash),unit);
            result.LineLabel.SlashB=Point(new ContourPoint(touch.X+slash,touch.Y-slash),unit);
            result.LineLabel.SlashWidth=unit.LengthToDrawing(10);
            result.WidthA=Point(steel.WidthA,unit);result.WidthB=Point(steel.WidthB,unit);
            result.WidthLine=Point(new ContourPoint((steel.WidthA.X+steel.WidthB.X)/2,top+offset),unit);
            result.HeightA=Point(steel.HeightA,unit);result.HeightB=Point(steel.HeightB,unit);
            result.HeightLine=Point(new ContourPoint(p.AnnotationRight?left-offset:right+offset,(steel.HeightA.Y+steel.HeightB.Y)/2),unit);
            return result;
        }
        private static AnnotationPoint Point(ContourPoint point,BeamSideUnitTransform unit)
        { var q=unit.ToDrawing(point);return new AnnotationPoint(q.X,q.Y); }
        private static UpstandLabelLayout Label(string text,ContourPoint anchor,double x,double y,bool right,double height,BeamSideUnitTransform unit)
        {
            var label=new UpstandLabelLayout { Text=text,Rightward=right,TextAnchor=Point(new ContourPoint(x,y+Math.Max(4,height*.08)),unit) };
            var elbow=Point(new ContourPoint(x,y),unit);
            label.Leaders.Add(new AnnotationSegment(Point(anchor,unit),elbow));
            label.Leaders.Add(new AnnotationSegment(elbow,elbow)); // CAD extends to actual TEXT extents.
            return label;
        }
    }
}
