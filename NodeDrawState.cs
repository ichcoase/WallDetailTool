using System;
using System.Globalization;

namespace WallDetailTool
{
    // Window-local text, including blanks/invalid edits, is deliberately not a
    // saved design parameter. Each opening starts from successful DWG records.
    internal sealed class NodeDrawState
    {
        private readonly string[][] drafts;
        internal readonly bool IsSlab;
        internal bool Hanging { get; private set; }
        internal NodeDrawState(bool slab,bool hanging,string[] up,string[] down)
        {
            IsSlab=slab; Hanging=hanging;
            int count=slab?8:6;
            if(up.Length!=count || down.Length!=count) throw new ArgumentException("参数项数错误。");
            drafts=new[]{(string[])up.Clone(),(string[])down.Clone()};
            foreach(var d in drafts)for(int i=0;i<count;i++) {
                double v;bool missing=String.IsNullOrWhiteSpace(d[i])||(double.TryParse(d[i],out v)&&v<=0);
                if(missing)d[i]=i==count-1?d[i-1]:(slab?new[]{"8","150","6","200","200","31","300","300"}:new[]{"8","150","6","200","300","300"})[i];
            }
        }
        internal string[] Current { get { return (string[])drafts[Hanging?1:0].Clone(); } }
        internal void Capture(string[] text)
        {
            if(text.Length!=drafts[0].Length) throw new ArgumentException("参数项数错误。");
            drafts[Hanging?1:0]=(string[])text.Clone();
        }
        internal void Switch(bool hanging,string[] text) { Capture(text); Hanging=hanging; }
        internal string NodeName { get { return (IsSlab?"板":"梁")+(Hanging?"下下挂":"上反坎"); } }
        internal string HLabel { get { return Hanging?"板顶至水平筋中心线距离 H (mm)":"板底至水平筋中心线距离 H (mm)"; } }
        internal NodeDrawRequest Request(double[] advanced,bool twoPointRectangle=false,int unitCode=-1)
        {
            string[] text=Current;
            double[] numbers=new double[text.Length];
            for(int i=0;i<text.Length;i++)
            {
                if(i==text.Length-1 && String.IsNullOrWhiteSpace(text[i])) { numbers[i]=numbers[i-1]; continue; }
                if(IsSlab && i==4 && String.IsNullOrWhiteSpace(text[i])) { numbers[i]=0; continue; }
                if(!double.TryParse(text[i],NumberStyles.Float,CultureInfo.CurrentCulture,out numbers[i]) ||
                    double.IsNaN(numbers[i]) || double.IsInfinity(numbers[i]) || numbers[i]<=0)
                    throw new InvalidOperationException("请填写有效的正数毫米参数；右侧留空随左侧，板厚可留空供完整截面识别。");
            }
            // h may be ignored by a full section, so do not reject H against the
            // optional local h here. The existing geometry validator decides it.
            if(twoPointRectangle&&IsSlab&&numbers[4]<=0)throw new InvalidOperationException("两点矩形须在窗口输入板厚h。");
            return new NodeDrawRequest(IsSlab,Hanging,numbers,advanced,twoPointRectangle,unitCode);
        }
    }

    internal sealed class NodeDrawRequest
    {
        internal readonly bool IsSlab,Hanging,TwoPointRectangle;
        internal readonly int UnitCode;
        private readonly double[] values,advanced;
        internal NodeDrawRequest(bool slab,bool hanging,double[] values,double[] advanced,bool twoPointRectangle=false,int unitCode=-1)
        {
            IsSlab=slab; Hanging=hanging;TwoPointRectangle=twoPointRectangle;UnitCode=unitCode;
            this.values=(double[])values.Clone(); this.advanced=(double[])advanced.Clone();
        }
        internal double Value(int i) { return values[i]; }
        internal double Advanced(int i) { return advanced[i]; }
    }

    internal static class SlabDrawRecord
    {
        // Upward records historically had H/Lleft/Lright[/local h]. Downward
        // records already had eight fields. Never interpret one type's key as another.
        internal static double[] Decode(double[] source,bool hanging)
        {
            if(source.Length!=8 && (hanging || (source.Length!=3 && source.Length!=4)))
                throw new InvalidOperationException("板反坎/下挂参数记录格式不支持。");
            var result=new double[source.Length==3?4:source.Length];
            Array.Copy(source,result,source.Length);
            for(int i=0;i<result.Length;i++)
            {
                if(i==3)
                {
                    if(double.IsNaN(result[i]) || double.IsInfinity(result[i]) || result[i]<0) result[i]=0;
                    continue;
                }
                if(double.IsNaN(result[i]) || double.IsInfinity(result[i]) || result[i]<=0)
                    throw new InvalidOperationException("板反坎/下挂参数记录包含无效值。");
            }
            return result;
        }
    }
}
