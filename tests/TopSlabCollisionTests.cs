using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Globalization;
namespace WallDetailTool
{
    internal static class TopSlabCollisionTests
    {
        static int checks;
        static void Check(bool value,string name) { checks++;if(!value)throw new Exception(name); }
        internal static TopSlabRequest Request(int direction,double spacing,TopSlabBase type=TopSlabBase.Beam)
        {
            return new TopSlabRequest(new object(),new TopSlabGeometry(0,0,1287.12590021827,322.938364858277,322.579945494302,1451.62391706825,0,direction),type,4,
                new double[]{8,150,8,150,8,150,6,spacing,6,200,0,60,300,300,200,31},new double[]{25,8,12,20,62.5},new[]{"a","b","c","d"});
        }
        internal static void Run()
        {
            foreach(int direction in new[]{-1,1})foreach(double spacing in new[]{150.0,200})foreach(var type in new[]{TopSlabBase.Beam,TopSlabBase.Slab})
            {
                var r=Request(direction,spacing,type);var a=TopSlabRebar.Calculate(r);
                double x=a.WallLeftDots[0].X*direction,y=a.WallCommonTop;
                Check(Math.Abs(x-39)<1e-7,"left corner stays on red vertical inner edge");
                Check(Math.Abs(y-1230.12590021827)<1e-7,"fixed blue tangent Y preserved");
                Check(Math.Abs(a.Bars[0].Max(v=>v.Y)-a.Bars[1].Last().Y-r.A(1)-10)<1e-7,"10mm red blue displayed gap");
                Check(Math.Abs(Math.Sqrt(Math.Pow(a.Bars[1].Last().X*direction-x,2)+36)-6-10)<1e-7,"10mm blue endpoint to dot edge");
                var top=a.WallCornerDots.Concat(a.WallTopDots).OrderBy(p=>p.X*direction).ToArray();
                for(int i=0;i<top.Length;i++)
                {
                    Check(Math.Abs(top[i].Y-y)<1e-7,"whole top row same Y");
                    if(i>0)Check(Math.Abs(top[i].X-top[i-1].X)<=spacing+1e-7,"top spacing maximum");
                }
                foreach(var p in a.WallDots.Concat(a.SlabDots))foreach(var bar in a.Bars)for(int i=0;i<bar.Count-1;i++)
                {
                    var c=new BeamSideCurve(new ContourPoint(bar[i].X,bar[i].Y),new ContourPoint(bar[i+1].X,bar[i+1].Y),bar[i].Bulge);
                    double t=c.Parameter(p),distance=Math.Min(BeamSideMath.Distance(p,c.A),BeamSideMath.Distance(p,c.B));
                    if(t>=0&&t<=1)distance=Math.Min(distance,BeamSideMath.Distance(p,c.At(t)));
                    Check(distance>=10-1e-7,"all dots clear every real bar segment");
                }
                for(int i=1;i<a.WallLeftDots.Count;i++)
                {
                    Check(Math.Abs(a.WallLeftDots[i].X*direction-39)<1e-7,"lower side X unchanged");
                    Check(a.WallLeftDots[i].Y==a.WallRightDots[i].Y&&Math.Abs(a.WallLeftDots[i].Y-(y-i*spacing))<1e-7,"sides remain synchronized");
                }
                Check(Math.Abs(a.Bars[1].Last().X*direction-(39+Math.Sqrt(220)))<1e-7&&a.LowerBendX==49,"new endpoint and unchanged lower bend");
            }
            foreach(int direction in new[]{-1,1})
            {
                bool refused=false;try { TopSlabRebar.Calculate(Request(direction,240)); }catch(ArgumentException e){refused=e.Message.Contains("点筋与真实交叉筋冲突");}
                Check(refused,"independent side collision still rejected");
            }
            Preview(false);Preview(true);Preview(true,true);
            Console.WriteLine("TopSlabCollisionTests PASS: "+checks);
        }
        static string F(double v) { return v.ToString("0.######",CultureInfo.InvariantCulture); }
        static void Preview(bool zoom,bool close=false)
        {
            var r=Request(1,200);var g=r.Geometry;var a=TopSlabRebar.Calculate(r);double h=g.Height;
            double left=zoom?10:-90,right=zoom?(close?85:365):1900,bottom=zoom?(close?h-90:h-390):-340,upper=zoom?h+15:h+130;
            var s=new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' style='background:#152131' width='1400' height='1000' viewBox='"+F(left)+" "+F(-upper)+" "+F(right-left)+" "+F(upper-bottom)+"'>");
            s.Append("<rect x='"+F(left)+"' y='"+F(-upper)+"' width='"+F(right-left)+"' height='"+F(upper-bottom)+"' fill='#152131'/><g transform='scale(1,-1)'>");
            s.Append("<path d='M0 0H"+F(g.WallThickness)+"V"+F(h-g.SlabThickness)+"H"+F(g.WallThickness+g.Projection)+"V"+F(h)+"H0Z' fill='none' stroke='#eee' stroke-width='1'/>");
            for(int j=0;j<3;j++)
            {
                var bar=a.Bars[j];s.Append("<path d='M"+F(bar[0].X)+" "+F(bar[0].Y));
                for(int i=0;i<bar.Count-1;i++)
                {
                    var v=bar[i];var b=bar[i+1];var c=new BeamSideCurve(new ContourPoint(v.X,v.Y),new ContourPoint(b.X,b.Y),v.Bulge);
                    s.Append(c.IsArc?"A"+F(c.Radius)+" "+F(c.Radius)+" 0 0 "+(v.Bulge>0?"1":"0")+" ":"L");s.Append(F(b.X)+" "+F(b.Y));
                }
                s.Append("' fill='none' stroke='"+new[]{"#ff7770","#55acff","#a5e77c"}[j]+"' stroke-width='8' stroke-linecap='butt'/>");
            }
            foreach(var p in a.WallDots)s.Append("<circle cx='"+F(p.X)+"' cy='"+F(p.Y)+"' r='6' fill='#ffcf60'/>");
            foreach(var p in a.SlabDots)s.Append("<circle cx='"+F(p.X)+"' cy='"+F(p.Y)+"' r='6' fill='#d08dff'/>");
            s.Append("<path d='M10 "+F(a.WallCommonTop)+"H360' stroke='#fff' stroke-width='0.4' stroke-dasharray='3 2'/>");
            s.Append("</g>");
            if(close)
            {
                double redBottom=a.Bars[0].Max(v=>v.Y)-4,blueTop=a.Bars[1].Last().Y+4,y=a.WallCommonTop;
                double end=a.Bars[1].Last().X,cornerY=a.Bars[1].Last().Y-4,dx=end-39,dy=cornerY-y,length=Math.Sqrt(dx*dx+dy*dy);
                s.Append("<path d='M70 "+F(-redBottom)+"V"+F(-blueTop)+"M68 "+F(-redBottom)+"H72M68 "+F(-blueTop)+"H72M"+F(39+6*dx/length)+" "+F(-y-6*dy/length)+"L"+F(end)+" "+F(-cornerY)+"' fill='none' stroke='#fff' stroke-width='0.25'/>");
                s.Append("<text x='73' y='"+F(-(redBottom+blueTop)/2+1)+"' fill='#fff' font-size='2.5'>10</text><text x='47' y='"+F(-y+10)+"' fill='#fff' font-size='2.5'>10</text>");
            }
            double font=close?2.4:zoom?7:25;
            s.Append("<text x='"+F(left+3)+"' y='"+F(-upper+font*1.5)+"' fill='white' font-family='Microsoft YaHei' font-size='"+F(font)+"'>"+(close?"两处图面净距10mm · 顶排同标高":zoom?"实际CAD尺寸 · 顶部点筋同标高":"修正后纯计算图 · CAD手绘试例 · 反坎点筋6@200")+"</text>");
            s.Append("<text x='"+F(left+3)+"' y='"+F(-bottom-font)+"' fill='white' font-family='Microsoft YaHei' font-size='"+F(font)+"'>"+(close?"左角点贴红色竖段；其余顶点贴蓝色水平段":"红/蓝/绿：三根独立线筋；黄：反坎点筋；紫：飘板点筋。审阅色。")+"</text></svg>");
            Directory.CreateDirectory("docs/top-slab-gap-review");File.WriteAllText("docs/top-slab-gap-review/"+(close?"top-contact":zoom?"junction":"full")+".svg",s.ToString());
        }
    }
}
