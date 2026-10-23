using System;
using WallDetailTool;

internal static class SlabUpstandRebarTests
{
    static void Ok(bool pass,string name) { if(!pass) throw new Exception(name); }
    static void Near(double a,double b,string name) { Ok(Math.Abs(a-b)<1e-7,name); }
    static SlabUpstandGeometry G(bool full=false)
    { return new SlabUpstandGeometry { Left=400,Right=600,Top=500,SlabTop=300,SlabBottom=0,
        InputKind=full?UpstandInputKind.FullSection:UpstandInputKind.ClosedRectangle,
        SlabLeft=full?(double?)0:null,SlabRight=full?(double?)1000:null }; }
    static void Fail(string name,SlabUpstandGeometry g,SlabUpstandRebarParameters p)
    { try { SlabUpstandRebarGeometry.Calculate(g,p); } catch(InvalidOperationException) { return; } throw new Exception(name+" accepted"); }
    static void Main()
    {
        var p=new SlabUpstandRebarParameters();
        var q=SlabUpstandRebarGeometry.Calculate(G(),p);
        Ok(q.Line.Count==10 && q.LeftDots.Count==1 && q.RightDots.Count==1,"one continuous path and first dots");
        Near(p.EffectiveH,31,"default H"); Near(q.Line[0].Y,31,"horizontal height");
        Near(q.Line[0].X,111,"left 300 from tangent"); Near(q.Line[9].X,889,"right 300 from tangent");
        Near(q.Line[2].X,431,"left vertical"); Near(q.Line[7].X,569,"right vertical");
        Near(q.Line[1].Bulge,Math.Tan(Math.PI/8),"left bottom tangent arc");
        Near(q.Line[3].Bulge,-Math.Tan(Math.PI/8),"left top tangent arc");
        Near(q.Line[5].Bulge,-Math.Tan(Math.PI/8),"right top tangent arc");
        Near(q.Line[7].Bulge,Math.Tan(Math.PI/8),"right bottom tangent arc");
        Near(q.LeftDots[0].X,431,"left dot on upright"); Near(q.RightDots[0].X,569,"right dot on upright");
        Near(q.TopDots[0].X,500,"top dot on horizontal line");
        Near(q.TopDots[0].Y,q.Line[4].Y,"top dot height follows line");
        Near(q.LeftDots[0].X,q.Line[2].X,"no interior left dot");
        Near(q.RightDots[0].X,q.Line[7].X,"no interior right dot");
        var changedSettings=new SlabUpstandRebarParameters { Cover=35,Radius=30 };
        var changedLayout=SlabUpstandRebarGeometry.Calculate(G(),changedSettings);
        Near(changedSettings.EffectiveH,41,"saved cover changes default H");
        Near(changedLayout.Line[2].X,441,"saved cover changes vertical placement");
        Near(changedLayout.Line[1].X,411,"saved R changes lower tangent");
        Near(changedLayout.Line[0].Y,41,"saved cover changes horizontal level");
        var restoredLayout=SlabUpstandRebarGeometry.Calculate(G(),new SlabUpstandRebarParameters());
        Near(restoredLayout.Line[2].X,q.Line[2].X,"restored cover geometry");
        Near(restoredLayout.Line[1].X,q.Line[1].X,"restored R geometry");
        var open=G(); open.InputKind=UpstandInputKind.OpenInvertedU;
        Ok(SlabUpstandRebarGeometry.Calculate(open,p).Line.Count==10,"open U");
        var custom=new SlabUpstandRebarParameters { H=40,LeftLength=250,RightLength=420,Radius=30 };
        var adjusted=SlabUpstandRebarGeometry.Calculate(G(),custom);
        Near(adjusted.Line[0].X,151,"custom left"); Near(adjusted.Line[9].X,1019,"custom right");
        Near(adjusted.Line[0].Y,40,"custom H");
        var thicker=G(); thicker.SlabBottom=-100;
        Near(SlabUpstandRebarGeometry.Calculate(thicker,custom).Line[0].Y,-60,"thickness changes position not H");
        var bounded=SlabUpstandRebarGeometry.Calculate(G(true),custom);
        Near(bounded.RightOverrun,19,"right overrun"); Near(bounded.LeftOverrun,0,"left in bounds");
        Ok(!SlabUpstandRebarGeometry.MayGenerate(bounded,false) && SlabUpstandRebarGeometry.MayGenerate(bounded,true),"continue cancel boundary");
        var both=G(true); both.SlabLeft=160; both.SlabRight=840;
        var bothLayout=SlabUpstandRebarGeometry.Calculate(both,p);
        Near(bothLayout.LeftOverrun,49,"left overrun"); Near(bothLayout.RightOverrun,49,"right overrun");
        Ok(!q.HasSlabBoundaries,"local has no boundary result");
        var wide=G(); wide.Right=700;
        var threeDots=SlabUpstandRebarGeometry.Calculate(wide,p);
        Ok(threeDots.TopDots.Count==1,"300 mm slab upstand has one top dot");
        Near(threeDots.TopDots[0].X,550,"slab top symmetry");
        wide.Right=900;
        var fourDots=SlabUpstandRebarGeometry.Calculate(wide,p);
        Ok(fourDots.TopDots.Count==2,"500 mm slab upstand has two top dots");
        Ok(SlabUpstandRebarGeometry.MayGenerate(q,false),"local mode generates without boundary prompt");
        var thin=G(); thin.SlabBottom=290; Fail("thin slab",thin,p);
        var invalid=new SlabUpstandRebarParameters { H=0 }; Fail("zero H",G(),invalid);
        invalid.H=300; Fail("H equals h",G(),invalid);
        invalid=new SlabUpstandRebarParameters { LeftLength=0 }; Fail("zero left",G(),invalid);
        invalid=new SlabUpstandRebarParameters { RightLength=-1 }; Fail("negative right",G(),invalid);
        invalid=new SlabUpstandRebarParameters { Radius=100 }; Fail("impossible R",G(),invalid);
        invalid=new SlabUpstandRebarParameters { Radius=0 }; Fail("zero R",G(),invalid);
        invalid=new SlabUpstandRebarParameters { Cover=-1 }; Fail("invalid saved cover",G(),invalid);
        invalid=new SlabUpstandRebarParameters { Radius=1000 }; Fail("invalid saved radius for space",G(),invalid);
        var narrow=G(); narrow.Right=450; Fail("self crossing top arcs",narrow,p);
        narrow.Right=505; Fail("top dot has no room",narrow,p);
        var shortRise=G(); shortRise.Top=360;
        Fail("self crossing vertical arcs",shortRise,new SlabUpstandRebarParameters { H=290 });
        invalid=new SlabUpstandRebarParameters { DotSpacing=5 }; Fail("overlapping dots",G(),invalid);
        invalid=new SlabUpstandRebarParameters { DotSpacing=12 }; Fail("overlapping top dots",G(),invalid);
        Console.WriteLine("SLAB_UPSTAND_STAGE2_REBAR_PASS");
    }
}
