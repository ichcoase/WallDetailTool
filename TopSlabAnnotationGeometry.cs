using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WallDetailTool
{
    internal struct TopSlabTextSize
    {
        internal readonly double Width,Height;
        internal TopSlabTextSize(double w,double h) { Width=CantileverNumbers.Positive(w);Height=CantileverNumbers.Positive(h); }
    }
    internal sealed class TopSlabDimension
    {
        internal AnnotationPoint A,B,Line;
        internal bool Horizontal;
    }
    internal sealed class TopSlabAnnotationPlan
    {
        internal readonly UpstandLabelLayout[] Labels=new UpstandLabelLayout[5];
        internal readonly List<TopSlabDimension> Dimensions=new List<TopSlabDimension>();
    }
    internal static class TopSlabAnnotationGeometry
    {
        internal static string Spec(TopSlabRequest r,int group)
        { return "φ"+r.V(group*2).ToString("g",CultureInfo.InvariantCulture)+"@"+r.V(group*2+1).ToString("g",CultureInfo.InvariantCulture); }
        internal static TopSlabAnnotationPlan Calculate(TopSlabRequest r,TopSlabLayout steel,IList<TopSlabTextSize> measured,IList<TopSlabTextSize> dimensions)
        {
            if(measured==null||measured.Count!=5||dimensions==null||dimensions.Count!=4)throw new ArgumentException("缺少实际文字测量结果。");
            var g=r.Geometry;var plan=new TopSlabAnnotationPlan();double h=g.Height,t=g.WallThickness,length=t+g.Projection;
            double gap=Math.Max(20,measured.Max(v=>v.Height)*.6);
            double junctionBottom=h-g.SlabThickness-r.A(3)-r.V(11)-UpstandDotDistribution.InnerOffset(r.A(1),r.A(2));
            if(junctionBottom<=0)throw new ArgumentException("交接区下方没有可用于竖筋标注的直段。");
            Func<double,double,AnnotationPoint> world=(x,y)=>new AnnotationPoint(g.OuterX+g.Direction*x,g.Bottom+y);
            var anchors=new ContourPoint[5];
            anchors[0]=VerticalAnchor(r,steel.Bars[0],junctionBottom*.72,junctionBottom);
            anchors[1]=VerticalAnchor(r,steel.Bars[1],junctionBottom*.47,junctionBottom);
            var wallDot=steel.WallLeftDots.OrderBy(p=>Math.Abs(p.Y-g.Bottom-junctionBottom*.2)).First();
            anchors[3]=new ContourPoint((wallDot.X-g.OuterX)*g.Direction,wallDot.Y-g.Bottom);
            var bottom=steel.Bars[2];var a=bottom[0];var b=bottom[1];
            double bx=(a.X-g.OuterX)*g.Direction,ex=(b.X-g.OuterX)*g.Direction;
            anchors[2]=new ContourPoint(BeamSideMath.Clamp(t+g.Projection*.7,Math.Min(bx,ex),Math.Max(bx,ex)),a.Y-g.Bottom);
            var lower=steel.SlabDots.Skip(steel.SlabDots.Count/2).OrderBy(p=>Math.Abs((p.X-g.InnerX)*g.Direction-g.Projection*.25)).First();
            anchors[4]=new ContourPoint((lower.X-g.OuterX)*g.Direction,lower.Y-g.Bottom);
            double previousTop=double.NegativeInfinity;
            foreach(int i in new[]{3,0})
            {
                double row=Math.Max(anchors[i].Y,previousTop+gap);
                var label=Create(r,i,world,anchors[i],-gap,row,-gap*.5,false);
                plan.Labels[i]=label;previousTop=row+Math.Max(4,r.A(4)*.15)+measured[i].Height;
            }
            double innerRow=Math.Min(anchors[1].Y,h-g.SlabThickness-gap-measured[1].Height-Math.Max(4,r.A(4)*.15));
            plan.Labels[1]=Create(r,1,world,anchors[1],t+gap,innerRow,t+gap*.5,true);
            var otherWall=steel.WallRightDots.OrderBy(p=>Math.Abs(p.Y-wallDot.Y)).First();
            plan.Labels[3].Leaders.Insert(0,new AnnotationSegment(new AnnotationPoint(otherWall.X,otherWall.Y),new AnnotationPoint(wallDot.X,wallDot.Y)));
            double below=h-g.SlabThickness-gap;
            foreach(int i in new[]{2,4})
            {
                double row=below-measured[i].Height-Math.Max(4,r.A(4)*.15);
                var label=Create(r,i,world,anchors[i],anchors[i].X+gap,row,anchors[i].X,true);
                plan.Labels[i]=label;below=row-gap;
            }
            int pair=steel.SlabDots.Skip(steel.SlabDots.Count/2).ToList().FindIndex(p=>p.X==lower.X&&p.Y==lower.Y);
            var upperDot=steel.SlabDots[pair];
            plan.Labels[4].Leaders.Insert(0,new AnnotationSegment(new AnnotationPoint(upperDot.X,upperDot.Y),new AnnotationPoint(lower.X,lower.Y)));
            double innerLow=innerRow+Math.Max(4,r.A(4)*.15),innerHigh=innerLow+measured[1].Height;
            bool innerConflict=false;
            foreach(int i in new[]{2,4})
            {
                var text=plan.Labels[i].TextAnchor;
                double x=(text.X-g.OuterX)*g.Direction,y=text.Y-g.Bottom;
                double rowBottom=plan.Labels[i].Leaders.Min(line=>Math.Min(line.A.Y,line.B.Y))-g.Bottom;
                if(x<t+gap+measured[1].Width&&x+measured[i].Width>t+gap&&y<innerHigh&&y+measured[i].Height>innerLow)innerConflict=true;
                if(anchors[i].X>t+gap&&anchors[i].X<t+gap+measured[1].Width&&rowBottom<innerHigh&&anchors[i].Y>innerLow)innerConflict=true;
            }
            if(innerConflict)
            {
                innerRow=new[]{2,4}.Min(i=>plan.Labels[i].Leaders.Min(line=>Math.Min(line.A.Y,line.B.Y)))-g.Bottom-gap-measured[1].Height-Math.Max(4,r.A(4)*.15);
                plan.Labels[1]=Create(r,1,world,anchors[1],t+gap,innerRow,t+gap*.5,true);
            }
            double dimGap=Math.Max(50,dimensions.Max(v=>v.Height)*1.5);
            if(dimensions[0].Width+dimGap*.2>t||dimensions[1].Width+dimGap*.2>g.Projection)
                throw new ArgumentException("实测水平尺寸文字无法在原跨度内共线居中，请人工调整尺寸文字设置；不改变测量定义。");
            // Horizontal definition points are unchanged; only the dimension rows move.
            plan.Dimensions.Add(new TopSlabDimension { A=world(0,h),B=world(t,h),Line=world(0,h+dimGap),Horizontal=true });
            plan.Dimensions.Add(new TopSlabDimension { A=world(t,h),B=world(length,h),Line=world(t,h+dimGap),Horizontal=true });
            double labelLeft=new[]{0,3}.Min(i=> -gap-measured[i].Width);
            plan.Dimensions.Add(new TopSlabDimension { A=world(0,0),B=world(0,h),Line=world(labelLeft-dimGap-dimensions[2].Height,0) });
            plan.Dimensions.Add(new TopSlabDimension { A=world(length,h-g.SlabThickness),B=world(length,h),Line=world(length+dimGap,h) });
            return plan;
        }
        private static ContourPoint VerticalAnchor(TopSlabRequest r,List<RebarVertex> bar,double desired,double maximum)
        {
            var g=r.Geometry;
            for(int i=0;i<bar.Count-1;i++)if(bar[i].Bulge==0&&Math.Abs(bar[i].X-bar[i+1].X)<1e-8)
            {
                double low=Math.Max(0,Math.Min(bar[i].Y,bar[i+1].Y)-g.Bottom),high=Math.Min(maximum,Math.Max(bar[i].Y,bar[i+1].Y)-g.Bottom);
                if(high>low)return new ContourPoint((bar[i].X-g.OuterX)*g.Direction,BeamSideMath.Clamp(desired,low,high));
            }
            throw new ArgumentException("缺少真实竖向直段标注锚点。");
        }
        private static UpstandLabelLayout Create(TopSlabRequest r,int group,Func<double,double,AnnotationPoint> world,ContourPoint anchor,double textX,double row,double escapeX,bool localRight)
        {
            double clearance=Math.Max(4,r.A(4)*.15);
            var label=new UpstandLabelLayout { Text=Spec(r,group),Rightward=localRight?r.Geometry.Direction>0:r.Geometry.Direction<0,
                TextAnchor=world(textX,row+clearance) };
            var contact=world(anchor.X,anchor.Y);var escape=world(escapeX,anchor.Y);var elbow=world(escapeX,row);
            if(Math.Abs(contact.X-escape.X)+Math.Abs(contact.Y-escape.Y)>1e-8)label.Leaders.Add(new AnnotationSegment(contact,escape));
            if(Math.Abs(escape.Y-elbow.Y)>1e-8)label.Leaders.Add(new AnnotationSegment(escape,elbow));
            label.Leaders.Add(new AnnotationSegment(elbow,world(textX,row)));
            if(group<3)
            {
                // Same 10 x 36 mm slash construction as accepted upstand labels.
                double d=36/(2*Math.Sqrt(2));label.HasSlash=true;label.SlashWidth=10;label.SlashCenter=contact;
                label.SlashA=new AnnotationPoint(contact.X-d,contact.Y+d);label.SlashB=new AnnotationPoint(contact.X+d,contact.Y-d);
            }
            return label;
        }
    }
}
