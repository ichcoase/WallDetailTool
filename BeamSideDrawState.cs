using System;
using System.Globalization;

namespace WallDetailTool
{
    internal sealed class BeamSideDrawRequest
    {
        internal readonly BeamSideDirection Direction;
        internal readonly bool FullSection,MillimetreOverride;
        internal readonly BeamSideRebarParameters Parameters;
        internal readonly double DrawingUnitsPerMillimetre;
        internal readonly int SourceUnitCode;
        private readonly string[] layers;
        internal BeamSideDrawRequest(BeamSideDirection direction,bool full,BeamSideRebarParameters p,
            double scale,int sourceUnitCode,bool mmOverride,string[] layers)
        {
            if((direction!=BeamSideDirection.Left&&direction!=BeamSideDirection.Right)||p==null)
                throw new InvalidOperationException("节点方向或参数无效。");
            BeamSideMath.Finite(scale);
            if(scale<=0 || sourceUnitCode<0 || sourceUnitCode>32767 || (mmOverride&&Math.Abs(scale-1)>1e-12))
                throw new InvalidOperationException("图纸单位解释无效。");
            if(layers==null||layers.Length!=4) throw new InvalidOperationException("缺少四类图层。");
            foreach(string name in layers)
                if(String.IsNullOrWhiteSpace(name)||name.IndexOfAny(new[]{'<','>','/','\\','"',':',';','?','*','|',','})>=0)
                    throw new InvalidOperationException("图层名称无效。");
            Direction=direction;FullSection=full;Parameters=p;DrawingUnitsPerMillimetre=scale;
            SourceUnitCode=sourceUnitCode;MillimetreOverride=mmOverride;this.layers=(string[])layers.Clone();
        }
        internal string Layer(int i) { return layers[i]; }
        internal string[] Layers { get { return (string[])layers.Clone(); } }
        internal static bool TargetMatches(object target,object active,int expectedUnit,int actualUnit)
        { return target!=null&&ReferenceEquals(target,active)&&expectedUnit==actualUnit; }
    }
    internal sealed class BeamSideUnitTransform
    {
        internal readonly double Scale;
        internal BeamSideUnitTransform(double drawingUnitsPerMillimetre)
        {
            BeamSideMath.Finite(drawingUnitsPerMillimetre);
            if(drawingUnitsPerMillimetre<=0) throw new InvalidOperationException("单位比例必须为正数。");
            Scale=drawingUnitsPerMillimetre;
        }
        internal ContourPoint ToMillimetres(ContourPoint p) { return Point(p.X/Scale,p.Y/Scale); }
        internal ContourPoint ToDrawing(ContourPoint p) { return Point(p.X*Scale,p.Y*Scale); }
        internal double LengthToMillimetres(double value) { double v=value/Scale;BeamSideMath.Finite(v);return v; }
        internal double LengthToDrawing(double value) { double v=value*Scale;BeamSideMath.Finite(v);return v; }
        private static ContourPoint Point(double x,double y) { BeamSideMath.Finite(x,y);return new ContourPoint(x,y); }
    }
    internal static class BeamSideDrawRecord
    {
        internal const string LastKey="WDT_BEAM_SIDE_LAST",BlockKey="WDT_BEAM_SIDE_PARAMS";
        internal const int Count=20;
        internal static double[] Encode(BeamSideDrawRequest r)
        {
            var p=r.Parameters;
            return new[]{1.0,(int)r.Direction,r.FullSection?1:0,p.LineDiameter,p.LineSpacing,p.DotDiameter,p.DotSpacing,
                p.Length,p.Vertical,p.TopUp?1:0,p.BottomUp?1:0,p.AnnotationRight?1:0,p.Cover,p.LineDisplayWidth,
                p.DotDisplayDiameter,p.Radius,p.TextHeight,r.DrawingUnitsPerMillimetre,r.SourceUnitCode,r.MillimetreOverride?1:0};
        }
        private static bool Flag(double v)
        { if(v!=0&&v!=1) throw new InvalidOperationException("节点记录包含无效开关。");return v==1; }
        internal static BeamSideDrawRequest Decode(double[] source,string[] layers)
        {
            if(source==null||source.Length!=Count) throw new InvalidOperationException("梁侧挂板成功参数记录格式不支持。");
            var v=(double[])source.Clone();BeamSideMath.Finite(v);
            if(v[0]!=1 || (v[1]!=1&&v[1]!=-1) || v[18]<0 || v[18]>32767 || v[18]!=Math.Floor(v[18]))
                throw new InvalidOperationException("梁侧挂板记录版本、方向或单位无效。");
            var p=new BeamSideRebarParameters(v[7],v[8],v[12],v[3],v[4],v[5],v[6],v[13],v[14],v[15],Flag(v[9]),Flag(v[10]),Flag(v[11]),v[16]);
            return new BeamSideDrawRequest((BeamSideDirection)(int)v[1],Flag(v[2]),p,v[17],(int)v[18],Flag(v[19]),layers);
        }
        internal static double[] EncodeSnapshot(BeamSideDrawRequest r,BeamSideGeometry g)
        {
            if(g==null||r.Direction!=g.Direction||r.FullSection!=(g.InputKind==BeamSideInputKind.FullSection))
                throw new InvalidOperationException("块快照的请求与识别类型不一致。");
            var result=new double[31];Array.Copy(Encode(r),result,Count);
            result[20]=(int)g.InputKind;result[21]=g.Block.Left;result[22]=g.Block.Right;
            result[23]=g.Block.Bottom;result[24]=g.Block.Top;result[25]=g.Beam==null?0:1;
            if(g.Beam!=null) { result[26]=g.Beam.Left;result[27]=g.Beam.Right;result[28]=g.Beam.Bottom;result[29]=g.Beam.Top; }
            result[30]=g.Elevation;return result;
        }
        internal static BeamSideGeometry DecodeSnapshot(double[] source,string[] layers,out BeamSideDrawRequest request)
        {
            if(source==null||source.Length!=31) throw new InvalidOperationException("块参数快照不完整。");
            BeamSideMath.Finite(source);
            var values=new double[Count];Array.Copy(source,values,Count);request=Decode(values,layers);
            if(source[20]!=Math.Floor(source[20])||source[20]<0||source[20]>2) throw new InvalidOperationException("快照输入类型无效。");
            bool hasBeam=Flag(source[25]);
            if(!hasBeam) for(int i=26;i<30;i++) if(source[i]!=0) throw new InvalidOperationException("局部快照存在未知梁边界。");
            var g=new BeamSideGeometry(new BeamSideRect(source[21],source[22],source[23],source[24]),
                hasBeam?new BeamSideRect(source[26],source[27],source[28],source[29]):null,request.Direction,(BeamSideInputKind)(int)source[20],source[30]);
            if(request.FullSection!=(g.InputKind==BeamSideInputKind.FullSection)) throw new InvalidOperationException("快照模式不一致。");
            return g;
        }
    }
    internal sealed class BeamSideDrawState
    {
        private readonly double[] advanced;
        private readonly string[] layers;
        internal readonly string[] InitialText;
        internal readonly BeamSideDrawRequest Saved;
        internal BeamSideDrawState(BeamSideDrawRequest saved,double[] sharedAdvanced,string[] layers,double[] initialSpecifications)
        {
            if(sharedAdvanced==null||sharedAdvanced.Length!=5||initialSpecifications.Length!=4)
                throw new InvalidOperationException("缺少共享设置或规格。");
            Saved=saved;advanced=(double[])sharedAdvanced.Clone();this.layers=(string[])layers.Clone();
            var p=saved==null?null:saved.Parameters;
            var numbers=p==null?new[]{initialSpecifications[0],initialSpecifications[1],initialSpecifications[2],initialSpecifications[3],0.0,0}:
                new[]{p.LineDiameter,p.LineSpacing,p.DotDiameter,p.DotSpacing,p.Length,p.Vertical};
            InitialText=Array.ConvertAll(numbers,v=>v>0?v.ToString("g",CultureInfo.CurrentCulture):"");
        }
        internal BeamSideDrawRequest Create(string[] text,BeamSideDirection direction,bool full,bool topUp,bool bottomUp,
            bool annotationRight,double scale,int unitCode,bool mmOverride)
        {
            if(text==null||text.Length!=6) throw new InvalidOperationException("参数项数无效。");
            var v=new double[6];
            for(int i=0;i<6;i++)
                if(!double.TryParse(text[i],NumberStyles.Float,CultureInfo.CurrentCulture,out v[i])||double.IsNaN(v[i])||double.IsInfinity(v[i])||v[i]<=0)
                    throw new InvalidOperationException("请填写有效正数毫米值；L、V 首次必须明确输入。");
            var p=new BeamSideRebarParameters(v[4],v[5],advanced[0],v[0],v[1],v[2],v[3],advanced[1],advanced[2],advanced[3],topUp,bottomUp,annotationRight,advanced[4]);
            return new BeamSideDrawRequest(direction,full,p,scale,unitCode,mmOverride,layers);
        }
    }
}
