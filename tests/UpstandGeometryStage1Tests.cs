using System;
using System.Collections.Generic;
using WallDetailTool;

internal static class UpstandGeometryStage1Tests
{
    private static ContourPoint P(double x,double y) { return new ContourPoint(x,y); }
    private static ContourSegment S(double x1,double y1,double x2,double y2) { return new ContourSegment(P(x1,y1),P(x2,y2)); }
    private static void Ok(bool condition,string name) { if (!condition) throw new Exception(name); }
    private static void Fail(string name,params ContourSegment[] edges)
    {
        try { UpstandRecognizer.Recognize(edges,0); }
        catch (InvalidOperationException) { return; }
        throw new Exception(name+" should fail");
    }
    private static UpstandGeometry One(string name,UpstandInputKind kind,params ContourSegment[] edges)
    {
        var result=UpstandRecognizer.Recognize(edges,0);
        Ok(result.Count==1 && result[0].InputKind==kind,name);
        return result[0];
    }
    private static ContourSegment[] Rect()
    { return new[] { S(0,0,200,0),S(200,0,200,300),S(200,300,0,300),S(0,300,0,0) }; }
    private static void Main()
    {
        // The same flattened segments are produced by LINE and LWPOLYLINE adapters.
        var r=One("rectangle",UpstandInputKind.ClosedRectangle,Rect());
        Ok(r.Left==0 && r.Right==200 && r.Top==300 && r.BeamTop==0 && r.Width==200 && r.Height==300,"geometry");
        One("open U",UpstandInputKind.OpenInvertedU,S(0,0,0,300),S(0,300,200,300),S(200,300,200,0));
        One("split mixed reverse order",UpstandInputKind.ClosedRectangle,
            S(200,300,100,300),S(200,0,200,300),S(0,300,0,0),S(0,0,100,0),S(100,300,0,300),S(100,0,200,0));
        One("gap 0.9",UpstandInputKind.OpenInvertedU,S(0,0,0,300),S(0,300.9,200,300),S(200,300,200,0));
        One("gap 1",UpstandInputKind.OpenInvertedU,S(0,0,0,300),S(0,301,200,300),S(200,300,200,0));
        Fail("gap 1.1",S(0,0,0,300),S(0,301.1,200,300),S(200,300,200,0));
        One("U base 1",UpstandInputKind.OpenInvertedU,S(0,0,0,300),S(0,300,200,300),S(200,300,200,1));
        Fail("U base 1.1",S(0,0,0,300),S(0,300,200,300),S(200,300,200,1.1));
        var full=new[] { S(0,0,1000,0),S(1000,0,1000,300),S(1000,300,600,300),S(600,300,600,500),
            S(600,500,400,500),S(400,500,400,300),S(400,300,0,300),S(0,300,0,0) };
        var f=One("full section",UpstandInputKind.FullSection,full);
        Ok(f.Left==400 && f.Right==600 && f.BeamTop==300 && f.Top==500,"full geometry");
        var multi=new[] { S(0,0,1200,0),S(1200,0,1200,300),S(1200,300,900,300),S(900,300,900,500),
            S(900,500,700,500),S(700,500,700,300),S(700,300,500,300),S(500,300,500,500),
            S(500,500,300,500),S(300,500,300,300),S(300,300,0,300),S(0,300,0,0) };
        var candidates=UpstandRecognizer.Recognize(multi,0);
        Ok(candidates.Count==2,"multiple candidates");
        Ok(UpstandRecognizer.SelectCandidate(candidates,400,400).Left==300,"choose left candidate");
        Ok(UpstandRecognizer.SelectCandidate(candidates,800,400).Left==700,"choose right candidate");
        try { UpstandRecognizer.SelectCandidate(candidates,600,400); throw new Exception("outside candidate accepted"); }
        catch (InvalidOperationException) { }
        var extra=new List<ContourSegment>(Rect()); extra.Add(S(300,0,400,0)); Fail("extra",extra.ToArray());
        Fail("missing",S(0,0,200,0),S(200,0,200,300));
        Fail("diagonal",S(0,0,0,300),S(0,300,200,301.1),S(200,301.1,200,0));
        Fail("zero",S(0,0,0,0),S(0,0,0,300),S(0,300,200,300),S(200,300,200,0));
        var overlap=new List<ContourSegment>(Rect()); overlap.Add(S(0,0,100,0)); Fail("overlap",overlap.ToArray());
        Fail("self crossing",S(0,0,200,0),S(200,0,200,300),S(200,300,0,300),S(0,300,0,-100),S(0,-100,100,-100),S(100,-100,100,100));
        Fail("invalid width",S(0,0,0,300),S(0,300,0.5,300),S(0.5,300,0.5,0));
        Console.WriteLine("UPSTAND_STAGE1_GEOMETRY_PASS");
    }
}
