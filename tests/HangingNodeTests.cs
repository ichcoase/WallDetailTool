using System;
using System.Collections.Generic;
using System.Linq;
using WallDetailTool;

internal static class HangingNodeTests
{
    static int checks;
    static void Ok(bool v,string name) { checks++; if(!v) throw new Exception(name); }
    static void Near(double a,double b,string name) { Ok(Math.Abs(a-b)<1e-7,name); }
    static void Reject(Action action,string name)
    { try { action(); } catch(InvalidOperationException) { checks++; return; } throw new Exception(name); }
    static List<ContourSegment> Path(bool closed,params double[] xy)
    {
        var q=new List<ContourSegment>(); int n=xy.Length/2;
        for(int i=0;i<(closed?n:n-1);i++)
            q.Add(new ContourSegment(new ContourPoint(xy[2*i],xy[2*i+1]),
                new ContourPoint(xy[2*((i+1)%n)],xy[2*((i+1)%n)+1])));
        return q;
    }
    static void Compare(List<RebarVertex> up,List<RebarVertex> down)
    {
        Ok(up.Count==down.Count,"vertex count");
        for(int i=0;i<up.Count;i++)
        {
            Near(up[i].X,down[i].X,"x"); Near(up[i].Y,-down[i].Y,"y");
            Near(up[i].Bulge,-down[i].Bulge,"arc handedness");
            if(i+1<up.Count)
            {
                double dx=down[i+1].X-down[i].X,dy=down[i+1].Y-down[i].Y;
                double ux=up[i+1].X-up[i].X,uy=up[i+1].Y-up[i].Y;
                Near(dx*dx+dy*dy,ux*ux+uy*uy,"chord and arc length invariant");
                if(Math.Abs(down[i].Bulge)>0)
                {
                    double r=Math.Sqrt(dx*dx+dy*dy)*(1+down[i].Bulge*down[i].Bulge)/(4*Math.Abs(down[i].Bulge));
                    Near(r,20,"quarter arc radius");
                    Near(Math.Abs(dx),20,"horizontal tangent offset");
                    Near(Math.Abs(dy),20,"vertical tangent offset");
                }
            }
        }
    }
    static void Dots(List<ContourPoint> a,List<ContourPoint> b)
    {
        Ok(a.Count==b.Count,"dot count");
        for(int i=0;i<a.Count;i++) { Near(a[i].X,b[i].X,"dot x"); Near(a[i].Y,-b[i].Y,"dot y"); }
    }
    static void Annotation(UpstandGeometry g,UpstandRebarLayout steel,UpstandRebarParameters p)
    {
        foreach(var view in new[]{new[]{-900.0,350},new[]{-10.0,1000}})
        {
            var a=UpstandAnnotationGeometry.Calculate(g,steel,p,62.5,view[0],view[1]);
            string text=a.DotLabel.Text;
            double width=a.WidthB.X-a.WidthA.X,height=a.HeightB.Y-a.HeightA.Y;
            NodeOrientation.Reflect(a);
            Near(a.WidthB.X-a.WidthA.X,width,"dimension width");
            Near(a.HeightB.Y-a.HeightA.Y,height,"dimension height ascending");
            Ok(a.WidthLine.Y<-g.Top,"width dimension below hanging");
            Ok(a.DotLabel.Text==text,"spec not mirrored");
            Ok(text==(steel.LeftDots.Count==1?"3φ6":"φ6@200"),"row spec");
            foreach(var label in new[]{a.LineLabel,a.DotLabel})
            {
                var underline=label.Leaders[label.Leaders.Count-1];
                Near(label.TextAnchor.Y-underline.B.Y,4,"upright text above underline");
                Near(underline.A.Y,underline.B.Y,"horizontal underline");
            }
            if(steel.LeftDots.Count==1)
            {
                Ok(a.DotLabel.TextAnchor.Y+a.TextHeight<-g.Top,"end-row text stays below concrete");
                Ok(a.DotLabel.Leaders.Count==4,"three dot branches");
            }
        }
    }
    static void Main()
    {
        foreach(double face in new[]{0.0,725.0,-420.0})
        foreach(double rise in new[]{150.0,500.0})
        {
            var outlines=new[]{
                Path(true,0,face,300,face,300,face+rise,0,face+rise),
                Path(false,0,face,0,face+rise,300,face+rise,300,face),
                Path(true,-500,face-200,800,face-200,800,face,300,face,300,face+rise,0,face+rise,0,face,-500,face)};
            foreach(var source in outlines)
            {
                var physical=NodeOrientation.Reflect(source);
                // Deterministic scrambled ordering and alternating reversed edges.
                physical=physical.OrderBy(s=>s.A.X).ThenByDescending(s=>s.B.Y).ToList();
                for(int i=0;i<physical.Count;i+=2) physical[i]=new ContourSegment(physical[i].B,physical[i].A);
                var copy=physical.Select(s=>s.A.Y).ToArray();
                var canonical=NodeOrientation.Reflect(physical);
                var g=UpstandRecognizer.Recognize(canonical,7)[0];
                Near(g.BeamTop,face,"connection"); Near(g.Width,300,"width"); Near(g.Height,rise,"rise");
                Near(UpstandRecognizer.SelectCandidate(new[]{g},150,NodeOrientation.Y(-face-rise/2,true)).Left,0,"selection conversion");
                var p=new UpstandRebarParameters{LeftAnchor=350,RightAnchor=470};
                var up=UpstandRebarGeometry.Calculate(g,p); var down=UpstandRebarGeometry.Calculate(g,p);
                Annotation(g,up,p);
                NodeOrientation.Reflect(down.Line,down.LeftDots,down.RightDots,down.TopDots,down.CornerDots);
                Compare(up.Line,down.Line); Dots(up.LeftDots,down.LeftDots); Dots(up.RightDots,down.RightDots);
                Dots(up.TopDots,down.TopDots); Dots(up.CornerDots,down.CornerDots);
                Near(down.Line[0].Y,-face+350,"left anchor upward"); Near(down.Line[7].Y,-face+470,"right anchor upward");
                Near(down.Line[2].X,31,"cover plus half width"); Near(down.LineWidth,12,"line width");
                Near(down.DotDiameter,12,"dot size");
                var sg=SlabUpstandRecognizer.Recognize(canonical,7,200)[0];
                var sp=new SlabUpstandRebarParameters{H=40,LeftLength=250,RightLength=420};
                var su=SlabUpstandRebarGeometry.Calculate(sg,sp); var sd=SlabUpstandRebarGeometry.Calculate(sg,sp);
                NodeOrientation.Reflect(sd.Line,sd.LeftDots,sd.RightDots,sd.TopDots,sd.CornerDots);
                Compare(su.Line,sd.Line); Dots(su.LeftDots,sd.LeftDots); Dots(su.RightDots,sd.RightDots);
                Dots(su.TopDots,sd.TopDots); Dots(su.CornerDots,sd.CornerDots);
                Near(sd.Line[0].Y,-sg.SlabBottom-40,"H below physical slab top");
                Near(sd.Line[1].X-sd.Line[0].X,250,"left tangent length");
                Near(sd.Line[9].X-sd.Line[8].X,420,"right tangent length");
                sg.SlabBottom-=80;
                var thick=SlabUpstandRebarGeometry.Calculate(sg,sp);
                Near(-thick.Line[0].Y,-sg.SlabBottom-40,"thickness keeps H"); Near(sp.EffectiveH,40,"H unchanged");
                for(int i=0;i<physical.Count;i++) Near(physical[i].A.Y,copy[i],"source unchanged");
                Reject(()=>SlabUpstandRebarGeometry.Calculate(sg,new SlabUpstandRebarParameters{H=sg.SlabThickness}),"H boundary");
                Reject(()=>SlabUpstandRebarGeometry.Calculate(sg,new SlabUpstandRebarParameters{Radius=1000}),"no auto radius reduction");
                Reject(()=>UpstandRebarGeometry.Calculate(g,new UpstandRebarParameters{LeftAnchor=-1,RightAnchor=1}),"negative anchor");
                Reject(()=>UpstandRebarGeometry.Calculate(g,new UpstandRebarParameters{LeftAnchor=1,RightAnchor=1,Radius=12}),"corner dot cannot fit");
            }
        }
        var bad=Path(false,0,0,0,150,300,152,300,0);
        Reject(()=>UpstandRecognizer.Recognize(NodeOrientation.Reflect(bad),0),"sloped physical input");
        var bounded=new SlabUpstandGeometry{Left=0,Right=300,Top=150,SlabTop=0,SlabBottom=-200,SlabLeft=-100,SlabRight=400};
        var b=SlabUpstandRebarGeometry.Calculate(bounded,new SlabUpstandRebarParameters{LeftLength=250,RightLength=420});
        Near(b.LeftOverrun,139,"left overrun"); Near(b.RightOverrun,309,"right overrun");
        Ok(!SlabUpstandRebarGeometry.MayGenerate(b,false)&&SlabUpstandRebarGeometry.MayGenerate(b,true),"continue cancel");
        Console.WriteLine("HANGING_NODE_GEOMETRY_PASS checks="+checks);
    }
}
