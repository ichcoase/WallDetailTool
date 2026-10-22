using System;
using System.Collections.Generic;
using System.Globalization;

namespace WallDetailTool
{
    internal struct AnnotationPoint
    {
        internal double X, Y;
        internal AnnotationPoint(double x, double y) { X=x; Y=y; }
    }

    internal struct AnnotationSegment
    {
        internal AnnotationPoint A, B;
        internal AnnotationSegment(AnnotationPoint a, AnnotationPoint b) { A=a; B=b; }
    }

    internal sealed class UpstandLabelLayout
    {
        internal string Text;
        internal AnnotationPoint TextAnchor;
        internal bool Rightward;
        internal readonly List<AnnotationSegment> Leaders=new List<AnnotationSegment>();
    }

    internal sealed class UpstandAnnotationLayout
    {
        internal UpstandLabelLayout LineLabel, DotLabel;
        internal AnnotationPoint WidthA, WidthB, WidthLine;
        internal AnnotationPoint HeightA, HeightB, HeightLine;
        internal double TextHeight, Elevation;
        internal readonly List<string> Warnings=new List<string>();
    }

    // CAD-independent layout. Measurements always come from the concrete geometry.
    internal static class UpstandAnnotationGeometry
    {
        private struct Box
        {
            internal double L,R,B,T;
            internal Box(double l,double r,double b,double t) { L=l; R=r; B=b; T=t; }
        }
        internal static UpstandAnnotationLayout Calculate(UpstandGeometry g, UpstandRebarLayout steel,
            UpstandRebarParameters p, double textHeight, double viewLeft, double viewRight)
        {
            if (g==null || steel==null || p==null || steel.Line.Count<5 ||
                steel.LeftDots.Count==0 || steel.LeftDots.Count!=steel.RightDots.Count)
                throw new InvalidOperationException("缺少完整的反坎钢筋布局，无法计算标注。");
            if (!Valid(textHeight) || textHeight<=0)
                throw new InvalidOperationException("标注文字高度必须为有效正数。");
            if (!Valid(viewLeft) || !Valid(viewRight) || viewRight<=viewLeft)
                throw new InvalidOperationException("当前视图范围无效。");
            var result=new UpstandAnnotationLayout { TextHeight=textHeight, Elevation=g.Elevation };
            double offset=Math.Max(20,.5*textHeight);
            string lineText=Spec(p.LineDiameter,p.LineSpacing);
            string dotText=steel.LeftDots.Count==1 ? "2φ"+Num(p.DotDiameter) : Spec(p.DotDiameter,p.DotSpacing);
            double run=Math.Max(100,Math.Max(lineText.Length,dotText.Length)*textHeight*.8);
            double need=run+offset;
            double freeLeft=g.Left-viewLeft, freeRight=viewRight-g.Right;
            bool right=freeRight>=freeLeft;
            if (right && freeRight<need && freeLeft>=need) right=false;
            if (!right && freeLeft<need && freeRight>=need) right=true;
            if (Math.Max(freeLeft,freeRight)<need)
                result.Warnings.Add("视图两侧空间均不足，标注使用备用位置；请人工检查文字和引线。");

            double side=right ? 1 : -1;
            double edge=right ? g.Right : g.Left;
            double labelX=edge+side*offset;
            // Place both labels beside the upstand, as in a structural detail drawing.
            double lineY=g.BeamTop+.65*g.Height;
            int last=steel.LeftDots.Count-1;
            double dotY=steel.LeftDots[last].Y;
            if (Math.Abs(dotY-lineY)<1.2*textHeight)
            {
                dotY=g.BeamTop+.22*g.Height;
                result.Warnings.Add("点筋排位与线筋文字接近，点筋标注使用备用高度；请人工检查。");
            }
            var lineTouch=new AnnotationPoint(right ? steel.Line[5].X : steel.Line[2].X,
                lineY+Math.Min(offset,20));
            result.LineLabel=Label(lineText,right,labelX,lineY,run,lineTouch,
                lineTouch.X+side*Math.Min(offset,20));
            ContourPoint dot=right ? steel.LeftDots[last] : steel.RightDots[last];
            result.DotLabel=Label(dotText,right,labelX,dotY,run,new AnnotationPoint(dot.X,dot.Y),labelX);
            var concrete=new Box(g.Left,g.Right,g.BeamTop,g.Top);
            Box lineBox=TextBounds(result.LineLabel,textHeight);
            Box dotBox=TextBounds(result.DotLabel,textHeight);
            if (Overlaps(lineBox,dotBox) || Overlaps(lineBox,concrete) ||
                Overlaps(dotBox,concrete) || LeaderHits(result.LineLabel,dotBox) ||
                LeaderHits(result.DotLabel,lineBox))
            {
                // One local fallback only; more complex global annotation packing belongs to later work.
                dotY=g.BeamTop-Math.Max(45,2*textHeight);
                result.DotLabel=Label(dotText,right,labelX,dotY,run,new AnnotationPoint(dot.X,dot.Y),labelX);
                result.Warnings.Add("局部标注发生碰撞，点筋标注改用备用位置；请人工检查。");
            }
            result.WidthA=new AnnotationPoint(g.Left,g.Top);
            result.WidthB=new AnnotationPoint(g.Right,g.Top);
            result.WidthLine=new AnnotationPoint((g.Left+g.Right)/2,g.Top+Math.Max(50,1.5*textHeight));
            // Prefer the opposite side, but keep the dimension on the side with actual room.
            double dimOffset=Math.Max(50,1.5*textHeight);
            bool dimLeft=right;
            if (dimLeft && freeLeft<dimOffset+textHeight && freeRight>=dimOffset+textHeight) dimLeft=false;
            if (!dimLeft && freeRight<dimOffset+textHeight && freeLeft>=dimOffset+textHeight) dimLeft=true;
            if (freeLeft<dimOffset+textHeight && freeRight<dimOffset+textHeight)
                result.Warnings.Add("高度尺寸两侧空间不足，请人工检查。");
            double dimX=dimLeft ? g.Left-dimOffset : g.Right+dimOffset;
            result.HeightA=new AnnotationPoint(dimLeft ? g.Left : g.Right,g.BeamTop);
            result.HeightB=new AnnotationPoint(dimLeft ? g.Left : g.Right,g.Top);
            result.HeightLine=new AnnotationPoint(dimX,(g.BeamTop+g.Top)/2);
            return result;
        }

