using System;
using System.Collections.Generic;
using System.Linq;

namespace WallDetailTool
{
    // Read-only LINE/LWPOLYLINE segment input. Curves must be rejected by the CAD
    // extractor before supplying segments; no source identifiers are retained.
    internal static class BeamSideContourRecognizer
    {
        internal const double Tolerance=1.0;
        private const double E=BeamSideMath.Epsilon;
        internal static BeamSideRecognition Recognize(IList<ContourSegment> source,double elevation,
            BeamSideDirection direction,bool fullSection)
        {
            BeamSideMath.Finite(elevation);
            if(source==null || source.Count<3 || source.Count>4096)
                throw new InvalidOperationException("轮廓线段数无效（3～4096）。");
            if(direction!=BeamSideDirection.Left && direction!=BeamSideDirection.Right)
                throw new InvalidOperationException("块体方向无效。");
            var copy=new List<ContourSegment>();
            foreach(var s in source)
            {
                BeamSideMath.Finite(s.A.X,s.A.Y,s.B.X,s.B.Y);
                double dx=Math.Abs(s.A.X-s.B.X),dy=Math.Abs(s.A.Y-s.B.Y);
                if((dx<=Tolerance && dy<=Tolerance) || (dx>Tolerance && dy>Tolerance))
                    throw new InvalidOperationException("轮廓含短边、零长度或斜边；仅支持 WCS 正交边。");
                copy.Add(s);
            }
            return fullSection ? Full(copy,elevation,direction) : Local(copy,elevation,direction);
        }
        private static BeamSideRecognition Local(List<ContourSegment> source,double z,BeamSideDirection direction)
        {
            var remaining=new HashSet<int>(Enumerable.Range(0,source.Count));
            var result=new List<BeamSideGeometry>();
            while(remaining.Count>0)
            {
                var indices=new List<int>{remaining.Min()}; remaining.Remove(indices[0]);
                for(int i=0;i<indices.Count;i++)
                    foreach(int j in remaining.ToArray())
                        if(Touch(source[indices[i]],source[j])) { indices.Add(j); remaining.Remove(j); }
                var rotated=new List<ContourSegment>();
                foreach(int i in indices)
                {
                    var s=source[i];
                    rotated.Add(new ContourSegment(new ContourPoint(s.A.Y,(int)direction*s.A.X),
                        new ContourPoint(s.B.Y,(int)direction*s.B.X)));
                }
                foreach(var c in UpstandRecognizer.Recognize(rotated,z))
                {
                    if(c.InputKind==UpstandInputKind.FullSection)
                        throw new InvalidOperationException("选中的是完整截面，请使用完整截面识别模式。");
                    double x0=(int)direction*c.BeamTop,x1=(int)direction*c.Top;
                    result.Add(new BeamSideGeometry(new BeamSideRect(Math.Min(x0,x1),Math.Max(x0,x1),c.Left,c.Right),
                        null,direction,c.InputKind==UpstandInputKind.ClosedRectangle?BeamSideInputKind.LocalRectangle:BeamSideInputKind.LocalOpenU,z));
                }
            }
            result=result.OrderBy(c=>c.Block.Left).ThenBy(c=>c.Block.Bottom).ToList();
            // Separate candidate contours may not cross, touch or overlap.
            for(int i=0;i<result.Count;i++) for(int j=0;j<i;j++)
                if(Math.Min(result[i].Block.Right,result[j].Block.Right)>=Math.Max(result[i].Block.Left,result[j].Block.Left)-E &&
                    Math.Min(result[i].Block.Top,result[j].Block.Top)>=Math.Max(result[i].Block.Bottom,result[j].Block.Bottom)-E)
                    throw new InvalidOperationException("局部候选重叠或接触，请只选有效轮廓。");
            return new BeamSideRecognition(result,new List<BeamSideSplitRegion>());
        }
        private static bool Touch(ContourSegment a,ContourSegment b)
        { return BeamSideMath.Distance(a.A,b.A)<=Tolerance || BeamSideMath.Distance(a.A,b.B)<=Tolerance ||
            BeamSideMath.Distance(a.B,b.A)<=Tolerance || BeamSideMath.Distance(a.B,b.B)<=Tolerance; }

