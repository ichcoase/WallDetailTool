using System;
using System.Collections.Generic;
using System.Globalization;

namespace WallDetailTool
{
    // Points are already in drawing units; the displayed value always remains mm.
    internal sealed class EndLengthNote
    {
        internal readonly string Name,Text;
        internal readonly AnnotationPoint A,B;
        internal readonly bool Vertical;
        internal readonly double Height,Gap,StrokeWidth,Normal;
        internal double Rotation { get { return Vertical?Math.PI/2:0; } }
        internal EndLengthNote(string name,AnnotationPoint a,AnnotationPoint b,double millimetres,
            double heightMm,double scale,double strokeWidth,double normal)
        {
            foreach(double n in new[]{a.X,a.Y,b.X,b.Y,millimetres,heightMm,scale,strokeWidth,normal})
                if(double.IsNaN(n)||double.IsInfinity(n))throw new ArgumentException("端部文字数值无效。");
            if(millimetres<=0||heightMm<=0||scale<=0||strokeWidth<=0||Math.Abs(normal)!=1)
                throw new ArgumentException("端部文字参数无效。");
            Vertical=Math.Abs(a.X-b.X)<1e-8;
            if(Vertical?Math.Abs(a.Y-b.Y)<1e-12:Math.Abs(a.Y-b.Y)>1e-8)
                throw new ArgumentException("端部文字需要真实水平或竖直直段。");
            Name=name;A=a;B=b;Text=millimetres.ToString("0.###",CultureInfo.InvariantCulture);
            Height=heightMm*scale;Gap=Math.Max(8,.2*heightMm)*scale;StrokeWidth=strokeWidth;Normal=normal;
        }
    }
    internal struct EndNoteBox
    {
        internal readonly double Left,Bottom,Right,Top;
        internal double CenterX { get { return (Left+Right)/2; } }
        internal double CenterY { get { return (Bottom+Top)/2; } }
        internal EndNoteBox(double left,double bottom,double right,double top)
        { Left=left;Bottom=bottom;Right=right;Top=top; }
        internal EndNoteBox Expand(double gap) { return new EndNoteBox(Left-gap,Bottom-gap,Right+gap,Top+gap); }
        internal bool Contains(double x,double y) { return x>=Left&&x<=Right&&y>=Bottom&&y<=Top; }
        internal bool Overlaps(EndNoteBox other)
        { return Left<other.Right&&Right>other.Left&&Bottom<other.Top&&Top>other.Bottom; }
    }
    internal static class EndLengthAnnotationGeometry
    {
        private static AnnotationPoint Point(double x,double y,double scale) { return new AnnotationPoint(x*scale,y*scale); }
        internal static List<EndLengthNote> Cantilever(CantileverFrame frame,CantileverDrawRequest r,CantileverPathInputs p,CantileverRebarLayout steel)
        {
            var result=new List<EndLengthNote>();
            if(r.Mode!=CantileverSupportMode.NoInnerUp&&r.Mode!=CantileverSupportMode.NoInnerDown)return result;
            var v=steel.Bars[0].Vertices;
            result.Add(new EndLengthNote("支座V",Point(v[0].Point.X,v[0].Point.Y,r.Scale),Point(v[1].Point.X,v[1].Point.Y,r.Scale),
                p.V,r.Advanced(4),r.Scale,p.DisplayWidth*r.Scale,-(int)frame.Direction));
            return result;
        }
        internal static List<EndLengthNote> BeamSide(BeamSideRebarLayout steel,double scale)
        {
            var p=steel.Parameters;var v=steel.Line;var result=new List<EndLengthNote>();
            foreach(int i in new[]{0,10}) result.Add(new EndLengthNote(i==0?"上端V":"下端V",
                Point(v[i].X,v[i].Y,scale),Point(v[i+1].X,v[i+1].Y,scale),p.Vertical,p.TextHeight,scale,steel.LineWidth*scale,-(int)steel.Geometry.Direction));
            return result;
        }
        internal static List<EndLengthNote> Slab(SlabUpstandRebarLayout steel,SlabUpstandRebarParameters p,bool hanging,double heightMm,double scale)
        {
            // Legacy slab vertices are already world drawing coordinates. Do not
            // rescale the accepted geometry; only new text height/gaps use scale.
            var v=steel.Line;var result=new List<EndLengthNote>();
            foreach(int i in new[]{0,8}) result.Add(new EndLengthNote(i==0?"左端L":"右端L",
                Point(v[i].X,v[i].Y,1),Point(v[i+1].X,v[i+1].Y,1),i==0?p.LeftLength:p.RightLength,
                heightMm,scale,steel.LineWidth,hanging?1:-1));
            return result;
        }
        internal static EndNoteBox Place(EndLengthNote note,double measuredWidth,double measuredHeight,Func<EndNoteBox,bool> blocked)
        {
            if(measuredWidth<=0||measuredHeight<=0||double.IsInfinity(measuredWidth+measuredHeight)||double.IsNaN(measuredWidth+measuredHeight))
                throw new ArgumentException("无法测量端部TEXT范围。");
            double half=note.Vertical?measuredWidth/2:measuredHeight/2;
            foreach(double ring in new[]{0.0,.5,1,2,3})foreach(double side in new[]{note.Normal,-note.Normal})
            foreach(double fraction in new[]{.5,.3,.7})
            {
                double x=note.A.X+(note.B.X-note.A.X)*fraction,y=note.A.Y+(note.B.Y-note.A.Y)*fraction;
                double offset=half+note.Gap+note.StrokeWidth/2+ring*note.Height;
                if(note.Vertical)x+=side*offset;else y+=side*offset;
                var box=new EndNoteBox(x-measuredWidth/2,y-measuredHeight/2,x+measuredWidth/2,y+measuredHeight/2);
                if(!blocked(box))return box;
            }
            throw new InvalidOperationException("端部长度文字没有可用空位，请人工布置（不会移动钢筋或原标注）。");
        }
    }
}