        private static UpstandLabelLayout Label(string value,bool right,double x,double y,double run,
            AnnotationPoint touch,double elbowX)
        {
            var label=new UpstandLabelLayout { Text=value,Rightward=right,
                TextAnchor=new AnnotationPoint(x,y+Math.Max(3,run/80)) };
            var elbow=new AnnotationPoint(elbowX,y);
            label.Leaders.Add(new AnnotationSegment(touch,elbow));
            label.Leaders.Add(new AnnotationSegment(elbow,new AnnotationPoint(x+(right ? run : -run),y)));
            return label;
        }
        private static string Spec(double diameter,double spacing)
        { return "φ"+Num(diameter)+"@"+Num(spacing); }
        private static string Num(double value)
        { return value.ToString("0.###",CultureInfo.InvariantCulture); }
        private static bool Valid(double v)
        { return !Double.IsNaN(v) && !Double.IsInfinity(v); }
        private static Box TextBounds(UpstandLabelLayout label,double height)
        {
            double width=Math.Max(height,label.Text.Length*height*.8);
            double x=label.TextAnchor.X;
            return label.Rightward ? new Box(x,x+width,label.TextAnchor.Y,label.TextAnchor.Y+height) :
                new Box(x-width,x,label.TextAnchor.Y,label.TextAnchor.Y+height);
        }
        private static bool Overlaps(Box a,Box b)
        { return a.L<b.R && a.R>b.L && a.B<b.T && a.T>b.B; }
        private static bool LeaderHits(UpstandLabelLayout label,Box text)
        {
            foreach (AnnotationSegment segment in label.Leaders)
            {
                double l=Math.Min(segment.A.X,segment.B.X),r=Math.Max(segment.A.X,segment.B.X);
                double b=Math.Min(segment.A.Y,segment.B.Y),t=Math.Max(segment.A.Y,segment.B.Y);
                if (l<text.R && r>text.L && b<text.T && t>text.B) return true;
            }
            return false;
        }
    }
}
