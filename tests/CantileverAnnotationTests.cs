using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using WallDetailTool;

internal static class CantileverAnnotationTests
{
    static int checks;
    static void Check(bool ok,string message) { checks++;if(!ok)throw new Exception(message); }
    static void Near(double a,double b,string message) { Check(Math.Abs(a-b)<1e-7,message+": "+a+" / "+b); }
    static readonly string[] Layers={"WDT-REBAR-LINE","WDT-REBAR-DOT","S-TEXT","s-floor_de_dim"};
    static double[] Geometry(CantileverRebarLayout steel,CantileverDotLayout dots)
    {
        var result=new List<double>{steel.EffectiveF,steel.Bars.Count,dots.Upper.Count,dots.Lower.Count};
        foreach(var bar in steel.Bars) { result.Add(bar.Vertices.Count);foreach(var v in bar.Vertices)result.AddRange(new[]{v.Point.X,v.Point.Y,v.Bulge}); }
        foreach(var dot in dots.Upper.Concat(dots.Lower)) result.AddRange(new[]{dot.X,dot.Y});
        return result.ToArray();
    }
    static AnnotationPoint Mm(AnnotationPoint p,double scale) { return new AnnotationPoint(p.X/scale,p.Y/scale); }
    static bool Contains(AnnotationSegment s,AnnotationPoint p)
    {
        const double e=1e-7;
        return Math.Abs((s.B.X-s.A.X)*(p.Y-s.A.Y)-(s.B.Y-s.A.Y)*(p.X-s.A.X))<e &&
            p.X>=Math.Min(s.A.X,s.B.X)-e&&p.X<=Math.Max(s.A.X,s.B.X)+e&&p.Y>=Math.Min(s.A.Y,s.B.Y)-e&&p.Y<=Math.Max(s.A.Y,s.B.Y)+e;
    }
    static bool Conflict(AnnotationSegment a,AnnotationSegment b)
    {
        const double e=1e-7;bool av=Math.Abs(a.A.X-a.B.X)<e,bv=Math.Abs(b.A.X-b.B.X)<e;
        if(av==bv)
        {
            if(av&&Math.Abs(a.A.X-b.A.X)<e)return Math.Min(Math.Max(a.A.Y,a.B.Y),Math.Max(b.A.Y,b.B.Y))-Math.Max(Math.Min(a.A.Y,a.B.Y),Math.Min(b.A.Y,b.B.Y))>e;
            if(!av&&Math.Abs(a.A.Y-b.A.Y)<e)return Math.Min(Math.Max(a.A.X,a.B.X),Math.Max(b.A.X,b.B.X))-Math.Max(Math.Min(a.A.X,a.B.X),Math.Min(b.A.X,b.B.X))>e;
            return false;
        }
        var v=av?a:b;var h=av?b:a;
        return v.A.X>=Math.Min(h.A.X,h.B.X)-e&&v.A.X<=Math.Max(h.A.X,h.B.X)+e&&h.A.Y>=Math.Min(v.A.Y,v.B.Y)-e&&h.A.Y<=Math.Max(v.A.Y,v.B.Y)+e;
    }
    static void Main()
    {
        try
        {
            foreach(var dir in new[]{CantileverDirection.Left,CantileverDirection.Right})
            foreach(CantileverSupportMode mode in Enum.GetValues(typeof(CantileverSupportMode)))
            foreach(double length in new[]{150.0,300,1500})foreach(double thickness in new[]{100.0,200,400})
            foreach(double height in new[]{20.0,62.5,150})foreach(double scale in new[]{1.0,.001,1/25.4})
                Run(dir,mode,length,thickness,height,scale,600,450,false);
            foreach(var dir in new[]{CantileverDirection.Left,CantileverDirection.Right})
            foreach(double e in new[]{30.0,600,10000})foreach(double lin in new[]{30.0,300,450,12000})
                Run(dir,CantileverSupportMode.InnerWithAdditional,1500,200,62.5,1,e,lin,false);
            foreach(var dir in new[]{CantileverDirection.Left,CantileverDirection.Right})
            foreach(double factor in new[]{.5,2.0,4.0})
                Run(dir,CantileverSupportMode.InnerWithAdditional,1500,200,62.5,1,10000,450,false,factor);
            Run(CantileverDirection.Right,CantileverSupportMode.InnerWithAdditional,1500,200,62.5,1,600,450,true);
            Run(CantileverDirection.Left,CantileverSupportMode.InnerWithAdditional,1500,200,62.5,1,600,450,true);
            Console.WriteLine("CANTILEVER_COMPACT_ANNOTATION_PASS checks="+checks);
        }
        catch(Exception ex) { Console.WriteLine(ex);Environment.ExitCode=1; }
    }
    static void Run(CantileverDirection direction,CantileverSupportMode mode,double length,double thickness,double height,double scale,double e,double lin,bool preview,double widthFactor=1)
    {
        var request=new CantileverDrawRequest(direction,mode,new[]{300.0,180,250,e,0,200,lin,12,150,8,200,16,100,6,200},
            new[]{25.0,12,12,20,height},Layers,scale,4,false);
        var frame=new CantileverFrame(direction,700,800,300);var p=request.Inputs(length,thickness);
        var steel=CantileverRebarGeometry.Calculate(frame,request.Specifications,p);var dots=CantileverDots.Calculate(frame,steel,p,12,200);
        var before=Geometry(steel,dots);var record=CantileverDrawRecord.Encode(request);
        var a=CantileverAnnotationGeometry.Calculate(frame,request,p,steel,dots,37);int sign=(int)direction;
        // Explicit measured-box fixtures, not a character-count estimate.
        var sizes=a.Labels.Select(l=>new CantileverTextSize((l.Name=="内跨伸出筋"?600:l.Name=="面筋"||l.Name=="附加筋"?400:350)*height/62.5*scale*widthFactor,height*scale)).ToArray();
        CantileverAnnotationGeometry.ApplyMeasuredLayout(a,sizes);
        Check(a.Labels.Count==(mode==CantileverSupportMode.InnerWithAdditional?4:3),"no empty labels");
        for(int index=0;index<steel.Bars.Count;index++)
        {
            var bar=steel.Bars[index];var label=a.Labels[index].Layout;var path=bar.Vertices;
            var start=Mm(label.Leaders[0].A,scale);var end=Mm(label.Leaders.Last().B,scale);
            var va=path.Count==2?path[0].Point:path[path.Count-4].Point;var vb=path.Count==2?path[1].Point:path[path.Count-3].Point;
            Check(Contains(new AnnotationSegment(new AnnotationPoint(va.X,va.Y),new AnnotationPoint(vb.X,vb.Y)),start),"anchor on own actual horizontal steel");
            Check(label.HasSlash,"steel slash retained");Near(label.SlashCenter.X,label.Leaders[0].A.X,"slash at actual contact X");Near(label.SlashCenter.Y,label.Leaders[0].A.Y,"slash at actual contact Y");
            bool inner=bar.Specification.Role==CantileverBarRole.InnerExtension;bool bottom=bar.Specification.Role==CantileverBarRole.Bottom;
            if(!bottom)Check(label.Rightward==(inner?sign<0:sign>0),"role-specific upper text direction");
            if(!bottom)Check((inner?-sign:sign)*(end.X-start.X)>0,"correct upper horizontal exit side");
            if(inner)
            {
                double x=(start.X-frame.SupportX)*sign;Check(x< -frame.BeamWidth&&x> -frame.BeamWidth-p.E,"inner extension contact strictly in inner span");
                if(mode==CantileverSupportMode.InnerWithAdditional)Check(Math.Abs(x-frame.CutoffLocalX(p.Lin))>1e-7,"not additional cutoff symbol");
            }
            if(bar.Specification.Role==CantileverBarRole.Additional)Check(sign*(start.X-frame.SupportX)>0,"additional contact on cantilever side");
            if(bottom)Check(end.Y<frame.TopY-p.Thickness,"bottom label below slab");
            else Check(end.Y>frame.TopY,"top labels above slab");
        }
        var dotLabel=a.Labels.Single(l=>l.Name=="上下两排点筋").Layout;
        int pair=Enumerable.Range(0,dots.Upper.Count).Single(i=>Math.Abs(dots.Upper[i].X*scale-dotLabel.Leaders[0].A.X)<1e-7);
        Check(dotLabel.Leaders.Count==2,"one merged vertical trunk and one horizontal line");
        foreach(var dot in new[]{dots.Upper[pair],dots.Lower[pair]})
            Check(Contains(new AnnotationSegment(Mm(dotLabel.Leaders[0].A,scale),Mm(dotLabel.Leaders[0].B,scale)),new AnnotationPoint(dot.X,dot.Y)),"both true dot centres connected");
        Check(dotLabel.Leaders.Last().A.Y/scale<frame.TopY-p.Thickness,"dot text below slab");
        int total=dots.Upper.Count+dots.Lower.Count;
        Check(dotLabel.Text==(total==2?"点筋 2φ6":"点筋 φ6@200"),"actual total count semantics unchanged");
        foreach(var item in a.Labels)
        {
            var label=item.Layout;Check(label.Leaders.Count==2,"exactly one 90-degree corner");
            var first=label.Leaders[0];var last=label.Leaders[1];
            Near(first.A.X,first.B.X,"first segment vertical");Near(last.A.Y,last.B.Y,"last segment horizontal");
            Near(first.B.X,last.A.X,"joint X");Near(first.B.Y,last.A.Y,"joint Y");
            Check(Math.Abs(first.B.Y-first.A.Y)>1e-8&&Math.Abs(last.B.X-last.A.X)>1e-8,"no zero segments");
            Check(label.TextAnchor.Y>last.A.Y,"text above its horizontal line");
            var size=sizes[a.Labels.IndexOf(item)];
            Near(Math.Abs(last.B.X-last.A.X),size.Width+Math.Max(4,.08*height)*scale,"only measured width plus small gap");
            Near(Math.Abs(label.TextAnchor.X-last.A.X),Math.Max(4,.08*height)*scale,"text close to elbow");
            double centre=(label.TextAnchor.X-frame.SupportX*scale)*sign+(label.Rightward?sign:-sign)*size.Width/2;
            if(length>=1500&&height<=62.5&&widthFactor<=2&&item.Name=="底筋")Check(centre>length*scale/2&&centre<length*scale,"bottom text in free half");
            if(length>=1500&&height<=62.5&&widthFactor<=2&&item.Name=="上下两排点筋")Check(centre>0&&centre<length*scale/2,"dot text in support half");
            double left=(label.Rightward?label.TextAnchor.X:label.TextAnchor.X-size.Width)/scale,right=left+size.Width/scale;
            double lower=label.TextAnchor.Y/scale,upper=lower+size.Height/scale;
            foreach(var other in a.Labels)
            {
                if(ReferenceEquals(item,other))continue;
                var os=sizes[a.Labels.IndexOf(other)];var ol=other.Layout;
                double otherLeft=(ol.Rightward?ol.TextAnchor.X:ol.TextAnchor.X-os.Width)/scale;
                Check(!(left<otherLeft+os.Width/scale-1e-7&&right>otherLeft+1e-7&&lower<(ol.TextAnchor.Y+os.Height)/scale-1e-7&&upper>ol.TextAnchor.Y/scale+1e-7),"measured text boxes do not overlap");
                foreach(var s in ol.Leaders)Check(!Hits(new AnnotationSegment(Mm(s.A,scale),Mm(s.B,scale)),left,right,lower,upper),"leaders do not cross another measured text box");
                foreach(var s in label.Leaders)foreach(var t in other.Layout.Leaders)
                    Check(!Conflict(new AnnotationSegment(Mm(s.A,scale),Mm(s.B,scale)),new AnnotationSegment(Mm(t.A,scale),Mm(t.B,scale))),"no label intersections or duplicate collinear lines");
            }
            Check(!Hits(new AnnotationSegment(Mm(a.Dimensions.WidthA,scale),new AnnotationPoint(a.Dimensions.WidthA.X/scale,a.Dimensions.WidthLine.Y/scale)),left,right,lower,upper),"L support extension clears spec text");
            Check(!Hits(new AnnotationSegment(Mm(a.Dimensions.WidthB,scale),new AnnotationPoint(a.Dimensions.WidthB.X/scale,a.Dimensions.WidthLine.Y/scale)),left,right,lower,upper),"L free extension clears spec text");
            if(a.Lin!=null)
            {
                Check(!Hits(new AnnotationSegment(new AnnotationPoint(a.Lin.Cutoff.X,a.Lin.Cutoff.Y),new AnnotationPoint(a.Lin.Cutoff.X,a.LinLine.Y/scale)),left,right,lower,upper),"Lin cutoff extension clears measured spec text");
                Check(!Hits(new AnnotationSegment(new AnnotationPoint(a.Lin.BeamFace.X,a.LinLine.Y/scale),new AnnotationPoint(a.Lin.Cutoff.X,a.LinLine.Y/scale)),left,right,lower,upper),"Lin horizontal row clears spec text");
            }
        }
        Check(before.SequenceEqual(Geometry(steel,dots)),"layout leaves steel/dots/F unchanged");
        Check(record.SequenceEqual(CantileverDrawRecord.Encode(request)),"layout leaves all saved values unchanged");
        // Long inner ranges must not push any label farther from its elbow,
        // or affect the lower layout (whose contacts come only from slab bars).
        if(mode==CantileverSupportMode.InnerWithAdditional&&e>=600)
        {
            var inner=a.Labels.Single(l=>l.Name=="内跨伸出筋").Layout;
            double local=(inner.Leaders[0].A.X/scale-frame.SupportX)*sign;
            Check(local>=-frame.BeamWidth-Math.Max(lin+Math.Max(8,.15*height),Math.Max(90,3*height))-1e-7,"inner anchor bounded by Lin clearance and height, not remote E");
        }
        if(mode==CantileverSupportMode.InnerWithAdditional&&e==10000)
        {
            var values=Enumerable.Range(0,15).Select(i=>request.Value(i)).ToArray();values[3]=600;
            var shorter=new CantileverDrawRequest(direction,mode,values,new[]{25.0,12,12,20,height},Layers,scale,4,false);
            var sp=shorter.Inputs(length,thickness);var ss=CantileverRebarGeometry.Calculate(frame,shorter.Specifications,sp);
            var sd=CantileverDots.Calculate(frame,ss,sp,12,200);
            var sa=CantileverAnnotationGeometry.Calculate(frame,shorter,sp,ss,sd,37);
            CantileverAnnotationGeometry.ApplyMeasuredLayout(sa,sizes);
            foreach(string name in new[]{"底筋","上下两排点筋","附加筋"})
            {
                var first=a.Labels.Single(l=>l.Name==name).Layout;var second=sa.Labels.Single(l=>l.Name==name).Layout;
                Near(first.TextAnchor.X,second.TextAnchor.X,"E does not push slab labels X");
                Near(first.TextAnchor.Y,second.TextAnchor.Y,"E does not push slab label rows");
                Near(first.Leaders[0].A.X,second.Leaders[0].A.X,"E does not move slab label contacts");
            }
        }
        var mirrorFrame=new CantileverFrame((CantileverDirection)(-sign),frame.SupportX,frame.TopY,frame.BeamWidth);
        var mirroredSteel=CantileverRebarGeometry.Calculate(mirrorFrame,request.Specifications,p);
        var mirroredDots=CantileverDots.Calculate(mirrorFrame,mirroredSteel,p,12,200);
        var mirrorRequest=new CantileverDrawRequest((CantileverDirection)(-sign),mode,new[]{300.0,180,250,e,0,200,lin,12,150,8,200,16,100,6,200},new[]{25.0,12,12,20,height},Layers,scale,4,false);
        var mirrored=CantileverAnnotationGeometry.Calculate(mirrorFrame,mirrorRequest,p,mirroredSteel,mirroredDots,37);
        CantileverAnnotationGeometry.ApplyMeasuredLayout(mirrored,sizes);
        for(int i=0;i<a.Labels.Count;i++)
        {
            Near(a.Labels[i].Layout.TextAnchor.X+mirrored.Labels[i].Layout.TextAnchor.X,2*frame.SupportX*scale,"mirrored text position");
            Near(a.Labels[i].Layout.TextAnchor.Y,mirrored.Labels[i].Layout.TextAnchor.Y,"mirrored text row");
        }
        if(preview) SavePreview(direction,steel,dots,a,frame,p);
    }
    static bool Hits(AnnotationSegment s,double left,double right,double bottom,double top)
    { return Math.Max(s.A.X,s.B.X)>left+1e-7&&Math.Min(s.A.X,s.B.X)<right-1e-7&&Math.Max(s.A.Y,s.B.Y)>bottom+1e-7&&Math.Min(s.A.Y,s.B.Y)<top-1e-7; }
    static void SavePreview(CantileverDirection dir,CantileverRebarLayout steel,CantileverDotLayout dots,CantileverAnnotationResult a,CantileverFrame frame,CantileverPathInputs p)
    {
        // Pure-layout preview only: no AutoCAD or approximation of its TEXT extents.
        var lines=new List<string>();Func<double,string> n=v=>v.ToString("0.###",CultureInfo.InvariantCulture);
        Func<double,double> x=v=>800+(v-frame.SupportX)*.3;Func<double,double> y=v=>300-(v-frame.TopY)*.3;
        lines.Add("<svg xmlns='http://www.w3.org/2000/svg' width='1700' height='620'><rect width='1700' height='620' fill='white'/>");
        double l=Math.Min(frame.SupportX,frame.SupportX+(int)dir*p.Length);
        lines.Add("<rect x='"+n(x(l))+"' y='300' width='"+n(p.Length*.3)+"' height='"+n(p.Thickness*.3)+"' fill='#edf1f4' stroke='#8998a5'/>");
        foreach(var b in steel.Bars)
        {
            string pts=String.Join(" ",b.Vertices.Select(v=>n(x(v.Point.X))+","+n(y(v.Point.Y))));
            lines.Add("<polyline points='"+pts+"' fill='none' stroke='#56788a' stroke-width='2'/>");
        }
        foreach(var dot in dots.Upper.Concat(dots.Lower))lines.Add("<circle cx='"+n(x(dot.X))+"' cy='"+n(y(dot.Y))+"' r='2' fill='#999'/>");
        foreach(var item in a.Labels)
        {
            foreach(var s in item.Layout.Leaders)lines.Add("<line x1='"+n(x(s.A.X))+"' y1='"+n(y(s.A.Y))+"' x2='"+n(x(s.B.X))+"' y2='"+n(y(s.B.Y))+"' stroke='#c55132' stroke-width='2'/>");
            var q=item.Layout.TextAnchor;lines.Add("<text x='"+n(x(q.X))+"' y='"+n(y(q.Y)-4)+"' text-anchor='"+(item.Layout.Rightward?"start":"end")+"' font-family='Microsoft YaHei' font-size='16'>"+item.Layout.Text+"</text>");
        }
        lines.Add("<text x='20' y='30' font-size='18'>Pure layout preview: "+dir+"; schematic steel arcs; CAD TEXT extents require user verification</text></svg>");
        File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cantilever-compact-"+dir+".svg"),lines);
    }
}
