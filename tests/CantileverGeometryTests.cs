using System;
using WallDetailTool;
internal static class CantileverGeometryTests
{
    static int checks;
    static void Check(bool ok) { checks++;if(!ok) throw new Exception("Check "+checks+" failed"); }
    static void Near(double a,double b) { Check(Math.Abs(a-b)<1e-8); }
    static void Reject(Action action) { bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected); }
    static void Main()
    {
        foreach(CantileverDirection direction in Enum.GetValues(typeof(CantileverDirection)))
        foreach(CantileverSupportMode mode in Enum.GetValues(typeof(CantileverSupportMode)))
        {
            var f=new CantileverFrame(direction,1234,567,300);int sign=(int)direction;
            var d=f.LinDimension(450,-31);
            Near(d.BeamFace.X,1234-sign*300);Near(d.Cutoff.X,1234-sign*750);
            Near(Math.Abs(d.BeamFace.X-d.Cutoff.X),450);Near(d.Lin,450);
            var tip=f.CutoffSymbolTip(450,-31,10,12);
            Check(tip.Y<d.Cutoff.Y);Near(d.Cutoff.Y-tip.Y,12);Near(Math.Abs(tip.X-d.Cutoff.X),10);
            var fixedTip=f.FixedCutoffSymbolTip(450,-31);
            Near(d.Cutoff.Y-fixedTip.Y,26);Near(Math.Abs(fixedTip.X-d.Cutoff.X),26);
            Near(f.WorldBulge(Math.Tan(Math.PI/8)),sign*Math.Tan(Math.PI/8));
            var wider=new CantileverFrame(direction,1234,567,600).LinDimension(450,-31);
            Near(Math.Abs(wider.Cutoff.X-d.Cutoff.X),300);Near(wider.Lin,d.Lin);
            var groups=new CantileverSpecifications(mode,new CantileverSpecification((int)mode>=3?CantileverBarRole.InnerExtension:CantileverBarRole.Top,12,150),
                new CantileverSpecification(CantileverBarRole.Bottom,8,200),mode==CantileverSupportMode.InnerWithAdditional?new CantileverSpecification(CantileverBarRole.Additional,16,100):null);
            Check(groups.Groups.Count==(mode==CantileverSupportMode.InnerWithAdditional?3:2));
            Near(groups.Groups[0].Diameter,12);Near(groups.Groups[1].Diameter,8);
            if(groups.Groups.Count==3) { Near(groups.Groups[2].Diameter,16);Near(groups.Groups[2].Spacing,100); }
            var inputs=new CantileverPathInputs(1500,200,25,20,12,180,250,600,100,200,450);
            var layout=CantileverRebarGeometry.Calculate(f,groups,inputs);
            Check(layout.Bars.Count==groups.Groups.Count);
            var top=layout.Bars[0].Vertices;var bot=layout.Bars[1].Vertices;
            Check(bot.Count==2);Check(!ReferenceEquals(top,bot));
            Near(bot[0].Point.X,1234-sign*200);
            Near(top[top.Count-1].Point.Y,567-31-100);
            Near(top[top.Count-2].Point.X,1234+sign*(1500-31));
            Check(top[top.Count-1].Point.Y<top[top.Count-2].Point.Y);
            if((int)mode<3)
            {
                Near(top[0].Point.X,1234-sign*180);
                Near(top[0].Point.Y,567-31+(mode==CantileverSupportMode.NoInnerUp?250:-250));
                // Upward support end deliberately outside concrete: no beam-boundary rejection.
                if(mode==CantileverSupportMode.NoInnerUp) Check(top[0].Point.Y>567);
            }
            else Near(top[0].Point.X,1234-sign*(300+600));
            if(mode==CantileverSupportMode.InnerWithAdditional)
            {
                var extra=layout.Bars[2].Vertices;
                Near(extra[0].Point.X,1234-sign*(300+450));
                for(int k=1;k<top.Count;k++)
                { Near(top[k].Point.X,extra[k].Point.X);Near(top[k].Point.Y,extra[k].Point.Y);Near(top[k].Bulge,extra[k].Bulge); }
            }
            // Check circular radius and tangent continuity analytically, not a screenshot.
            for(int k=0;k<top.Count-1;k++) if(top[k].Bulge!=0)
            {
                var a=top[k].Point;var b=top[k+1].Point;double bulge=top[k].Bulge;
                double dx=b.X-a.X,dy=b.Y-a.Y,chord=Math.Sqrt(dx*dx+dy*dy);
                Near(chord*(1+bulge*bulge)/(4*Math.Abs(bulge)),20);
                double factor=(1-bulge*bulge)/(4*bulge);
                double cx=(a.X+b.X)/2-dy*factor,cy=(a.Y+b.Y)/2+dx*factor;
                var before=top[k-1].Point;var after=top[k+2].Point;
                Near((a.X-cx)*(a.X-before.X)+(a.Y-cy)*(a.Y-before.Y),0);
                Near((b.X-cx)*(after.X-b.X)+(b.Y-cy)*(after.Y-b.Y),0);
            }
        }
        foreach(double bad in new[]{0.0,-1,double.NaN,double.PositiveInfinity})
        {
            Reject(()=>new CantileverFrame(CantileverDirection.Right,0,0,bad));
            Reject(()=>new CantileverFrame(CantileverDirection.Left,0,0,300).LinDimension(bad,0));
            Reject(()=>new CantileverSpecification(CantileverBarRole.Top,bad,150));
        }
        Reject(()=>new CantileverFrame((CantileverDirection)0,0,0,300));
        Reject(()=>new CantileverFrame(CantileverDirection.Left,double.MaxValue,0,double.MaxValue));
        Reject(()=>new CantileverFrame(CantileverDirection.Right,0,0,300).CutoffSymbolTip(450,0,0,10));
        Reject(()=>new CantileverSpecifications(CantileverSupportMode.InnerWithAdditional,new CantileverSpecification(CantileverBarRole.InnerExtension,12,150),new CantileverSpecification(CantileverBarRole.Bottom,8,200)));
        var simple=new CantileverSpecifications(CantileverSupportMode.NoInnerDown,new CantileverSpecification(CantileverBarRole.Top,12,150),new CantileverSpecification(CantileverBarRole.Bottom,8,200));
        var frame=new CantileverFrame(CantileverDirection.Right,0,0,300);
        Reject(()=>CantileverRebarGeometry.Calculate(frame,simple,new CantileverPathInputs(1500,200,25,20,12,180,20,0,100,200,0)));
        Reject(()=>CantileverRebarGeometry.Calculate(frame,simple,new CantileverPathInputs(1500,200,25,20,12,0,250,0,100,200,0)));
        Reject(()=>CantileverRebarGeometry.Calculate(frame,simple,new CantileverPathInputs(1500,50,25,20,12,180,250,0,100,200,0)));
        Console.WriteLine("CANTILEVER_PREREQUISITES_PASS checks="+checks);
    }
}
