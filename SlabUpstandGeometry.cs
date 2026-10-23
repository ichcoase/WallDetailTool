using System;
using System.Collections.Generic;
using System.Linq;

namespace WallDetailTool
{
    internal enum SlabCandidateState { SingleCandidate, AwaitingSelection, SelectedAndRevalidated }

    internal sealed class SlabUpstandGeometry
    {
        internal double Left, Right, Top, SlabTop, SlabBottom, Elevation;
        internal double? SlabLeft, SlabRight;
        internal UpstandInputKind InputKind;
        internal bool ThicknessIsManual;
        internal SlabCandidateState CandidateState;
        internal double Width { get { return Right-Left; } }
        internal double Height { get { return Top-SlabTop; } }
        internal double SlabThickness { get { return SlabTop-SlabBottom; } }
        internal bool HasSlabBoundaries { get { return SlabLeft.HasValue && SlabRight.HasValue; } }
    }

    internal static class SlabUpstandRecognizer
    {
        private const double T = UpstandRecognizer.Tolerance;

        internal static List<SlabUpstandGeometry> Recognize(IList<ContourSegment> source, double elevation, double? manualThickness)
        {
            List<UpstandGeometry> bases=UpstandRecognizer.Recognize(source,elevation);
            if (bases[0].InputKind!=UpstandInputKind.FullSection)
            {
                if (!manualThickness.HasValue || double.IsNaN(manualThickness.Value) ||
                    double.IsInfinity(manualThickness.Value) || manualThickness.Value<=0)
                    throw new InvalidOperationException("局部轮廓需要输入正数楼板厚度。");
                return bases.Select(b=>new SlabUpstandGeometry { Left=b.Left,Right=b.Right,Top=b.Top,
                    SlabTop=b.BeamTop,SlabBottom=b.BeamTop-manualThickness.Value,Elevation=elevation,
                    InputKind=b.InputKind,ThicknessIsManual=true,CandidateState=SlabCandidateState.SingleCandidate }).ToList();
            }
            // The V0.1 recognizer finds protrusions; it does not validate the slab below them.
            double slabTop=bases[0].BeamTop;
            if (bases.Any(b=>Math.Abs(b.BeamTop-slabTop)>T))
                throw new InvalidOperationException("完整截面含不同板顶标高，不能唯一识别等厚楼板。");
            double left=source.Min(s=>Math.Min(s.A.X,s.B.X));
            double right=source.Max(s=>Math.Max(s.A.X,s.B.X));
            double bottom=source.Min(s=>Math.Min(s.A.Y,s.B.Y));
            if (right-left<=T || slabTop-bottom<=T)
                throw new InvalidOperationException("完整截面楼板宽度或厚度无效。");
            foreach (ContourSegment s in source)
            {
                double low=Math.Min(s.A.Y,s.B.Y), high=Math.Max(s.A.Y,s.B.Y);
                if (low>=slabTop-T)
                {
                    if (high<=slabTop+T) continue;
                    bool belongs=false;
                    foreach (UpstandGeometry b in bases)
                    {
                        bool topEdge=Math.Abs(s.A.Y-b.Top)<=T && Math.Abs(s.B.Y-b.Top)<=T &&
                            Math.Min(s.A.X,s.B.X)>=b.Left-T && Math.Max(s.A.X,s.B.X)<=b.Right+T;
                        bool sideEdge=(Math.Abs(s.A.X-b.Left)<=T && Math.Abs(s.B.X-b.Left)<=T ||
                            Math.Abs(s.A.X-b.Right)<=T && Math.Abs(s.B.X-b.Right)<=T) &&
                            low>=slabTop-T && high<=b.Top+T;
                        if (topEdge || sideEdge) { belongs=true; break; }
                    }
                    if (!belongs) throw new InvalidOperationException("完整截面含非矩形反坎或复杂板顶。");
                    continue;
                }
                bool horizontal=Math.Abs(s.A.Y-s.B.Y)<=T;
                if (horizontal)
                {
                    if (Math.Abs(s.A.Y-bottom)>T)
                        throw new InvalidOperationException("不支持降板或变厚楼板。");
                }
                else if (Math.Abs(s.A.X-left)>T && Math.Abs(s.A.X-right)>T)
                    throw new InvalidOperationException("板底以下存在内部台阶或复杂截面。");
                if (high>slabTop+T && (Math.Abs(s.A.X-left)<=T || Math.Abs(s.A.X-right)<=T))
                    throw new InvalidOperationException("楼板外边界超过板顶，截面不规则。");
            }
            if (!Covered(source,bottom,left,right,true) ||
                !Covered(source,left,bottom,slabTop,false) ||
                !Covered(source,right,bottom,slabTop,false))
                throw new InvalidOperationException("完整截面缺少连续板底或左右边界。");
            return bases.Select(b=>new SlabUpstandGeometry { Left=b.Left,Right=b.Right,Top=b.Top,
                SlabTop=slabTop,SlabBottom=bottom,SlabLeft=left,SlabRight=right,
                Elevation=elevation,InputKind=UpstandInputKind.FullSection,
                CandidateState=bases.Count==1?SlabCandidateState.SingleCandidate:SlabCandidateState.AwaitingSelection }).ToList();
        }

        private static bool Covered(IList<ContourSegment> segments,double fixedCoordinate,double start,double end,bool horizontal)
        {
            var intervals=new List<Tuple<double,double>>();
            foreach (ContourSegment s in segments)
            {
                double fixedA=horizontal?s.A.Y:s.A.X, fixedB=horizontal?s.B.Y:s.B.X;
                if (Math.Abs(fixedA-fixedCoordinate)>T || Math.Abs(fixedB-fixedCoordinate)>T) continue;
                double a=horizontal?s.A.X:s.A.Y, b=horizontal?s.B.X:s.B.Y;
                intervals.Add(Tuple.Create(Math.Min(a,b),Math.Max(a,b)));
            }
            intervals.Sort((a,b)=>a.Item1.CompareTo(b.Item1));
            double reached=start;
            foreach (var part in intervals)
            {
                if (part.Item1>reached+T) return false;
                reached=Math.Max(reached,part.Item2);
                if (reached>=end-T) return true;
            }
            return false;
        }

        internal static SlabUpstandGeometry SelectCandidate(IList<SlabUpstandGeometry> candidates,double x,double y)
        {
            SlabUpstandGeometry found=null;
            foreach (var c in candidates)
            {
                if (x<=c.Left+T || x>=c.Right-T || y<=c.SlabTop+T || y>=c.Top-T) continue;
                if (found!=null) throw new InvalidOperationException("点选位置覆盖多个候选，请重选。");
                found=c;
            }
            if (found==null) throw new InvalidOperationException("点选位置不在有效反坎内部。");
            return found;
        }
    }
}
