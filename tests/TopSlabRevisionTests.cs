using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
namespace WallDetailTool
{
    internal static class TopSlabRevisionTests
    {
        static int checks;
        static void Check(bool ok,string why) { checks++;if(!ok)throw new Exception(why); }
        internal static TopSlabRequest Request(int d,double wall,double slab,double display=12,double ws=200,double ss=200)
        {
            return new TopSlabRequest(new object(),new TopSlabGeometry(0,0,800,wall,slab,900,0,d),TopSlabBase.Beam,4,
                new double[]{12,150,16,150,10,150,6,ws,8,ss,40,60,300,350,200,31},new double[]{25,12,display,20,30},new[]{"a","b","c","d"});
        }
        internal static void Run()
        {
            int acceptedCases=0,refusedCases=0;
            foreach(double wall in new[]{250.0,300,350})foreach(double slab in new[]{180.0,200,250})foreach(double dd in new[]{8.0,12})
            foreach(double ws in new[]{160.0,200,250})foreach(double ss in new[]{170.0,230})
            {
                var r=Request(1,wall,slab,dd,ws,ss);var mirror=Request(-1,wall,slab,dd,ws,ss);
                TopSlabLayout a,b;
                try { a=TopSlabRebar.Calculate(r); }
                catch(ArgumentException failure)
                {
                    Check(failure.Message.Contains("点筋与真实交叉筋冲突"),"only fixed-row collision is an expected refusal");
                    bool refused=false;try { TopSlabRebar.Calculate(mirror); }catch(ArgumentException) { refused=true; }
                    Check(refused,"fixed blue row collision rejects both directions");refusedCases++;continue;
                }
                acceptedCases++;
                b=TopSlabRebar.Calculate(mirror);
                double off=(r.A(1)+dd)/2;
                foreach(var p in a.WallRightDots.Take(1).Concat(a.WallTopDots))
                    Check(Math.Abs(a.Bars[1].Last().Y-p.Y-off)<1e-7,"each top circle tangent to blue underside");
                Check(Math.Abs(Math.Sqrt(Math.Pow(a.Bars[1].Last().X-a.WallLeftDots[0].X,2)+dd*dd/4)-dd/2-10)<1e-7,"automatic 10mm visible endpoint clearance");
                Check(Math.Abs(a.Bars[0].Max(v=>v.Y)-a.Bars[1].Last().Y-r.A(1)-10)<1e-7,"red blue visible horizontal gap 10mm");
                Check(Math.Abs(a.WallLeftDots[0].X-a.Bars[0][0].X-off)<1e-7,"left corner touches red vertical segment");
                Check(a.LowerBendX-(31+off)>=off-1e-7,"lower bend clears outside point row");
                Check(r.Record()[0]==3&&r.Record()[12]==0,"v3 automatic slot, no old A writes");
                Check(Math.Abs(r.Record(a.EffectiveA)[12]-a.EffectiveA)<1e-7,"block snapshot retains derived A");
                Check(a.WallDots.Count==b.WallDots.Count&&a.SlabDots.Count==b.SlabDots.Count,"mirror counts");
                foreach(var lists in new[]{new[]{a.WallDots,b.WallDots},new[]{a.SlabDots,b.SlabDots}})
                    for(int i=0;i<lists[0].Count;i++)Check(Math.Abs(lists[0][i].X+lists[1][i].X)<1e-7&&Math.Abs(lists[0][i].Y-lists[1][i].Y)<1e-7,"mirror positions");
                Check(a.SlabDots.All(p=>p.X-dd/2>=wall-1e-7),"slab domain ownership");
                Check(a.WallDots.All(p=>p.X+dd/2<=wall+1e-7),"wall domain ownership");
                Check(a.SlabBoundaryClearance>=-1e-7&&a.SlabBoundaryClearance<=ss+1e-7,"real residual at support boundary");
                Check(Math.Abs(a.SlabFirstCenterOffset-a.SlabBoundaryClearance-dd/2)<1e-7,"recorded first offset");
                var all=a.WallDots.Concat(a.SlabDots).ToArray();for(int i=0;i<all.Length;i++)for(int j=0;j<i;j++)Check(BeamSideMath.Distance(all[i],all[j])>=dd-1e-7,"no overlapping dots");
                Check(a.WallLeftDots.Count==a.WallRightDots.Count,"paired row count");
                Check(a.WallTopDots.All(p=>Math.Abs(p.Y-a.WallCommonTop)<1e-7),"all top dots same Y");
                for(int i=0;i<a.WallLeftDots.Count;i++)
                {
                    Check(Math.Abs(a.WallLeftDots[i].Y-a.WallRightDots[i].Y)<1e-7,"synchronized side row Y");
                    Check(Math.Abs(a.WallLeftDots[i].Y-(a.WallCommonTop-i*ws))<1e-7,"exact synchronized input spacing");
                }
                var sizes=Enumerable.Range(0,5).Select(i=>new TopSlabTextSize(140+i*17,25+i*2)).ToArray();
                var dim=Enumerable.Range(0,4).Select(i=>new TopSlabTextSize(80,50)).ToArray();
                var plan=TopSlabAnnotationGeometry.Calculate(r,a,sizes,dim);
                var reflected=TopSlabAnnotationGeometry.Calculate(mirror,b,sizes,dim);
                foreach(int i in new[]{0,3})Check(plan.Labels[i].TextAnchor.X<0&&!plan.Labels[i].Rightward,"wall labels outside left");
                Check(plan.Labels[1].TextAnchor.X>wall&&plan.Labels[1].Rightward,"inner bar label on inner side");
                foreach(int group in new[]{3,4})
                {
                    var lists=group==3?new[]{a.WallLeftDots,a.WallRightDots}:new[]{a.SlabDots.Take(a.SlabDots.Count/2).ToList(),a.SlabDots.Skip(a.SlabDots.Count/2).ToList()};
                    foreach(var actual in lists)Check(plan.Labels[group].Leaders.Any(line=>actual.Any(p=>Math.Abs(p.X-line.A.X)<1e-7&&Math.Abs(p.Y-line.A.Y)<1e-7)),"one text branches to each actual row");
                }
                foreach(int i in new[]{2,4})Check(plan.Labels[i].TextAnchor.Y+sizes[i].Height<800-slab,"slab labels below");
                Check(plan.Dimensions.Take(2).All(d=>d.Line.Y>800)&&plan.Dimensions[1].Line.Y==plan.Dimensions[0].Line.Y,"horizontal dimensions share Y");
                Check(plan.Dimensions[0].A.X==0&&plan.Dimensions[0].B.X==wall&&plan.Dimensions[0].A.Y==800&&plan.Dimensions[0].B.Y==800&&plan.Dimensions[1].A.Y==800&&plan.Dimensions[1].B.Y==800,"wall thickness definition unchanged");
                Check(plan.Dimensions[1].A.X==wall&&plan.Dimensions[1].B.X==wall+900,"projection definition excludes stem");
                for(int i=0;i<5;i++)Check(Math.Abs(plan.Labels[i].TextAnchor.X+reflected.Labels[i].TextAnchor.X)<1e-7&&plan.Labels[i].Rightward!=reflected.Labels[i].Rightward,"label mirror, positive glyph flag");
                for(int j=0;j<3;j++)for(int i=0;i<a.Bars[j].Count;i++)Check(a.Bars[j][i].X==-b.Bars[j][i].X&&a.Bars[j][i].Y==b.Bars[j][i].Y&&a.Bars[j][i].Bulge==-b.Bars[j][i].Bulge,"bar path mirrored unchanged");
            }
            Check(acceptedCases>50&&refusedCases>0,"matrix covers valid results and explicit refusal");
            Console.WriteLine("Auto matrix: valid="+acceptedCases+", fixed-row collision refusal="+refusedCases);
            Preview(Request(1,300,200),"right-full",false);
            Preview(Request(1,300,200),"right-junction",true);
            Preview(Request(-1,350,180),"left-different-thickness",false);
            var baseRequest=Request(1,300,200);var baseline=TopSlabRebar.Calculate(baseRequest);
            bool rejected=false;try { TopSlabRebar.Calculate(Request(1,300,200,12,13,200)); }catch(ArgumentException) { rejected=true; }
            Check(rejected,"no common phase must reject, no isolated shifts or deleted rows");
            foreach(int direction in new[]{-1,1})foreach(var diameter in new[]{new[]{8.0,12,16},new[]{16.0,8,12},new[]{20.0,16,12}})
            {
                var numbers=Enumerable.Range(0,16).Select(baseRequest.V).ToArray();numbers[0]=diameter[0];numbers[2]=diameter[1];numbers[4]=diameter[2];
                var changed=new TopSlabRequest(new object(),Request(direction,300,200).Geometry,baseRequest.Base,4,numbers,new double[]{25,12,12,20,30},new[]{"a","b","c","d"});
                var arranged=TopSlabRebar.Calculate(changed);
                Check(arranged.WallLeftDots.Select(p=>p.Y).SequenceEqual(arranged.WallRightDots.Select(p=>p.Y)),"different line diameter synchronized rows");
            }
            Check(Math.Abs(baseline.WallCommonTop-735)<1e-7,"top touches blue horizontal segment underside");
            var independent=TopSlabRebar.Calculate(Request(1,300,200,12,250,200));
            Check(baseline.SlabDots.SequenceEqual(independent.SlabDots),"wall spacing must not drive slab positions");
            independent=TopSlabRebar.Calculate(Request(1,300,200,12,200,230));
            Check(baseline.WallDots.SequenceEqual(independent.WallDots),"slab spacing must not drive wall positions");
            var expected=new[]{
                new[]{new RebarVertex(31,-300,0),new RebarVertex(31,749,-Math.Tan(Math.PI/8)),new RebarVertex(51,769,0),new RebarVertex(1149,769,-Math.Tan(Math.PI/8)),new RebarVertex(1169,749,0),new RebarVertex(1169,640,0)},
                new[]{new RebarVertex(267,-350,0),new RebarVertex(267,727,Math.Tan(Math.PI/8)),new RebarVertex(247,747,0),new RebarVertex(57.8323969741913,747,0)},
                new[]{new RebarVertex(1170,630,0),new RebarVertex(75,630,Math.Tan(Math.PI/8)),new RebarVertex(55,610,0),new RebarVertex(55,550,0)}};
            for(int i=0;i<3;i++) { Check(baseline.Bars[i].Count==expected[i].Length,"golden bar vertex count");for(int j=0;j<expected[i].Length;j++)
                Check(Math.Abs(baseline.Bars[i][j].X-expected[i][j].X)<1e-8&&Math.Abs(baseline.Bars[i][j].Y-expected[i][j].Y)<1e-8&&Math.Abs(baseline.Bars[i][j].Bulge-expected[i][j].Bulge)<1e-8,"original three-bar path golden"); }
            foreach(double lineWidth in new[]{8.0,16})
            {
                var numbers=Enumerable.Range(0,16).Select(baseRequest.V).ToArray();numbers[6]=10;numbers[8]=6;
                var changed=new TopSlabRequest(new object(),baseRequest.Geometry,baseRequest.Base,4,numbers,new[]{25,lineWidth,10,20,30},new[]{"a","b","c","d"});
                var dots=TopSlabRebar.Calculate(changed);Check(dots.WallDots.Count>0&&dots.SlabDots.Count>0,"independent specs and display widths");
            }
            var large=Enumerable.Range(0,5).Select(i=>new TopSlabTextSize(600+i*70,80+i*5)).ToArray();
            var dsize=Enumerable.Range(0,4).Select(i=>new TopSlabTextSize(180,80)).ToArray();
            var spaced=TopSlabAnnotationGeometry.Calculate(baseRequest,baseline,large,dsize);
            var order=new[]{3,0};for(int i=1;i<order.Length;i++)Check(spaced.Labels[order[i]].TextAnchor.Y>spaced.Labels[order[i-1]].TextAnchor.Y+large[order[i-1]].Height,"measured tall text spacing");
            Check(spaced.Dimensions[2].Line.X<new[]{0,3}.Min(i=>spaced.Labels[i].TextAnchor.X-large[i].Width),"height dimension outside measured labels");
            foreach(int i in new[]{2,4})
            {
                var innerText=spaced.Labels[1].TextAnchor;var slabText=spaced.Labels[i].TextAnchor;
                Check(innerText.Y+large[1].Height<=slabText.Y||slabText.Y+large[i].Height<=innerText.Y||innerText.X+large[1].Width<=slabText.X||slabText.X+large[i].Width<=innerText.X,"measured inner/slab labels do not overlap");
            }
            Console.WriteLine("TopSlabRevisionTests PASS: "+checks);
        }
        static string F(double d) { return d.ToString("0.###",CultureInfo.InvariantCulture); }
        static void Preview(TopSlabRequest r,string name,bool zoom)
        {
            var g=r.Geometry;var steel=TopSlabRebar.Calculate(r);double width=g.WallThickness+g.Projection;
            double xmin=g.Direction>0?-550:-width-300,xmax=g.Direction>0?width+300:550;
            double ymin=-430,ymax=1100;
            if(zoom) { xmin=-15;xmax=g.WallThickness+180;ymin=800-g.SlabThickness-170;ymax=870; }
            var s=new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='1400' height='1000' viewBox='"+F(xmin)+" "+F(-ymax)+" "+F(xmax-xmin)+" "+F(ymax-ymin)+"'><rect x='"+F(xmin)+"' y='"+F(-ymax)+"' width='"+F(xmax-xmin)+"' height='"+F(ymax-ymin)+"' fill='#142131'/><g transform='scale(1,-1)'>");
            s.Append("<path d='M0 0H"+F(g.Direction*g.WallThickness)+"V"+F(800-g.SlabThickness)+"H"+F(g.Direction*width)+"V800H0Z' fill='none' stroke='#eeeeee' stroke-width='2'/>");
            s.Append("<path d='M"+F(g.InnerX)+" "+F(800-g.SlabThickness-80)+"V830' fill='none' stroke='#ffe178' stroke-width='2' stroke-dasharray='8 5'/>");
            foreach(var bar in steel.Bars)
            {
                s.Append("<path d='");for(int i=0;i<bar.Count-1;i++)
                {
                    var a=bar[i];var b=bar[i+1];var curve=new BeamSideCurve(new ContourPoint(a.X,a.Y),new ContourPoint(b.X,b.Y),a.Bulge);
                    if(i==0)s.Append("M"+F(a.X)+" "+F(a.Y));
                    for(int j=1;j<=24;j++) { var p=curve.At(j/24.0);s.Append("L"+F(p.X)+" "+F(p.Y)); }
                }
                s.Append("' fill='none' stroke='"+new[]{"#ff7770","#ffb15a","#cf9bff"}[steel.Bars.IndexOf(bar)]+"' stroke-width='12'/>");
            }
            foreach(var dots in new[]{steel.WallDots,steel.SlabDots})foreach(var p in dots)s.Append("<circle cx='"+F(p.X)+"' cy='"+F(p.Y)+"' r='"+F(r.A(2)/2)+"' fill='"+(ReferenceEquals(dots,steel.WallDots)?"#5fd7ff":"#ffda53")+"'/>");
            s.Append("</g>");
            if(zoom)
            {
                s.Append("<path d='M20 "+F(-steel.WallCommonTop)+"H"+F(g.WallThickness-15)+"' stroke='#ffffff' stroke-dasharray='3 2' fill='none'/>");
                s.Append("<text x='80' y='"+F(-g.Height-25)+"' fill='#ffffff' font-size='9'>共同顶部Y="+F(steel.WallCommonTop)+"；左右同步向下s="+F(r.V(7))+"</text>");
                if(steel.WallLeftDots.Count>1)s.Append("<path d='M100 "+F(-steel.WallCommonTop)+"V"+F(-steel.WallLeftDots[1].Y)+"' stroke='#ffffff' fill='none'/>");
            }
            if(!zoom)
            {
                var sizes=Enumerable.Range(0,5).Select(i=>new TopSlabTextSize(150,30)).ToArray();var dims=Enumerable.Range(0,4).Select(i=>new TopSlabTextSize(90,50)).ToArray();
                var plan=TopSlabAnnotationGeometry.Calculate(r,steel,sizes,dims);
                foreach(var label in plan.Labels)
                {
                    foreach(var line in label.Leaders)s.Append("<path d='M"+F(line.A.X)+" "+F(-line.A.Y)+"L"+F(line.B.X)+" "+F(-line.B.Y)+"' stroke='#c6ec91' fill='none'/>");
                    s.Append("<text x='"+F(label.TextAnchor.X)+"' y='"+F(-label.TextAnchor.Y)+"' text-anchor='"+(label.Rightward?"start":"end")+"' fill='#c6ec91' font-family='Microsoft YaHei' font-size='26'>"+label.Text+"</text>");
                }
                foreach(var d in plan.Dimensions)
                {
                    var a=d.Horizontal?new AnnotationPoint(d.A.X,d.Line.Y):new AnnotationPoint(d.Line.X,d.A.Y);var b=d.Horizontal?new AnnotationPoint(d.B.X,d.Line.Y):new AnnotationPoint(d.Line.X,d.B.Y);
                    s.Append("<path d='M"+F(d.A.X)+" "+F(-d.A.Y)+"L"+F(a.X)+" "+F(-a.Y)+"L"+F(b.X)+" "+F(-b.Y)+"L"+F(d.B.X)+" "+F(-d.B.Y)+"' stroke='#adb7c4' fill='none'/>");
                    s.Append("<text text-anchor='middle' x='"+F((a.X+b.X)/2)+"' y='"+F(-(a.Y+b.Y)/2-12)+"' fill='#eeeeee' font-size='26'>"+F(d.Horizontal?Math.Abs(d.B.X-d.A.X):Math.Abs(d.B.Y-d.A.Y))+"</text>");
                }
            }
            s.Append("<text x='"+F(xmin+15)+"' y='"+F(-ymax+(zoom?22:35))+"' fill='#ffffff' font-family='Microsoft YaHei' font-size='"+(zoom?13:24)+"'>"+(zoom?"同例阴角放大":"纯计算预览："+(g.Direction>0?"右飘":"左飘，不同厚度"))+"</text>");
            s.Append("<rect x='"+F(xmin)+"' y='"+F(-ymin-38)+"' width='"+F(xmax-xmin)+"' height='38' fill='#142131'/><text x='"+F(xmin+15)+"' y='"+F(-ymin-14)+"' fill='#5fd7ff' font-family='Microsoft YaHei' font-size='"+(zoom?8:20)+"'>蓝：反坎点筋　黄：飘板点筋　虚线：内侧分界；审阅色，不是生产颜色</text>");
            s.Append("</svg>");Directory.CreateDirectory("docs/top-slab-review");File.WriteAllText("docs/top-slab-review/"+name+".svg",s.ToString());
            Console.WriteLine(name+": first center offset="+F(steel.SlabFirstCenterOffset)+", boundary clearance="+F(steel.SlabBoundaryClearance));
        }
    }
}