        private sealed class Axis
        {
            internal readonly List<double> Values=new List<double>();
            private readonly Dictionary<double,int> indices=new Dictionary<double,int>();
            internal Axis(IEnumerable<double> source)
            {
                var sorted=source.Distinct().OrderBy(v=>v).ToList();
                for(int i=0;i<sorted.Count;)
                {
                    int end=i+1; while(end<sorted.Count && sorted[end]-sorted[i]<=Tolerance) end++;
                    double representative=0; for(int j=i;j<end;j++) representative+=sorted[j]/(end-i);
                    int index=Values.Count; Values.Add(representative);
                    for(int j=i;j<end;j++) indices.Add(sorted[j],index);
                    i=end;
                }
                if(Values.Count>256) throw new InvalidOperationException("轮廓坐标过多，请简化为规则截面。");
            }
            internal int At(double v) { return indices[v]; }
        }
        private static long Key(bool vertical,int x,int y) { return (vertical?1L<<40:0) | ((long)x<<20) | (uint)y; }
        private sealed class Run { internal int Left,Right,Bottom,Top; }
        private static BeamSideRecognition Full(List<ContourSegment> input,double z,BeamSideDirection direction)
        {
            // Normalized U grows towards the block. V is still world Y.
            var source=input.Select(s=>new ContourSegment(new ContourPoint((int)direction*s.A.X,s.A.Y),
                new ContourPoint((int)direction*s.B.X,s.B.Y))).ToList();
            var xs=new Axis(source.SelectMany(s=>new[]{s.A.X,s.B.X}));
            var ys=new Axis(source.SelectMany(s=>new[]{s.A.Y,s.B.Y}));
            int nx=xs.Values.Count,ny=ys.Values.Count;
            if(nx<2 || ny<2) throw new InvalidOperationException("完整截面不能围成有效区域。");
            var edges=new Dictionary<long,int>();
            var graph=new Dictionary<int,HashSet<int>>();
            Action<bool,int,int> add=(vertical,x,y)=>
            {
                long key=Key(vertical,x,y); int count;
                edges.TryGetValue(key,out count); edges[key]=count+1;
                if(count>=2) throw new InvalidOperationException("轮廓重复超过两次。");
                int a=x*ny+y,b=vertical?a+1:a+ny;
                if(!graph.ContainsKey(a)) graph[a]=new HashSet<int>();
                if(!graph.ContainsKey(b)) graph[b]=new HashSet<int>();
                graph[a].Add(b); graph[b].Add(a);
            };
            foreach(var s in source)
            {
                int ax=xs.At(s.A.X),bx=xs.At(s.B.X),ay=ys.At(s.A.Y),by=ys.At(s.B.Y);
                if(ax==bx && ay!=by) for(int y=Math.Min(ay,by);y<Math.Max(ay,by);y++) add(true,ax,y);
                else if(ay==by && ax!=bx) for(int x=Math.Min(ax,bx);x<Math.Max(ax,bx);x++) add(false,x,ay);
                else throw new InvalidOperationException("轮廓在 1 mm 容差下方向不明确，请修正短边或端点。");
            }
            if(graph.Values.Any(v=>v.Count<2)) throw new InvalidOperationException("完整截面缺边或有悬空线。");
            var visited=new HashSet<int>(); var queue=new Queue<int>(); queue.Enqueue(graph.Keys.First());
            while(queue.Count>0) { int id=queue.Dequeue(); if(!visited.Add(id)) continue; foreach(int n in graph[id]) queue.Enqueue(n); }
            if(visited.Count!=graph.Count) throw new InvalidOperationException("完整梁与块体不连通；请只选择一个相连的完整截面。");

            // Flood the outside on an expanded rectangular cell grid. Internal
            // seams divide faces but do not alter the enclosing concrete union.
            var outside=new bool[nx+1,ny+1]; var cells=new Queue<int>(); cells.Enqueue(0); outside[0,0]=true;
            while(cells.Count>0)
            {
                int id=cells.Dequeue(),i=id/(ny+1),j=id%(ny+1);
                for(int d=0;d<4;d++)
                {
                    int ii=i+(d==0?1:d==1?-1:0),jj=j+(d==2?1:d==3?-1:0);
                    if(ii<0||ii>nx||jj<0||jj>ny||outside[ii,jj]) continue;
                    bool wall=d==0?edges.ContainsKey(Key(true,i,j-1)):
                        d==1?edges.ContainsKey(Key(true,i-1,j-1)):
                        d==2?edges.ContainsKey(Key(false,i-1,j)):edges.ContainsKey(Key(false,i-1,j-1));
                    if(!wall) { outside[ii,jj]=true; cells.Enqueue(ii*(ny+1)+jj); }
                }
            }
            Func<int,int,bool> inside=(x,y)=>x>=0&&x<nx-1&&y>=0&&y<ny-1&&!outside[x+1,y+1];
            var runs=new List<Run>();
            for(int x=0;x<nx-1;x++)
            {
                var rows=Enumerable.Range(0,ny-1).Where(y=>inside(x,y)).ToArray();
                if(rows.Length==0 || rows[rows.Length-1]-rows[0]+1!=rows.Length)
                    throw new InvalidOperationException("完整截面含空洞、断开区域或非规则梁/块体。");
                int b=rows[0],t=rows[rows.Length-1]+1;
                if(runs.Count>0 && runs[runs.Count-1].Bottom==b && runs[runs.Count-1].Top==t)
                    runs[runs.Count-1].Right=x+1;
                else runs.Add(new Run { Left=x,Right=x+1,Bottom=b,Top=t });
            }
            if(runs.Count>2) throw new InvalidOperationException("完整截面不是一个规则矩形梁与一个矩形块体的组合。");
            var interior=new List<long>(); var seamXs=new HashSet<int>();
            foreach(var entry in edges)
            {
                bool v=(entry.Key&(1L<<40))!=0; int x=(int)((entry.Key>>20)&0xfffff),y=(int)(entry.Key&0xfffff);
                bool a=v?inside(x-1,y):inside(x,y-1),b=inside(x,y);
                if(!a&&!b) throw new InvalidOperationException("选区含混凝土外部多余线段。");
                if(a&&b)
                {
                    if(!v) throw new InvalidOperationException("完整截面含不支持的内部水平分支。");
                    interior.Add(entry.Key); seamXs.Add(x);
                }
                else if(entry.Value!=1) throw new InvalidOperationException("外轮廓含重复或重叠边。");
            }
            if(seamXs.Count>1) throw new InvalidOperationException("完整截面含多个内部连接面，请拆分选择。");
            int split=runs.Count==2?runs[0].Right:(seamXs.Count==1?seamXs.First():-1);
            if(seamXs.Count==1 && seamXs.First()!=split) throw new InvalidOperationException("内部连接缝与外轮廓的分界不一致。");
            var candidates=new List<BeamSideGeometry>(); var selections=new List<BeamSideSplitRegion>();
            if(split<0)
            {
                var outline=WorldRect(xs.Values[0],xs.Values[nx-1],ys.Values[0],ys.Values[ny-1],direction);
                selections.Add(new BeamSideSplitRegion(outline,direction,z));
            }
            else
            {
                Run beam=runs[0],block=runs[runs.Count-1];
                int cb=Math.Max(beam.Bottom,block.Bottom),ct=Math.Min(beam.Top,block.Top);
                if(ct<=cb || ys.Values[ct]-ys.Values[cb]<=Tolerance)
                    throw new InvalidOperationException("梁与块体没有大于 1 mm 的有效连接范围。");
                if(interior.Count>0)
                    for(int y=cb;y<ct;y++) if(!edges.ContainsKey(Key(true,split,y)))
                        throw new InvalidOperationException("内部连接面缺边，不能确认完整梁边界。");
                if(interior.Count!=0 && interior.Count!=ct-cb)
                    throw new InvalidOperationException("内部连接面超出梁与块体的交接范围。");
                var br=WorldRect(xs.Values[0],xs.Values[split],ys.Values[beam.Bottom],ys.Values[beam.Top],direction);
                var cr=WorldRect(xs.Values[split],xs.Values[nx-1],ys.Values[block.Bottom],ys.Values[block.Top],direction);
                if(br.Width<=Tolerance||cr.Width<=Tolerance||br.Height<=Tolerance||cr.Height<=Tolerance)
                    throw new InvalidOperationException("梁或块体尺寸不大于 1 mm，请修正轮廓。");
                candidates.Add(new BeamSideGeometry(cr,br,direction,BeamSideInputKind.FullSection,z));
            }
            return new BeamSideRecognition(candidates,selections);
        }
        private static BeamSideRect WorldRect(double left,double right,double bottom,double top,BeamSideDirection direction)
        { return direction==BeamSideDirection.Right?new BeamSideRect(left,right,bottom,top):new BeamSideRect(-right,-left,bottom,top); }
    }
}
