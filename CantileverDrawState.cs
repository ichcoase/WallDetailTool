using System;
using System.Globalization;

namespace WallDetailTool
{
    internal sealed class CantileverDrawRequest
    {
        internal readonly CantileverDirection Direction;
        internal readonly CantileverSupportMode Mode;
        internal readonly CantileverSpecifications Specifications;
        internal readonly double Scale;
        internal readonly int UnitCode;
        internal readonly bool OverrideMillimetres;
        private readonly double[] values,advanced;
        private readonly string[] layers;
        internal CantileverDrawRequest(CantileverDirection direction,CantileverSupportMode mode,double[] values,
            double[] advanced,string[] layers,double scale,int unitCode,bool mmOverride)
        {
            new CantileverFrame(direction,0,0,1);
            if(values==null||values.Length!=15||advanced==null||advanced.Length!=5||layers==null||layers.Length!=4)
                throw new ArgumentException("悬挑板参数不完整。");
            this.values=(double[])values.Clone();this.advanced=(double[])advanced.Clone();this.layers=(string[])layers.Clone();
            foreach(double v in this.values) if(CantileverNumbers.Finite(v)<0) throw new ArgumentException("参数不能为负。");
            foreach(double v in this.advanced) if(CantileverNumbers.Finite(v)<0) throw new ArgumentException("高级参数不能为负。");
            for(int i=1;i<5;i++) CantileverNumbers.Positive(this.advanced[i]);
            if(this.advanced[1]>1000||this.advanced[2]>1000) throw new ArgumentException("显示尺寸不得超过1000mm。");
            foreach(string name in this.layers)
                if(String.IsNullOrWhiteSpace(name)||name.IndexOfAny(new[]{'<','>','/','\\','"',':',';','?','*','|',','})>=0)
                    throw new ArgumentException("图层名称无效。");
            foreach(int i in new[]{0,4,5,7,8,9,10,13,14}) CantileverNumbers.Positive(this.values[i]);
            if((int)mode<3) { CantileverNumbers.Positive(this.values[1]);CantileverNumbers.Positive(this.values[2]); }
            else CantileverNumbers.Positive(this.values[3]);
            if(mode==CantileverSupportMode.InnerWithAdditional) CantileverNumbers.Positive(this.values[6]);
            Specifications=new CantileverSpecifications(mode,
                new CantileverSpecification((int)mode>=3?CantileverBarRole.InnerExtension:CantileverBarRole.Top,values[7],values[8]),
                new CantileverSpecification(CantileverBarRole.Bottom,values[9],values[10]),
                mode==CantileverSupportMode.InnerWithAdditional?new CantileverSpecification(CantileverBarRole.Additional,values[11],values[12]):null);
            Scale=CantileverNumbers.Positive(scale);
            if(unitCode<0||unitCode>32767||(mmOverride&&scale!=1)) throw new ArgumentException("单位解释无效。");
            Direction=direction;Mode=mode;UnitCode=unitCode;OverrideMillimetres=mmOverride;
        }
        internal double Value(int i) { return values[i]; }
        internal double Advanced(int i) { return advanced[i]; }
        internal string Layer(int i) { return layers[i]; }
        internal CantileverPathInputs Inputs(double length,double thickness)
        { return new CantileverPathInputs(length,thickness,advanced[0],advanced[3],advanced[1],values[1],values[2],values[3],values[4],values[5],values[6]); }
        internal static bool TargetMatches(object target,object active,int expectedUnit,int currentUnit)
        { return target!=null&&ReferenceEquals(target,active)&&expectedUnit==currentUnit; }
    }
    internal static class CantileverDrawRecord
    {
        internal const string LastKey="WDT_CANTILEVER_LAST",BlockKey="WDT_CANTILEVER_PARAMS";
        internal const int Count=26;
        internal static double[] Encode(CantileverDrawRequest r)
        {
            var v=new double[Count];v[0]=1;v[1]=(int)r.Direction;v[2]=(int)r.Mode;
            for(int i=0;i<15;i++) v[3+i]=r.Value(i);
            for(int i=0;i<5;i++) v[18+i]=r.Advanced(i);
            v[23]=r.Scale;v[24]=r.UnitCode;v[25]=r.OverrideMillimetres?1:0;return v;
        }
        internal static CantileverDrawRequest Decode(double[] source,string[] layers)
        {
            if(source==null||source.Length!=Count) throw new ArgumentException("悬挑板记录项数无效。");
            var v=(double[])source.Clone();foreach(double n in v) CantileverNumbers.Finite(n);
            if(v[0]!=1||(v[1]!=1&&v[1]!=-1)||v[2]!=Math.Floor(v[2])||v[2]<1||v[2]>4||
                v[24]!=Math.Floor(v[24])||(v[25]!=0&&v[25]!=1)) throw new ArgumentException("悬挑板记录版本或开关无效。");
            var values=new double[15];var advanced=new double[5];Array.Copy(v,3,values,0,15);Array.Copy(v,18,advanced,0,5);
            if(v[24]<0||v[24]>32767) throw new ArgumentException("图纸单位代码无效。");
            return new CantileverDrawRequest((CantileverDirection)(int)v[1],(CantileverSupportMode)(int)v[2],values,advanced,layers,v[23],(int)v[24],v[25]==1);
        }
        internal static double[] EncodeSnapshot(CantileverDrawRequest r,CantileverFrame frame,CantileverPathInputs p,double elevation)
        {
            if(frame.Direction!=r.Direction||frame.BeamWidth!=r.Value(0)) throw new ArgumentException("快照方向或梁宽不一致。");
            var values=new double[31];Array.Copy(Encode(r),values,Count);
            values[26]=frame.SupportX;values[27]=frame.TopY;values[28]=p.Length;values[29]=p.Thickness;values[30]=CantileverNumbers.Finite(elevation);return values;
        }
        internal static CantileverDrawRequest DecodeSnapshot(double[] source,string[] layers,out CantileverFrame frame,out CantileverPathInputs p,out double elevation)
        {
            if(source==null||source.Length!=31) throw new ArgumentException("悬挑板块快照不完整。");
            var values=new double[Count];Array.Copy(source,values,Count);var request=Decode(values,layers);
            frame=new CantileverFrame(request.Direction,source[26],source[27],request.Value(0));
            p=request.Inputs(source[28],source[29]);elevation=CantileverNumbers.Finite(source[30]);return request;
        }
    }
    internal sealed class CantileverDrawState
    {
        internal readonly string[] Initial;
        private readonly double[] advanced;
        private readonly string[] layers;
        internal readonly CantileverDrawRequest Saved;
        internal CantileverDrawState(CantileverDrawRequest saved,double[] shared,string[] layers,double[] specs)
        {
            Saved=saved;advanced=(double[])shared.Clone();this.layers=(string[])layers.Clone();Initial=new string[15];
            for(int i=0;i<15;i++) Initial[i]=saved==null?"":saved.Value(i)>0?saved.Value(i).ToString("g",CultureInfo.CurrentCulture):"";
            if(saved==null) for(int i=7;i<15;i++) Initial[i]=specs[(i-7)%2+(i>=13?2:0)].ToString("g",CultureInfo.CurrentCulture);
        }
        internal CantileverDrawRequest Create(string[] text,CantileverDirection direction,CantileverSupportMode mode,double scale,int units,bool mmOverride)
        {
            if(text==null||text.Length!=15) throw new ArgumentException("参数项数错误。");
            var values=new double[15];
            for(int i=0;i<15;i++)
            {
                bool used=!(i==1||i==2)?true:(int)mode<3;
                if(i==3) used=(int)mode>=3;
                if(i==6||i==11||i==12) used=mode==CantileverSupportMode.InnerWithAdditional;
                if(!used) continue;
                if(!double.TryParse(text[i],NumberStyles.Float,CultureInfo.CurrentCulture,out values[i])||
                    double.IsNaN(values[i])||double.IsInfinity(values[i])||values[i]<=0)
                    throw new ArgumentException("请填写所有启用项的有效正数毫米值；控制长度首次必须明确输入。");
            }
            return new CantileverDrawRequest(direction,mode,values,advanced,layers,scale,units,mmOverride);
        }
    }
}
