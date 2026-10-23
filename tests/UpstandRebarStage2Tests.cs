using System;
using System.Collections.Generic;
using WallDetailTool;

internal static class UpstandRebarStage2Tests
{
    private static void Require(bool ok,string message) { if (!ok) throw new Exception(message); }
    private static void Near(double actual,double expected,string message)
    { Require(Math.Abs(actual-expected)<1e-7,message+": "+actual+" expected "+expected); }
    private static UpstandGeometry G(UpstandInputKind kind)
    { return new UpstandGeometry { Left=0,Right=200,BeamTop=0,Top=300,Elevation=0,InputKind=kind }; }
    private static UpstandRebarParameters P()
    { return new UpstandRebarParameters { LeftAnchor=450,RightAnchor=480 }; }
    private static void Reject(string name,UpstandGeometry g,UpstandRebarParameters p)
    {
        try { UpstandRebarGeometry.Calculate(g,p); }
        catch (InvalidOperationException) { return; }
        throw new Exception(name+" accepted");
    }
    private static void Main()
    {
        UpstandRebarLayout basis=null;
        foreach (UpstandInputKind kind in Enum.GetValues(typeof(UpstandInputKind)))
        {
            var p=P(); var q=UpstandRebarGeometry.Calculate(G(kind),p);
            Require(q.Line.Count==8,"one continuous line geometry");
            Near(q.Line[0].X,31,"cover plus half display width");
            Near(q.Line[0].Y,-450,"left anchor without beam height check");
            Near(q.Line[7].Y,-480,"right anchor");
            Near(q.Line[2].X,31,"left arc tangent x");
            Near(q.Line[2].Y,249,"left arc tangent y");
            Near(q.Line[3].X,51,"left arc radius");
            Near(q.Line[3].Y,269,"top line level");
            Near(q.Line[4].X,149,"right arc radius");
            Near(q.Line[5].X,169,"right arc tangent x");
            Near(q.Line[2].Bulge,-Math.Tan(Math.PI/8),"left quarter arc bulge");
            Near(q.Line[4].Bulge,-Math.Tan(Math.PI/8),"right quarter arc bulge");
            Require(q.LeftDots.Count==q.RightDots.Count && q.LeftDots.Count==2,"dot count");
            Near(q.LeftDots[0].X,43,"left dot side tangency");
            Near(q.RightDots[0].X,157,"right dot side tangency");
            Near(q.LeftDots[0].Y,257,"top dot tangency");
            Near(q.LeftDots[1].Y,57,"fixed 200 spacing, bottom remainder");
            basis=q;
        }
        var changed=P(); changed.Cover=10; changed.LineDisplayWidth=8; changed.DotDisplayDiameter=10;
        changed.Radius=30; changed.LineDiameter=12; changed.DotDiameter=16;
        var c=UpstandRebarGeometry.Calculate(G(UpstandInputKind.OpenInvertedU),changed);
        Near(c.Line[0].X,14,"display width controls offset");
        Near(c.Line[3].X,44,"custom radius");
        Near(c.LeftDots[0].X,23,"display dimensions control side contact");
        Require(c.LeftDots.Count>=1,"dots remain");
        var reversed=new List<ContourSegment> {
            new ContourSegment(new ContourPoint(200,0),new ContourPoint(200,300)),
            new ContourSegment(new ContourPoint(200,300),new ContourPoint(0,300)),
            new ContourSegment(new ContourPoint(0,300),new ContourPoint(0,0)) };
        reversed.Reverse();
        var fromSelection=UpstandRecognizer.Recognize(reversed,0)[0];
        var reversedLayout=UpstandRebarGeometry.Calculate(fromSelection,P());
        Near(reversedLayout.Line[0].X,basis.Line[0].X,"reverse selection left boundary");
        Near(reversedLayout.Line[7].X,basis.Line[7].X,"reverse selection right boundary");
        var invalid=P(); invalid.Radius=100; Reject("too wide for R",G(UpstandInputKind.FullSection),invalid);
        invalid=P(); invalid.Radius=280; Reject("too low for R",G(UpstandInputKind.FullSection),invalid);
        invalid=P(); invalid.LeftAnchor=0; Reject("left anchor",G(UpstandInputKind.ClosedRectangle),invalid);
        invalid=P(); invalid.RightAnchor=-1; Reject("right anchor",G(UpstandInputKind.ClosedRectangle),invalid);
        invalid=P(); invalid.DotSpacing=0; Reject("spacing zero",G(UpstandInputKind.ClosedRectangle),invalid);
        invalid=P(); invalid.DotSpacing=6; Reject("overlapping dots",G(UpstandInputKind.ClosedRectangle),invalid);
        invalid=P(); invalid.Cover=-1; Reject("negative cover",G(UpstandInputKind.ClosedRectangle),invalid);
        invalid=P(); invalid.Radius=0; Reject("zero radius",G(UpstandInputKind.ClosedRectangle),invalid);
        var narrow=G(UpstandInputKind.OpenInvertedU); narrow.Right=65; Reject("narrow",narrow,P());
        var shortOne=G(UpstandInputKind.OpenInvertedU); shortOne.Top=40; Reject("short",shortOne,P());
        Require(basis.LeftDots.Count==2,"source layout unchanged");
        var width300=G(UpstandInputKind.ClosedRectangle); width300.Right=300;
        var three=UpstandRebarGeometry.Calculate(width300,P());
        Require(three.MiddleDots.Count==three.LeftDots.Count,"300 mm width has three dots per row");
        Near(three.MiddleDots[0].X,150,"middle dot centred");
        var width500=G(UpstandInputKind.ClosedRectangle); width500.Right=500;
        var four=UpstandRebarGeometry.Calculate(width500,P());
        Require(four.MiddleDots.Count==2*four.LeftDots.Count,"500 mm width has four dots per row");
        Near(four.MiddleDots[0].X+four.MiddleDots[1].X,500,"four dot symmetry");
        var width700=G(UpstandInputKind.ClosedRectangle); width700.Right=700;
        var five=UpstandRebarGeometry.Calculate(width700,P());
        Require(five.MiddleDots.Count==3*five.LeftDots.Count,"700 mm width has five dots per row");
        var display=P(); display.DotDisplayDiameter=20;
        var displayLayout=UpstandRebarGeometry.Calculate(width300,display);
        Require(displayLayout.MiddleDots.Count==displayLayout.LeftDots.Count,
            "display diameter changes outer centre but not actual diameter rule");
        var boundary=P(); boundary.DotSpacing=114;
        Require(UpstandRebarGeometry.Calculate(G(UpstandInputKind.ClosedRectangle),boundary).MiddleDots.Count==0,
            "exact 114 centre distance allows two dots");
        boundary.DotSpacing=113.999;
        var justOver=UpstandRebarGeometry.Calculate(G(UpstandInputKind.ClosedRectangle),boundary);
        Require(justOver.MiddleDots.Count==justOver.LeftDots.Count,
            "slightly above spacing limit adds one middle per row");
        Console.WriteLine("UPSTAND_STAGE2_GEOMETRY_PASS");
    }
}
