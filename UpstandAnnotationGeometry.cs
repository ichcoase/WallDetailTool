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
        internal bool HasSlash;
        internal AnnotationPoint SlashCenter, SlashA, SlashB;
        internal double SlashWidth;
        internal bool KeepLastLeaderEnd;
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
                steel.LeftDots.Count==0 || steel.LeftDots.Count!=steel.RightDots.Count ||
                steel.CornerDots.Count!=2)
                throw new InvalidOperationException("缺少完整的反坎钢筋布局，无法计算标注。");
            if (!Valid(textHeight) || textHeight<=0)
                throw new InvalidOperationException("标注文字高度必须为有效正数。");
            if (!Valid(viewLeft) || !Valid(viewRight) || viewRight<=viewLeft)
                throw new InvalidOperationException("当前视图范围无效。");
            var result=new UpstandAnnotationLayout { TextHeight=textHeight, Elevation=g.Elevation };
            double offset=Math.Max(20,.5*textHeight);
            string lineText=Spec(p.LineDiameter,p.LineSpacing);
            int dotsPerRow=2+steel.TopDots.Count+steel.CornerDots.Count;
            string dotText=steel.LeftDots.Count==1 ? dotsPerRow.ToString(CultureInfo.InvariantCulture)+"φ"+Num(p.DotDiameter) :
                Spec(p.DotDiameter,p.DotSpacing);
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
            double lineY=Math.Min(g.BeamTop+.65*g.Height,steel.Line[2].Y-5);
            if (lineY<=steel.Line[1].Y)
                throw new InvalidOperationException("线筋竖段过短，无法将斜杠放在线筋中心线上。");
            int last=steel.LeftDots.Count-1;
            double dotY=steel.LeftDots[last].Y;
            if (Math.Abs(dotY-lineY)<1.2*textHeight)
            {
                dotY=g.BeamTop+.22*g.Height;
                result.Warnings.Add("点筋排位与线筋文字接近，点筋标注使用备用高度；请人工检查。");
            }
            var lineTouch=new AnnotationPoint(right ? steel.Line[5].X : steel.Line[2].X,lineY);
            result.LineLabel=Label(lineText,right,labelX,lineY,lineTouch,lineTouch.X);
            double slashHalf=36/(2*Math.Sqrt(2));
            result.LineLabel.HasSlash=true;
            result.LineLabel.SlashCenter=lineTouch;
            result.LineLabel.SlashA=new AnnotationPoint(lineTouch.X-slashHalf,lineTouch.Y+slashHalf);
            result.LineLabel.SlashB=new AnnotationPoint(lineTouch.X+slashHalf,lineTouch.Y-slashHalf);
            result.LineLabel.SlashWidth=10;
            ContourPoint dot=right ? steel.LeftDots[last] : steel.RightDots[last];
            result.DotLabel=last==0 ? SingleRowDotLabel(dotText,g,steel,textHeight,freeLeft,freeRight) :
                Label(dotText,right,labelX,dotY,new AnnotationPoint(dot.X,dot.Y),labelX);
            var concrete=new Box(g.Left,g.Right,g.BeamTop,g.Top);
            Box lineBox=TextBounds(result.LineLabel,textHeight);
            Box dotBox=TextBounds(result.DotLabel,textHeight);
            if (Overlaps(lineBox,dotBox) || Overlaps(lineBox,concrete) ||
                Overlaps(dotBox,concrete) || LeaderHits(result.LineLabel,dotBox) ||
                LeaderHits(result.DotLabel,lineBox))
            {
                // One local fallback only; more complex global annotation packing belongs to later work.
                if (last==0)
                {
                    result.Warnings.Add("单排点筋上方标注与其他内容接近；请人工检查位置。");
                }
                else
                {
                    dotY=g.BeamTop-Math.Max(45,2*textHeight);
                    result.DotLabel=Label(dotText,right,labelX,dotY,new AnnotationPoint(dot.X,dot.Y),labelX);
                    result.Warnings.Add("局部标注发生碰撞，点筋标注改用备用位置；请人工检查。");
                }
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

        private static UpstandLabelLayout SingleRowDotLabel(string value,UpstandGeometry g,
            UpstandRebarLayout steel,double height,double freeLeft,double freeRight)
        {
            // One shared underline with a branch to every dot in the single row.
            bool onLeft=freeLeft>=freeRight;
            double y=g.Top+Math.Max(25,.55*height);
            double rise=y-steel.LeftDots[0].Y;
            double shift=Math.Min(g.Width*.3,rise*.55)*(onLeft ? -1 : 1);
            double anchorX=onLeft ? g.Left-Math.Max(60,1.5*height) :
                g.Right+Math.Max(60,1.5*height);
            var label=new UpstandLabelLayout { Text=value,Rightward=onLeft,KeepLastLeaderEnd=true,
                TextAnchor=new AnnotationPoint(anchorX,y+4) };
            var row=new List<ContourPoint> { steel.LeftDots[0] };
            row.Add(steel.CornerDots[0]);
            row.AddRange(steel.TopDots);
            row.Add(steel.CornerDots[1]);
            row.Add(steel.RightDots[0]);
            foreach (ContourPoint dot in row)
                label.Leaders.Add(new AnnotationSegment(new AnnotationPoint(dot.X,dot.Y),
                    new AnnotationPoint(dot.X+shift,y)));
            label.Leaders.Add(new AnnotationSegment(
                new AnnotationPoint(onLeft ? anchorX : row[0].X+shift,y),
                new AnnotationPoint(onLeft ? row[row.Count-1].X+shift : anchorX,y)));
            return label;
        }

        private static UpstandLabelLayout Label(string value,bool right,double x,double y,
            AnnotationPoint touch,double elbowX)
        {
            var label=new UpstandLabelLayout { Text=value,Rightward=right,
                TextAnchor=new AnnotationPoint(x,y+4) };
            var elbow=new AnnotationPoint(elbowX,y);
            if (Math.Abs(touch.X-elbow.X)>1e-8 || Math.Abs(touch.Y-elbow.Y)>1e-8)
                label.Leaders.Add(new AnnotationSegment(touch,elbow));
            // The CAD adapter extends this endpoint to the actual MTEXT width, never beyond it.
            label.Leaders.Add(new AnnotationSegment(elbow,new AnnotationPoint(x,y)));
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
