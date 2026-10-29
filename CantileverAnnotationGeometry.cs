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
        internal CantileverFrame Frame;
        internal CantileverPathInputs Inputs;
        internal CantileverDotLayout Dots;
        internal double Scale;
        internal double BottomStartLocal,BottomEndLocal;
        internal double InnerStartLocal,InnerEndLocal;
        internal double TopStartLocal;
    }
    internal sealed class CantileverTextSize
    {
        internal readonly double Width,Height;
        // Drawing units, supplied by actual CAD TEXT extents (or explicit test metrics).
        internal CantileverTextSize(double width,double height)
        { Width=CantileverNumbers.Positive(width);Height=CantileverNumbers.Positive(height); }
    }
    internal static class CantileverAnnotationGeometry
    {
        internal static CantileverAnnotationResult Calculate(CantileverFrame frame,CantileverDrawRequest request,
            CantileverPathInputs p,CantileverRebarLayout steel,CantileverDotLayout dots,double elevation)
        {
            var result=new CantileverAnnotationResult { Frame=frame,Inputs=p,Dots=dots,Scale=request.Scale };double scale=request.Scale;
            double h=request.Advanced(4),offset=Math.Max(50,1.5*h),sign=(int)frame.Direction;
            var points=steel.Bars.SelectMany(b=>b.Vertices).Select(v=>v.Point).ToArray();
            double gap=Math.Max(4,.08*h);
            double highest=Math.Max(frame.TopY,points.Max(q=>q.Y));
            result.BottomStartLocal=(steel.Bars[1].Vertices[0].Point.X-frame.SupportX)*sign*scale;
            result.BottomEndLocal=(steel.Bars[1].Vertices[1].Point.X-frame.SupportX)*sign*scale;
            foreach(var bar in steel.Bars)
            {
                bool outward=bar.Specification.Role!=CantileverBarRole.InnerExtension;
                bool rightward=outward?sign>0:sign<0;
                var v=bar.Vertices;
                var a=v.Count==2?v[0].Point:v[v.Count-4].Point;
                var b=v.Count==2?v[1].Point:v[v.Count-3].Point;
                bool isBottom=bar.Specification.Role==CantileverBarRole.Bottom;
                var touch=isBottom?new CantileverPoint(a.X+(b.X-a.X)*.75,a.Y):new CantileverPoint(a.X+(b.X-a.X)*.75,a.Y+(b.Y-a.Y)*.75);
                if(!isBottom)
                {
                    double start=(a.X-frame.SupportX)*sign,end=(b.X-frame.SupportX)*sign;
                    if(bar.Specification.Role==CantileverBarRole.InnerExtension)
                    {
                        // The inner-extension group's own straight segment in
                        // the INNER span, never the additional-bar cutoff mark.
                        end=Math.Min(end,-frame.BeamWidth);
                        result.InnerStartLocal=start*scale;result.InnerEndLocal=end*scale;
                        // Stay near the support even if the inner bar is very long.
                        double x=end-Math.Min((end-start)/2,Math.Max(60,2*h));
                        if(request.Mode==CantileverSupportMode.InnerWithAdditional&&Math.Abs(x-frame.CutoffLocalX(p.Lin))<1e-8)
                            x=end-Math.Min((end-start)*.75,Math.Max(90,3*h));
                        touch=frame.ToWorld(x,touch.Y-frame.TopY);
                    }
                    else
                    {
                        if(end>0) start=Math.Max(start,0);
                        result.TopStartLocal=start*scale;
                        touch=frame.ToWorld(start+(end-start)*.75,touch.Y-frame.TopY);
                    }
                }
                string prefix=bar.Specification.Role==CantileverBarRole.Additional?"附加筋 ":bar.Specification.Role==CantileverBarRole.InnerExtension?"内跨伸出筋 ":bar.Specification.Role==CantileverBarRole.Bottom?"底筋 ":"面筋 ";
                if(isBottom) rightward=sign<0; // Prefer an inward horizontal run.
                var label=Label(prefix+Spec(bar.Specification.Diameter,bar.Specification.Spacing),touch,touch.X+(rightward?gap:-gap),
                    isBottom?frame.TopY-p.Thickness-offset:highest+offset,rightward,h,scale);
                double slash=36/(2*Math.Sqrt(2));label.HasSlash=true;
                label.SlashCenter=Point(touch,scale);label.SlashA=Point(new CantileverPoint(touch.X-sign*slash,touch.Y+slash),scale);
                label.SlashB=Point(new CantileverPoint(touch.X+sign*slash,touch.Y-slash),scale);label.SlashWidth=10*scale;
                result.Labels.Add(new CantileverLabel(prefix.Trim(),label));
            }
            // Both real contacts have the same X. Their two vertical branches
            // are emitted as one continuous LINE through both actual centres.
            bool dotRight=sign>0;
            int pair=Enumerable.Range(0,dots.Upper.Count).OrderBy(i=>Math.Abs((dots.Upper[i].X-frame.SupportX)*sign-p.Length*.25)).First();
            var dot=dots.Upper[pair];
            int total=dots.Upper.Count+dots.Lower.Count;
            string dotSpec=total==2?"2φ"+request.Value(13).ToString("0.###",CultureInfo.InvariantCulture):Spec(request.Value(13),request.Value(14));
            result.Labels.Add(new CantileverLabel("上下两排点筋",Label("点筋 "+dotSpec,dot,dot.X+(dotRight?gap:-gap),frame.TopY-p.Thickness-offset,dotRight,h,scale)));
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
        // Final routing uses measured widths; Calculate above only supplies
        // text/specifications and real contact geometry for the measurement pass.
        internal static void ApplyMeasuredLayout(CantileverAnnotationResult result,IList<CantileverTextSize> sizes)
        {
            if(sizes==null||sizes.Count!=result.Labels.Count||sizes.Any(s=>s==null)) throw new ArgumentException("缺少真实TEXT范围。");
            var frame=result.Frame;double scale=result.Scale,sign=(int)frame.Direction;
            double h=result.Dimensions.TextHeight,gap=Math.Max(4*scale,.08*h),clearance=Math.Max(8*scale,.15*h);
            for(int i=0;i<result.Labels.Count;i++)
            {
                var item=result.Labels[i];if(item.Name=="底筋"||item.Name=="上下两排点筋")continue;
                var label=item.Layout;var contact=label.Leaders[0].A;double row=label.Leaders.Last().A.Y;
                if(item.Name=="内跨伸出筋"&&result.Lin!=null)
                {
                    double x=(contact.X-frame.SupportX*scale)*sign;
                    double cutoff=frame.CutoffLocalX(result.Inputs.Lin)*scale;
                    if(x-gap>cutoff-clearance&&x-gap-sizes[i].Width<cutoff+clearance)
                    {
                        // Keep the text left of the Lin extension in local coordinates,
                        // if the actual inner span provides a real contact there.
                        double candidate=cutoff-clearance;
                        if(candidate>result.InnerStartLocal+clearance&&candidate<result.InnerEndLocal-clearance)
                            contact=new AnnotationPoint(frame.SupportX*scale+sign*candidate,contact.Y);
                        else
                            // A short inner span cannot provide that contact. Keep its
                            // real contact and put this text above the Lin dimension row.
                            row=result.LinLine.Y+2*Math.Max(50*scale,1.5*h);
                    }
                }
                else if(item.Name=="面筋"||item.Name=="附加筋")
                {
                    double x=(contact.X-frame.SupportX*scale)*sign;
                    double free=result.Inputs.Length*scale;
                    if(x+gap+sizes[i].Width>free-clearance)
                    {
                        double candidate=free-clearance-gap-sizes[i].Width;
                        if(candidate>result.TopStartLocal+clearance)
                            contact=new AnnotationPoint(frame.SupportX*scale+sign*Math.Min(x,candidate),contact.Y);
                        else row=result.Dimensions.WidthLine.Y+Math.Max(50*scale,1.5*h);
                    }
                }
                Place(label,contact,row,label.Rightward,gap,sizes[i].Width);
                MoveSlash(label,contact.X);
            }
            int di=result.Labels.FindIndex(l=>l.Name=="上下两排点筋"),bi=result.Labels.FindIndex(l=>l.Name=="底筋");
            var dotLabel=result.Labels[di].Layout;var bottomLabel=result.Labels[bi].Layout;
            double support=frame.SupportX*scale,length=result.Inputs.Length*scale;
            double slabBottom=(frame.TopY-result.Inputs.Thickness)*scale;
            double baseRow=slabBottom-Math.Max(sizes[di].Height,sizes[bi].Height)-gap-clearance;
            double rowStep=Math.Max(sizes[di].Height,sizes[bi].Height)+2*gap+clearance;
            double bottomY=bottomLabel.Leaders[0].A.Y;
            // The real bottom straight span includes its independently specified support entry.
            double bottomEnd=result.BottomEndLocal;
            double bottomStart=result.BottomStartLocal;
            var candidates=new HashSet<int>();
            foreach(int d in new[]{-1,1})
            {
                double target=length*.25-d*(gap+sizes[di].Width/2);
                foreach(int i in Enumerable.Range(0,result.Dots.Upper.Count).OrderBy(i=>Math.Abs((result.Dots.Upper[i].X-frame.SupportX)*sign*scale-target)).Take(2)) candidates.Add(i);
            }
            double best=double.PositiveInfinity;UpstandLabelLayout bestDot=null,bestBottom=null;
            foreach(int pair in candidates)foreach(int dd in new[]{-1,1})foreach(int bd in new[]{-1,1})
            {
                var dot=result.Dots.Upper[pair];var dc=new AnnotationPoint(dot.X*scale,dot.Y*scale);
                double desired=length*.75-bd*(gap+sizes[bi].Width/2);
                foreach(double bx in new[]{Math.Max(bottomStart,Math.Min(bottomEnd,desired)),bottomEnd,Math.Max(bottomStart,0),bottomEnd*.75})
                foreach(int stagger in new[]{0,1,2})
                {
                    double dr=baseRow-(stagger==2?rowStep:0),br=baseRow-(stagger==1?rowStep:0);
                    var dl=new UpstandLabelLayout();var bl=new UpstandLabelLayout();
                    Place(dl,dc,dr,dd*sign>0,gap,sizes[di].Width);
                    Place(bl,new AnnotationPoint(support+sign*bx,bottomY),br,bd*sign>0,gap,sizes[bi].Width);
                    if(Conflict(dl,sizes[di],bl,sizes[bi],clearance))continue;
                    double dotCentre=(dc.X-support)*sign+dd*(gap+sizes[di].Width/2);
                    double bottomCentre=bx+bd*(gap+sizes[bi].Width/2);
                    double score=Math.Abs(dotCentre-length*.25)+Math.Abs(bottomCentre-length*.75)+
                        (stagger==0?0:rowStep*.1);
                    if(score>=best)continue;
                    best=score;bestDot=dl;bestBottom=bl;
                }
            }
            if(bestDot==null)throw new InvalidOperationException("当前字高和板长无法排布清晰的下方规格，请人工调整标注。");
            CopyPlacement(dotLabel,bestDot);CopyPlacement(bottomLabel,bestBottom);
            // The slash follows the selected real bottom contact, not its old end.
            MoveSlash(bottomLabel,bottomLabel.Leaders[0].A.X);
            AlignUpperDimensions(result,sizes);
        }
        // Route specifications first, then raise BOTH independent dimensions.
        // Never feed this final row back into compact specification routing.
        private static void AlignUpperDimensions(CantileverAnnotationResult result,IList<CantileverTextSize> sizes)
        {
            if(result.Lin==null)return;
            double clearance=Math.Max(50*result.Scale,1.5*result.Dimensions.TextHeight);
            double y=Math.Max(result.Dimensions.WidthLine.Y,result.LinLine.Y);
            for(int i=0;i<result.Labels.Count;i++)
            {
                var label=result.Labels[i];
                if(label.Name=="底筋"||label.Name=="上下两排点筋")continue;
                double top=Math.Max(label.Layout.TextAnchor.Y+sizes[i].Height,
                    label.Layout.Leaders.Max(s=>Math.Max(s.A.Y,s.B.Y)));
                y=Math.Max(y,top+clearance);
            }
            result.Dimensions.WidthLine=new AnnotationPoint(result.Dimensions.WidthLine.X,y);
            result.LinLine=new AnnotationPoint(result.LinLine.X,y);
        }
        private static void MoveSlash(UpstandLabelLayout label,double x)
        {
            if(!label.HasSlash)return;
            double dx=x-label.SlashCenter.X;
            label.SlashCenter=new AnnotationPoint(x,label.SlashCenter.Y);
            label.SlashA=new AnnotationPoint(label.SlashA.X+dx,label.SlashA.Y);
            label.SlashB=new AnnotationPoint(label.SlashB.X+dx,label.SlashB.Y);
        }
        private static void CopyPlacement(UpstandLabelLayout target,UpstandLabelLayout source)
        { target.Rightward=source.Rightward;target.TextAnchor=source.TextAnchor;target.Leaders.Clear();target.Leaders.AddRange(source.Leaders); }
        private static void Place(UpstandLabelLayout label,AnnotationPoint contact,double row,bool right,double gap,double width)
        {
            double sign=right?1:-1;label.Rightward=right;
            label.TextAnchor=new AnnotationPoint(contact.X+sign*gap,row+gap);
            label.Leaders.Clear();var corner=new AnnotationPoint(contact.X,row);
            label.Leaders.Add(new AnnotationSegment(contact,corner));
            label.Leaders.Add(new AnnotationSegment(corner,new AnnotationPoint(contact.X+sign*(gap+width),row)));
        }
        private static bool Conflict(UpstandLabelLayout a,CantileverTextSize sa,UpstandLabelLayout b,CantileverTextSize sb,double gap)
        {
            double al=a.Rightward?a.TextAnchor.X:a.TextAnchor.X-sa.Width,ar=al+sa.Width;
            double bl=b.Rightward?b.TextAnchor.X:b.TextAnchor.X-sb.Width,br=bl+sb.Width;
            if(al<br+gap&&ar+gap>bl&&a.TextAnchor.Y<b.TextAnchor.Y+sb.Height+gap&&a.TextAnchor.Y+sa.Height+gap>b.TextAnchor.Y)return true;
            foreach(var s in a.Leaders)foreach(var t in b.Leaders)if(Intersects(s,t))return true;
            foreach(var s in a.Leaders)if(HitsBox(s,bl-gap,br+gap,b.TextAnchor.Y-gap,b.TextAnchor.Y+sb.Height+gap))return true;
            foreach(var s in b.Leaders)if(HitsBox(s,al-gap,ar+gap,a.TextAnchor.Y-gap,a.TextAnchor.Y+sa.Height+gap))return true;
            return false;
        }
        private static bool HitsBox(AnnotationSegment s,double left,double right,double bottom,double top)
        { return Math.Max(s.A.X,s.B.X)>left&&Math.Min(s.A.X,s.B.X)<right&&Math.Max(s.A.Y,s.B.Y)>bottom&&Math.Min(s.A.Y,s.B.Y)<top; }
        private static bool Intersects(AnnotationSegment a,AnnotationSegment b)
        {
            const double e=1e-9;bool av=Math.Abs(a.A.X-a.B.X)<e,bv=Math.Abs(b.A.X-b.B.X)<e;
            if(av==bv)
            {
                if(av&&Math.Abs(a.A.X-b.A.X)<e)return Math.Min(Math.Max(a.A.Y,a.B.Y),Math.Max(b.A.Y,b.B.Y))-Math.Max(Math.Min(a.A.Y,a.B.Y),Math.Min(b.A.Y,b.B.Y))>e;
                if(!av&&Math.Abs(a.A.Y-b.A.Y)<e)return Math.Min(Math.Max(a.A.X,a.B.X),Math.Max(b.A.X,b.B.X))-Math.Max(Math.Min(a.A.X,a.B.X),Math.Min(b.A.X,b.B.X))>e;
                return false;
            }
            var v=av?a:b;var h=av?b:a;
            return v.A.X>=Math.Min(h.A.X,h.B.X)-e&&v.A.X<=Math.Max(h.A.X,h.B.X)+e&&h.A.Y>=Math.Min(v.A.Y,v.B.Y)-e&&h.A.Y<=Math.Max(v.A.Y,v.B.Y)+e;
        }
        private static AnnotationPoint Point(CantileverPoint p,double scale) { return new AnnotationPoint(p.X*scale,p.Y*scale); }
        internal static string Spec(double diameter,double spacing)
        { return "φ"+diameter.ToString("0.###",CultureInfo.InvariantCulture)+"@"+spacing.ToString("0.###",CultureInfo.InvariantCulture); }
        private static UpstandLabelLayout Label(string text,CantileverPoint touch,double x,double rowY,bool right,double height,double scale)
        {
            var label=new UpstandLabelLayout { Text=text,Rightward=right,TextAnchor=new AnnotationPoint(x*scale,(rowY+Math.Max(4,.08*height))*scale) };
            var elbow=new AnnotationPoint(touch.X*scale,rowY*scale);
            label.Leaders.Add(new AnnotationSegment(Point(touch,scale),elbow));
            label.Leaders.Add(new AnnotationSegment(elbow,new AnnotationPoint((x+(right?1:-1)*height)*scale,rowY*scale)));
            return label;
        }
    }
}
