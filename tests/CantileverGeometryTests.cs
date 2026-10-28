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
            var dotLayout=CantileverDots.Calculate(f,layout,inputs,12,200);
            Check(dotLayout.Upper.Count==dotLayout.Lower.Count);Check(dotLayout.Upper.Count==8);
            double rho=20-(12+12)/2.0;
            Near(dotLayout.Upper[0].X,1234+sign*(1500-31-20+rho/Math.Sqrt(2)));
            Near(dotLayout.Upper[0].Y,567-31-20+rho/Math.Sqrt(2));
            for(int index=0;index<dotLayout.Upper.Count;index++)
            {
                var u=dotLayout.Upper[index];var l=dotLayout.Lower[index];
                Near(u.X,l.X);Near(l.Y,567-200+25+8/2.0+12);
                Check(sign*(u.X-1234)-6>=0);Check(u.Y-l.Y>=12);
                if(index>0)
                {
                    var previous=dotLayout.Upper[index-1];double dx=previous.X-u.X,dy=previous.Y-u.Y;
                    Near(Math.Sqrt(dx*dx+dy*dy),200);Check(sign*dx>0);
                    if(index>1) Near(Math.Abs(dx),200);
                }
            }
            double clearance=sign*(dotLayout.Upper[dotLayout.Upper.Count-1].X-1234)-6;
            Check(clearance<=200);Check(clearance+200>200);
            Check(layout.Bars.Count==groups.Groups.Count);
            var top=layout.Bars[0].Vertices;var bot=layout.Bars[1].Vertices;
            Check(bot.Count==2);Check(!ReferenceEquals(top,bot));
            Near(bot[0].Point.X,1234-sign*200);
            Near(top[top.Count-1].Point.Y,bot[0].Point.Y+10);
            Near(layout.EffectiveF,130);
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
        var input=new CantileverPathInputs(1500,200,25,20,12,180,250,0,100,200,0);
        var normal=CantileverRebarGeometry.Calculate(frame,simple,input);
        Reject(()=>CantileverDots.Calculate(frame,normal,input,50,200));
        Reject(()=>CantileverDots.Calculate(frame,normal,input,12,10));
        Reject(()=>CantileverDots.Calculate(frame,normal,input,12,double.MaxValue));
        // Shared X makes a too-short lower step overlap, even if the upper spacing is valid.
        Reject(()=>CantileverDots.Calculate(frame,normal,input,12,12));
        var narrow=new CantileverPathInputs(150,200,25,20,12,180,250,0,100,200,0);
        var onlyCorner=CantileverDots.Calculate(frame,CantileverRebarGeometry.Calculate(frame,simple,narrow),narrow,12,200);
        Check(onlyCorner.Upper.Count==1&&onlyCorner.Lower.Count==1);
        foreach(var direction in new[]{CantileverDirection.Left,CantileverDirection.Right})
        foreach(double length in new[]{60.0,150,300,600,1500})
        foreach(double spacing in new[]{25.0,100,200,1000})
        foreach(double display in new[]{8.0,12})
        {
            var f=new CantileverFrame(direction,-1234,2345,300);
            var p=new CantileverPathInputs(length,300,25,20,12,180,250,0,100,200,0);
            var line=CantileverRebarGeometry.Calculate(f,simple,p);
            var dots=CantileverDots.Calculate(f,line,p,display,spacing);
            Check(dots.Upper.Count==dots.Lower.Count);
            var last=dots.Upper[dots.Upper.Count-1];double remaining=(int)direction*(last.X-f.SupportX)-display/2;
            Check(remaining<=spacing+1e-7);Check(remaining>=-1e-7);
            if(dots.Upper.Count>1)
            {
                var previous=dots.Upper[dots.Upper.Count-2];
                Check((int)direction*(previous.X-f.SupportX)-display/2>spacing-1e-7);
            }
            for(int i=0;i<dots.Upper.Count;i++)
            {
                Near(dots.Upper[i].X,dots.Lower[i].X);
                if(i>0)
                {
                    var a=dots.Upper[i-1];var b=dots.Upper[i];
                    Near(Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)),spacing);
                    Check(Math.Abs(dots.Lower[i].X-dots.Lower[i-1].X)<=spacing+1e-7);
                }
            }
        }
        var largeBottom=new CantileverSpecifications(CantileverSupportMode.NoInnerUp,new CantileverSpecification(CantileverBarRole.Top,12,150),new CantileverSpecification(CantileverBarRole.Bottom,100,200));
        var thick=new CantileverPathInputs(1500,300,25,20,12,180,250,0,100,200,0);
        Reject(()=>CantileverDots.Calculate(frame,CantileverRebarGeometry.Calculate(frame,largeBottom,thick),thick,12,200));
        foreach(var direction in new[]{CantileverDirection.Left,CantileverDirection.Right})
        foreach(CantileverSupportMode mode in Enum.GetValues(typeof(CantileverSupportMode)))
        foreach(double thickness in new[]{150.0,200,300}) foreach(double cover in new[]{20.0,25,40})
        foreach(double topDiameter in new[]{12.0,16,20}) foreach(double bottomDiameter in new[]{8.0,12,16})
        {
            var f=new CantileverFrame(direction,700,900,300);
            var specs=new CantileverSpecifications(mode,new CantileverSpecification((int)mode>=3?CantileverBarRole.InnerExtension:CantileverBarRole.Top,topDiameter,150),
                new CantileverSpecification(CantileverBarRole.Bottom,bottomDiameter,200),mode==CantileverSupportMode.InnerWithAdditional?new CantileverSpecification(CantileverBarRole.Additional,20,100):null);
            var p=new CantileverPathInputs(1500,thickness,cover,20,12,180,250,600,999,200,450);
            var steel=CantileverRebarGeometry.Calculate(f,specs,p);
            var end=steel.Bars[0].Vertices[steel.Bars[0].Vertices.Count-1].Point;
            Near(end.Y-steel.Bars[1].Vertices[0].Point.Y,10);
            Check(steel.EffectiveF>20);
            if(mode==CantileverSupportMode.InnerWithAdditional)Near(steel.Bars[2].Vertices[steel.Bars[2].Vertices.Count-1].Point.Y,end.Y);
            // Retired manual F has no influence, including zero.
            var other=CantileverRebarGeometry.Calculate(f,specs,new CantileverPathInputs(1500,thickness,cover,20,12,180,250,600,0,200,450));
            Near(other.EffectiveF,steel.EffectiveF);
        }
        Reject(()=>CantileverRebarGeometry.Calculate(frame,simple,new CantileverPathInputs(1500,80,25,20,12,180,250,0,0,200,0)));
        Console.WriteLine("CANTILEVER_PREREQUISITES_PASS checks="+checks);
    }
}
