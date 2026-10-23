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
            Require(q.LeftDots.Count==q.RightDots.Count && q.LeftDots.Count==1,"side dot count");
            Near(q.LeftDots[0].X,31,"left dot on upright centreline");
            Near(q.RightDots[0].X,169,"right dot on upright centreline");
            Near(q.LeftDots[0].Y,69,"side dot 200 below top centreline");
            Require(q.TopDots.Count==1,"one top dot");
            Near(q.TopDots[0].X,100,"top dot on horizontal centreline");
            Near(q.TopDots[0].Y,269,"top dot height");
            foreach (ContourPoint dot in q.TopDots)
                Require(dot.Y==q.Line[3].Y && dot.X>=q.Line[3].X && dot.X<=q.Line[4].X,
                    "top dots stay on the horizontal line");
            foreach (ContourPoint dot in q.LeftDots)
                Near(dot.X,q.Line[2].X,"no interior left dot");
            foreach (ContourPoint dot in q.RightDots)
                Near(dot.X,q.Line[5].X,"no interior right dot");
            basis=q;
        }
        var changed=P(); changed.Cover=10; changed.LineDisplayWidth=8; changed.DotDisplayDiameter=10;
        changed.Radius=30; changed.LineDiameter=12; changed.DotDiameter=16;
        var c=UpstandRebarGeometry.Calculate(G(UpstandInputKind.OpenInvertedU),changed);
        Near(c.Line[0].X,14,"display width controls offset");
        Near(c.Line[3].X,44,"custom radius");
        Near(c.LeftDots[0].X,14,"side dot follows line centreline");
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
        invalid=P(); invalid.DotSpacing=12; Reject("top dots cannot fit without overlap",G(UpstandInputKind.ClosedRectangle),invalid);
        invalid=P(); invalid.Cover=-1; Reject("negative cover",G(UpstandInputKind.ClosedRectangle),invalid);
        invalid=P(); invalid.Radius=0; Reject("zero radius",G(UpstandInputKind.ClosedRectangle),invalid);
        var narrow=G(UpstandInputKind.OpenInvertedU); narrow.Right=65; Reject("narrow",narrow,P());
        narrow.Right=105; Reject("top dot has no room",narrow,P());
        var shortOne=G(UpstandInputKind.OpenInvertedU); shortOne.Top=40; Reject("short",shortOne,P());
        Require(basis.LeftDots.Count==1,"source layout unchanged");
        var width300=G(UpstandInputKind.ClosedRectangle); width300.Right=300;
        var three=UpstandRebarGeometry.Calculate(width300,P());
        Require(three.TopDots.Count==1,"300 mm width has one dot on top line");
        Near(three.TopDots[0].X,150,"top dot centred");
        var width500=G(UpstandInputKind.ClosedRectangle); width500.Right=500;
        var four=UpstandRebarGeometry.Calculate(width500,P());
        Require(four.TopDots.Count==2,"500 mm width has two dots on top line");
        Near(four.TopDots[0].X+four.TopDots[1].X,500,"top dot symmetry");
        var width700=G(UpstandInputKind.ClosedRectangle); width700.Right=700;
        var five=UpstandRebarGeometry.Calculate(width700,P());
        Require(five.TopDots.Count==3,"700 mm width has three dots on top line");
        var display=P(); display.DotDisplayDiameter=20;
        var displayLayout=UpstandRebarGeometry.Calculate(width300,display);
        Require(displayLayout.TopDots.Count==1 && displayLayout.LeftDots[0].X==31,
            "display diameter does not move line-centred dots");
        var boundary=P(); boundary.DotSpacing=198;
        Require(UpstandRebarGeometry.Calculate(width300,boundary).TopDots.Count==1,
            "exact 198 top straight length uses one top dot");
        boundary.DotSpacing=197.999;
        var justOver=UpstandRebarGeometry.Calculate(width300,boundary);
        Require(justOver.TopDots.Count==2,
            "slightly above top spacing limit adds a second top dot");
        var tall=G(UpstandInputKind.ClosedRectangle); tall.Top=500;
        var tallLayout=UpstandRebarGeometry.Calculate(tall,P());
        Require(tallLayout.LeftDots.Count==2 && tallLayout.RightDots.Count==2,
            "500 mm height has two dots on each upright");
        Near(tallLayout.LeftDots[0].Y-tallLayout.LeftDots[1].Y,200,
            "vertical centre spacing");
        Console.WriteLine("UPSTAND_STAGE2_GEOMETRY_PASS");
    }
}
