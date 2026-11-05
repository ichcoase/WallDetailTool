using System;
using System.Collections.Generic;
using System.Linq;

namespace WallDetailTool
{
    // Array slots are specified in TopSlabForm.Names. Arrays are always defensively copied.
    internal sealed class TopSlabRequest
    {
        internal readonly object Target;
        internal readonly TopSlabGeometry Geometry;
        internal readonly TopSlabBase Base;
        internal readonly int UnitCode;
        private readonly double[] values,advanced;
        private readonly string[] layers;
        internal double V(int i) { return values[i]; }
        internal double A(int i) { return advanced[i]; }
        internal string Layer(int i) { return layers[i]; }
        internal TopSlabRequest(object target,TopSlabGeometry geometry,TopSlabBase type,int units,double[] numbers,double[] settings,string[] layerNames)
        {
            if(target==null||geometry==null||numbers==null||numbers.Length!=16||settings==null||settings.Length!=5||layerNames==null||layerNames.Length!=4)
                throw new ArgumentException("反坎带飘板请求不完整。");
            if(type!=TopSlabBase.Beam&&type!=TopSlabBase.Slab) throw new ArgumentException("底部连接类型无效。");
            if(units!=0&&units!=4) throw new ArgumentException("第一版仅支持毫米或无单位（按毫米）图纸。");
            values=(double[])numbers.Clone();values[10]=0;advanced=(double[])settings.Clone();layers=(string[])layerNames.Clone();
            foreach(double v in values.Concat(advanced)) if(double.IsNaN(v)||double.IsInfinity(v)||v<0) throw new ArgumentException("参数必须为有限非负数。");
            for(int i=0;i<14;i++) if(i!=10&&values[i]<=0) throw new ArgumentException("启用配筋长度及规格必须为正数；B须填写设计值，A自动计算。");
            if(type==TopSlabBase.Slab&&(values[14]<=0||values[15]<=0||values[15]>=values[14])) throw new ArgumentException("底部板厚h与H必须满足0 < H < h。");
            for(int i=1;i<5;i++) if(advanced[i]<=0) throw new ArgumentException("显示尺寸、半径及字高必须为正数。");
            if(layers.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("图层不完整。");
            Target=target;Geometry=geometry;Base=type;UnitCode=units;
        }
        internal bool Matches(object active,int units) { return ReferenceEquals(Target,active)&&UnitCode==units; }
        internal double[] Record(double? effectiveA=null)
        {
            var data=new[]{3.0,(double)Base}.Concat(values).Concat(advanced).ToArray();
            if(effectiveA.HasValue)data[12]=CantileverNumbers.Positive(effectiveA.Value);
            return data;
        }
        internal static string LastKey(TopSlabBase type) { return type==TopSlabBase.Beam?"WDT_TOP_SLAB_BEAM_LAST":"WDT_TOP_SLAB_SLAB_LAST"; }
    }
    internal sealed class TopSlabLayout
    {
        internal readonly List<List<RebarVertex>> Bars=new List<List<RebarVertex>>();
        internal readonly List<ContourPoint> WallDots=new List<ContourPoint>(), SlabDots=new List<ContourPoint>();
        internal readonly List<ContourPoint> WallLeftDots=new List<ContourPoint>(),WallRightDots=new List<ContourPoint>(),WallTopDots=new List<ContourPoint>(),WallCornerDots=new List<ContourPoint>();
        internal double SlabFirstCenterOffset,SlabBoundaryClearance,WallCommonTop,EffectiveA,InnerEndX,LowerBendX;
    }
    internal static class TopSlabRebar
    {
        internal static TopSlabLayout Calculate(TopSlabRequest r)
        {
            var g=r.Geometry;double c=r.A(0),w=r.A(1),dd=r.A(2),radius=r.A(3),h=g.Height,t=g.WallThickness,l=t+g.Projection;
            double outer=c+r.V(0)/2,inner=t-c-r.V(2)/2,top=h-c-r.V(0)/2,bottom=h-g.SlabThickness+c+r.V(4)/2;
            // Calculation diameters locate concrete cover offsets; the two visible
            // clearances below follow the user's independent display-edge rule.
            // User confirmed 2026-11-05: visible-edge clear gaps, independent of calculation diameters.
            double innerTop=top-w-10;
            double innerEnd=outer+UpstandDotDistribution.InnerOffset(w,dd)+Math.Sqrt(100+10*dd),effectiveA=inner-radius-innerEnd;
            if(effectiveA<=0)throw new ArgumentException("自动A不足以形成顶部水平直段，不缩小R或改变10mm端部规则。");
            // Minimum inward position that clears the outside row's displayed
            // circles. Derived from actual display sizes, no added design length.
            double down=Math.Max(outer+(r.V(0)+r.V(4))/2,
                outer+2*UpstandDotDistribution.InnerOffset(w,dd));
            if(radius<=w/2||outer+w/2>=inner-w/2||bottom+w/2>=top-w/2||innerTop-radius<=0)
                throw new ArgumentException("厚度、保护层、直径或R不能容纳三根独立折筋。");
            var frame=new CantileverFrame((CantileverDirection)g.Direction,g.OuterX,g.Bottom,t);
            var specs=new CantileverSpecifications(CantileverSupportMode.InnerContinuous,
                new CantileverSpecification(CantileverBarRole.InnerExtension,r.V(0),r.V(1)),
                new CantileverSpecification(CantileverBarRole.Bottom,r.V(4),r.V(5)));
            var tail=CantileverRebarGeometry.Calculate(new CantileverFrame(CantileverDirection.Right,0,h,t),specs,
                new CantileverPathInputs(l,g.SlabThickness,c,radius,w,0,0,1,0,1,0));
            double endY=tail.Bars[0].Vertices.Last().Point.Y;
            double free=l-c-r.V(0)/2;
            var outerPath=new List<CantileverPoint>();var innerPath=new List<CantileverPoint>();
            if(r.Base==TopSlabBase.Beam)
            {
                outerPath.Add(new CantileverPoint(outer,-r.V(12)));
                innerPath.Add(new CantileverPoint(inner,-r.V(13)));
            }
            else
            {
                double baseY=-r.V(14)+r.V(15);
                if(baseY+radius>=0) throw new ArgumentException("底部板厚、H及R不能在连接面以下完成弯折。");
                outerPath.Add(new CantileverPoint(outer-radius-r.V(12),baseY));outerPath.Add(new CantileverPoint(outer,baseY));
                innerPath.Add(new CantileverPoint(inner+radius+r.V(13),baseY));innerPath.Add(new CantileverPoint(inner,baseY));
            }
            outerPath.Add(new CantileverPoint(outer,top));outerPath.Add(new CantileverPoint(free,top));outerPath.Add(new CantileverPoint(free,endY));
            innerPath.Add(new CantileverPoint(inner,innerTop));innerPath.Add(new CantileverPoint(innerEnd,innerTop));
            var bottomPath=new List<CantileverPoint>{new CantileverPoint(l-c-r.V(4)/2,bottom),new CantileverPoint(down,bottom),new CantileverPoint(down,bottom-radius-r.V(11))};
            if(innerEnd<c+r.V(2)/2||bottom-radius-r.V(11)<c+r.V(4)/2)
                throw new ArgumentException("阴角A或B超出保护层允许范围；不自动缩短设计值。");
            if(bottom-radius<=0||innerTop<=bottom||down+radius>=inner)
                throw new ArgumentException("阴角两根筋无法形成交叉弯折，请调整厚度、直径或R。");
            var result=new TopSlabLayout { EffectiveA=effectiveA,InnerEndX=innerEnd,LowerBendX=down };
            foreach(var corners in new[]{outerPath,innerPath,bottomPath})
            {
                for(int i=0;i<corners.Count-1;i++)
                {
                    double length=Math.Abs(corners[i+1].X-corners[i].X)+Math.Abs(corners[i+1].Y-corners[i].Y);
                    if(length<=radius*((i>0?1:0)+(i+1<corners.Count-1?1:0))+1e-8)
                        throw new ArgumentException("相邻弯折修剪后没有正向直段，不自动缩小R。");
                }
                var vertices=CantileverRebarGeometry.Round(frame,corners,radius);
                result.Bars.Add(vertices.Select(v=>new RebarVertex(v.Point.X,v.Point.Y,v.Bulge)).ToList());
            }
            TopSlabDots.Calculate(r,result);
            return result;
        }
    }
}
