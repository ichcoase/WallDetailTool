using System;
using WallDetailTool;

internal static class UpstandAnnotationStage3Tests
{
    private static void Ok(bool value,string name) { if (!value) throw new Exception(name); }
    private static void Near(double value,double expected,string name)
    { Ok(Math.Abs(value-expected)<1e-8,name+" "+value+" != "+expected); }
    private static UpstandGeometry Geometry(UpstandInputKind kind)
    { return new UpstandGeometry { Left=0,Right=200,Top=300,BeamTop=0,Elevation=0,InputKind=kind }; }
    private static UpstandRebarParameters Parameters()
    { return new UpstandRebarParameters { LeftAnchor=450,RightAnchor=450 }; }
    private static UpstandAnnotationLayout Calculate(UpstandGeometry g,UpstandRebarParameters p,double left,double right)
    { return UpstandAnnotationGeometry.Calculate(g,UpstandRebarGeometry.Calculate(g,p),p,62.5,left,right); }
    private static void Main()
    {
        try { Run(); }
        catch (Exception ex) { Console.WriteLine("UPSTAND_STAGE3_ANNOTATION_FAIL: "+ex.Message); Environment.ExitCode=1; }
    }
    private static void Run()
    {
        foreach (UpstandInputKind kind in Enum.GetValues(typeof(UpstandInputKind)))
        {
            var layout=Calculate(Geometry(kind),Parameters(),-400,600);
            Ok(layout.LineLabel.Text=="φ8@150","line specification");
            Ok(layout.DotLabel.Text=="φ6@200","multiple dot rows");
            Ok(layout.LineLabel.Rightward && layout.DotLabel.Rightward,"equal space chooses right");
            Ok(layout.LineLabel.Leaders.Count==1 && layout.DotLabel.Leaders.Count==2,
                "straight line leader and dot leader");
            Near(layout.TextHeight,62.5,"requested text height");
            Ok(layout.LineLabel.TextAnchor.Y<300 && layout.DotLabel.TextAnchor.Y<300,
                "both labels beside the upstand, not above its top");
            Near(layout.LineLabel.Leaders[0].A.X,169,"line leader touches right upright");
            Near(layout.LineLabel.Leaders[0].A.Y,layout.LineLabel.Leaders[0].B.Y,
                "line leader is horizontal");
            Near(layout.LineLabel.Leaders[0].B.X,layout.LineLabel.TextAnchor.X,
                "pure leader stops at text start until CAD measures glyphs");
            Ok(layout.LineLabel.HasSlash && !layout.DotLabel.HasSlash,"slash only on line rebar");
            Near(layout.LineLabel.SlashCenter.X,169,"slash centered on line rebar");
            Near(layout.LineLabel.SlashCenter.Y,layout.LineLabel.Leaders[0].A.Y,
                "slash center on leader contact");
            Near(Math.Sqrt(Math.Pow(layout.LineLabel.SlashB.X-layout.LineLabel.SlashA.X,2)+
                Math.Pow(layout.LineLabel.SlashB.Y-layout.LineLabel.SlashA.Y,2)),36,"slash length");
            Near(layout.LineLabel.SlashWidth,10,"slash width");
            Near(layout.DotLabel.Leaders[0].A.X,43,"dot leader crosses both dots from left");
            Near(layout.DotLabel.Leaders[0].A.Y,57,"dot leader touches bottom dot row");
            Near(layout.DotLabel.Leaders[0].A.Y,layout.DotLabel.Leaders[0].B.Y,"dot horizontal leader");
            Ok(layout.LineLabel.TextAnchor.Y>layout.LineLabel.Leaders[0].A.Y,"text above leader");
            Near(layout.WidthB.X-layout.WidthA.X,200,"concrete width");
            Near(layout.HeightB.Y-layout.HeightA.Y,300,"concrete height");
            Near(layout.HeightLine.X,-93.75,"height dimension opposite right label");
        }
        var leftward=Calculate(Geometry(UpstandInputKind.OpenInvertedU),Parameters(),-900,250);
        Ok(!leftward.LineLabel.Rightward && !leftward.DotLabel.Rightward,"space chooses left");
        Near(leftward.HeightLine.X,-93.75,"height dimension stays where space permits");
        var crowdedLeft=Calculate(Geometry(UpstandInputKind.ClosedRectangle),Parameters(),-10,900);
        Near(crowdedLeft.HeightLine.X,293.75,"height dimension chooses available right side");
        var single=Parameters(); single.DotSpacing=500;
        var one=Calculate(Geometry(UpstandInputKind.ClosedRectangle),single,-400,600);
        Ok(one.DotLabel.Text=="2φ6","single row both sides");
        Ok(one.DotLabel.Leaders.Count==3 && one.DotLabel.KeepLastLeaderEnd,
            "single row has two diagonal branches and one full underline");
        var oneSteel=UpstandRebarGeometry.Calculate(Geometry(UpstandInputKind.ClosedRectangle),single);
        Near(one.DotLabel.Leaders[0].A.X,oneSteel.LeftDots[0].X,"left branch touches left dot");
        Near(one.DotLabel.Leaders[1].A.X,oneSteel.RightDots[0].X,"right branch touches right dot");
        Ok(one.DotLabel.Leaders[0].B.Y>one.DotLabel.Leaders[0].A.Y &&
            one.DotLabel.Leaders[1].B.Y>one.DotLabel.Leaders[1].A.Y,
            "both branches rise from dots");
        Near(one.DotLabel.Leaders[2].A.Y,one.DotLabel.Leaders[2].B.Y,
            "single row underline horizontal");
        Ok(one.DotLabel.TextAnchor.Y>one.DotLabel.Leaders[2].A.Y &&
            one.DotLabel.Leaders[2].A.Y>300,"single row text and underline above concrete");
        var oneMirrored=Calculate(Geometry(UpstandInputKind.OpenInvertedU),single,-20,700);
        Ok(!oneMirrored.DotLabel.Rightward && oneMirrored.DotLabel.Leaders.Count==3,
            "single row mirrors to available right side");
        var custom=Parameters(); custom.LineDiameter=10; custom.LineSpacing=125;
        custom.DotDiameter=8; custom.DotSpacing=150;
        var changed=Calculate(Geometry(UpstandInputKind.FullSection),custom,-400,600);
        Ok(changed.LineLabel.Text=="φ10@125" && changed.DotLabel.Text=="φ8@150","design values in text");
        var fallback=Calculate(Geometry(UpstandInputKind.ClosedRectangle),Parameters(),-20,220);
        Ok(fallback.Warnings.Count>=1,"insufficient view space warns");
        bool rejected=false;
        try { UpstandAnnotationGeometry.Calculate(Geometry(UpstandInputKind.ClosedRectangle),
            UpstandRebarGeometry.Calculate(Geometry(UpstandInputKind.ClosedRectangle),Parameters()),
            Parameters(),0,-400,600); }
        catch (InvalidOperationException) { rejected=true; }
        Ok(rejected,"invalid optional annotation height rejected");
        Console.WriteLine("UPSTAND_STAGE3_ANNOTATION_PASS");
    }
}
