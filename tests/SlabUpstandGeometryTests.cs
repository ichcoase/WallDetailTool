using System;
using System.Collections.Generic;
using WallDetailTool;

internal static class SlabUpstandGeometryTests
{
    private static ContourSegment S(double x,double y,double a,double b)
    { return new ContourSegment(new ContourPoint(x,y),new ContourPoint(a,b)); }
    private static void Check(bool ok,string name) { if (!ok) throw new Exception(name); }
    private static void Near(double a,double b,string name) { Check(Math.Abs(a-b)<1e-8,name); }
    private static void Fails(string name,ContourSegment[] edges,double? h=null)
    {
        try { SlabUpstandRecognizer.Recognize(edges,0,h); }
        catch (InvalidOperationException) { return; }
        throw new Exception(name+" accepted");
    }
    private static ContourSegment[] Full(double bottom=0)
    { return new[] { S(0,bottom,1000,bottom),S(1000,bottom,1000,300),S(1000,300,600,300),
        S(600,300,600,500),S(600,500,400,500),S(400,500,400,300),S(400,300,0,300),S(0,300,0,bottom) }; }
    private static void Main()
    {
        var rect=new[] { S(0,0,200,0),S(200,0,200,300),S(200,300,0,300),S(0,300,0,0) };
        var local=SlabUpstandRecognizer.Recognize(rect,7,180)[0];
        Check(local.InputKind==UpstandInputKind.ClosedRectangle && local.ThicknessIsManual && !local.HasSlabBoundaries,"local kind");
        Near(local.SlabTop,0,"local top"); Near(local.SlabBottom,-180,"local bottom"); Near(local.SlabThickness,180,"local h");
        var open=new[] { S(0,0,0,300),S(0,300,200,300),S(200,300,200,0) };
        Check(SlabUpstandRecognizer.Recognize(open,0,120)[0].InputKind==UpstandInputKind.OpenInvertedU,"open U");
        Fails("missing h",open); Fails("zero h",open,0); Fails("negative h",open,-1);
        var full=SlabUpstandRecognizer.Recognize(Full(),0,null)[0];
        Check(full.InputKind==UpstandInputKind.FullSection && full.HasSlabBoundaries && !full.ThicknessIsManual,"full kind");
        Near(full.Left,400,"upstand left"); Near(full.Right,600,"upstand right"); Near(full.Width,200,"width");
        Near(full.Height,200,"height"); Near(full.SlabTop,300,"slab top"); Near(full.SlabBottom,0,"slab bottom");
        Near(full.SlabThickness,300,"slab thickness"); Near(full.SlabLeft.Value,0,"slab left"); Near(full.SlabRight.Value,1000,"slab right");
        var reversed=new List<ContourSegment>(); foreach(var e in Full()) reversed.Insert(0,new ContourSegment(e.B,e.A));
        Check(SlabUpstandRecognizer.Recognize(reversed,0,null).Count==1,"reverse and mixed flattened segments");
        var near=new[] { S(0,0,0,300),S(0,301,200,300),S(200,300,200,0) };
        Check(SlabUpstandRecognizer.Recognize(near,0,100).Count==1,"gap 1");
        Fails("gap 1.1",new[] { S(0,0,0,300),S(0,301.1,200,300),S(200,300,200,0) },100);
        var multi=new[] { S(0,0,1200,0),S(1200,0,1200,300),S(1200,300,900,300),S(900,300,900,500),
            S(900,500,700,500),S(700,500,700,300),S(700,300,500,300),S(500,300,500,500),
            S(500,500,300,500),S(300,500,300,300),S(300,300,0,300),S(0,300,0,0) };
        var choices=SlabUpstandRecognizer.Recognize(multi,0,null);
        Check(choices.Count==2,"multi count"); Near(SlabUpstandRecognizer.SelectCandidate(choices,400,400).Left,300,"manual left");
        Check(choices[0].CandidateState==SlabCandidateState.AwaitingSelection,"selection state");
        Near(SlabUpstandRecognizer.SelectCandidate(choices,800,400).Left,700,"manual right");
        Fails("invalid width",new[] { S(0,0,0,300),S(0,300,0.5,300),S(0.5,300,0.5,0) },100);
        var stepped=new[] { S(0,0,500,0),S(500,0,500,50),S(500,50,1000,50),S(1000,50,1000,300),
            S(1000,300,600,300),S(600,300,600,500),S(600,500,400,500),S(400,500,400,300),
            S(400,300,0,300),S(0,300,0,0) };
        Fails("stepped bottom",stepped);
        var loweredTop=new[] { S(0,0,1000,0),S(1000,0,1000,300),S(1000,300,600,300),
            S(600,300,600,500),S(600,500,400,500),S(400,500,400,300),S(400,300,200,300),
            S(200,300,200,250),S(200,250,0,250),S(0,250,0,0) };
        Fails("lowered slab top",loweredTop);
        var slope=new List<ContourSegment>(Full()); slope[0]=S(0,0,1000,2); Fails("sloped bottom",slope.ToArray());
        Console.WriteLine("SLAB_UPSTAND_STAGE1_GEOMETRY_PASS");
    }
}
