using System;
using System.Collections.Generic;
using System.Linq;

namespace WallDetailTool
{
    internal enum TopSlabBase { Beam = 1, Slab = 2 }

    // Immutable WCS millimetre geometry. No source entity identifiers or CAD resources.
    internal sealed class TopSlabGeometry
    {
        internal readonly double OuterX, Bottom, Top, WallThickness, SlabThickness, Projection, Elevation;
        internal readonly int Direction;
        internal double Height { get { return Top-Bottom; } }
        internal double InnerX { get { return OuterX+Direction*WallThickness; } }
        internal double FreeX { get { return InnerX+Direction*Projection; } }
        internal TopSlabGeometry(double outerX,double bottom,double top,double wall,double slab,double projection,double z,int direction)
        {
            foreach(double v in new[]{outerX,bottom,top,wall,slab,projection,z})
                if(double.IsNaN(v)||double.IsInfinity(v)) throw new ArgumentException("轮廓坐标必须有限。");
            if(wall<=TopSlabRecognizer.Tolerance||slab<=TopSlabRecognizer.Tolerance||projection<=TopSlabRecognizer.Tolerance||
                top-bottom-slab<=TopSlabRecognizer.Tolerance||(direction!=1&&direction!=-1))
                throw new ArgumentException("反坎、飘板尺寸退化，或方向无效。");
            OuterX=outerX;Bottom=bottom;Top=top;WallThickness=wall;SlabThickness=slab;Projection=projection;Elevation=z;Direction=direction;
        }
        internal ContourPoint World(double x,double y) { return new ContourPoint(OuterX+Direction*x,Bottom+y); }
    }

    internal static class TopSlabRecognizer
    {
        // Numerical tolerance only; do not heal drafting gaps of millimetre size.
        internal const double Tolerance=1e-6;
        private sealed class Edge { internal int A,B; }
        private static void Fail(string reason) { throw new InvalidOperationException(reason+" 请修正或重新选择完整L形边界。"); }
        private static List<double> Axis(IEnumerable<double> values)
        {
            var result=new List<double>();
            foreach(double v in values.OrderBy(x=>x))
            {
                if(double.IsNaN(v)||double.IsInfinity(v)) Fail("轮廓含非有限坐标。");
                if(result.Count==0||v-result[result.Count-1]>Tolerance) result.Add(v);
            }
            return result;
        }
        internal static TopSlabGeometry Recognize(IList<ContourSegment> source,double elevation)
        { return RecognizeInput(source,elevation).Geometry; }
        internal static TopSlabContourInput RecognizeInput(IList<ContourSegment> source,double elevation)
        {
            if(source==null||source.Count<5||source.Count>4096) Fail("边界需5～4096条直线段；仅允许缺少反坎底部封口边。");
            if(double.IsNaN(elevation)||double.IsInfinity(elevation)) Fail("轮廓标高无效。");
            var xs=Axis(source.SelectMany(s=>new[]{s.A.X,s.B.X}));
            var ys=Axis(source.SelectMany(s=>new[]{s.A.Y,s.B.Y}));
            var points=new List<ContourPoint>();var edges=new List<Edge>();
            var nodeIds=new Dictionary<long,int>();
            Func<ContourPoint,int> node=p=>
            {
                int x=xs.FindIndex(v=>Math.Abs(v-p.X)<=Tolerance),y=ys.FindIndex(v=>Math.Abs(v-p.Y)<=Tolerance);
                long key=((long)x<<32)|(uint)y;int id;
                if(!nodeIds.TryGetValue(key,out id)) { id=points.Count;nodeIds.Add(key,id);points.Add(new ContourPoint(xs[x],ys[y])); }
                return id;
            };
            var unique=new HashSet<long>();
            foreach(var s in source)
            {
                int a=node(s.A),b=node(s.B);var p=points[a];var q=points[b];
                if(a==b) Fail("轮廓存在零长度或退化短边。");
                if(p.X!=q.X&&p.Y!=q.Y) Fail("轮廓含非正交边，仅支持WCS水平/竖直边。");
                long key=((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);
                if(!unique.Add(key)) Fail("轮廓含重复边。");
                edges.Add(new Edge { A=a,B=b });
            }
            var adjacent=points.Select(p=>new List<int>()).ToArray();
            for(int i=0;i<edges.Count;i++) { adjacent[edges[i].A].Add(i);adjacent[edges[i].B].Add(i); }
            if(adjacent.Any(a=>a.Count>2)) Fail("轮廓存在分叉或多余边。");
            var ends=Enumerable.Range(0,points.Count).Where(i=>adjacent[i].Count==1).ToArray();
            bool virtualBottom=ends.Length!=0;
            if(virtualBottom)
            {
                if(ends.Length!=2) Fail("开放边界必须是一条连续链，且恰好两个底部端点；不能有多个缺口。");
                var a=points[ends[0]];var b=points[ends[1]];
                if(a.Y!=b.Y) Fail("开放边界两个底部端点不等高，不能自动延伸侧边。");
                if(a.X==b.X||a.Y!=points.Min(p=>p.Y)) Fail("开放端点必须位于反坎两侧的最低位置，且X不同；仅允许缺底边。");
                foreach(int end in ends)
                {
                    var edge=edges[adjacent[end][0]];var other=points[edge.A==end?edge.B:edge.A];
                    if(other.X!=points[end].X) Fail("底部开放端点的相邻实际边必须是反坎竖直侧边。");
                }
                int index=edges.Count;
                edges.Add(new Edge { A=ends[0],B=ends[1] });
                adjacent[ends[0]].Add(index);adjacent[ends[1]].Add(index);
            }
            var used=new HashSet<int>();var path=new List<ContourPoint>();int current=0;
            do
            {
                path.Add(points[current]);int next=adjacent[current].FirstOrDefault(i=>!used.Contains(i));
                if(used.Contains(next)) Fail("轮廓闭合路径无效。");
                used.Add(next);var e=edges[next];current=e.A==current?e.B:e.A;
            } while(current!=0&&used.Count<=edges.Count);
            if(used.Count!=edges.Count) Fail("选择包含多个闭环或断开的多余对象。");
            // Remove only forward collinear subdivisions, never reverse/overlapping edges.
            bool changed=true;
            while(changed&&path.Count>=3)
            {
                changed=false;
                for(int i=0;i<path.Count;i++)
                {
                    var a=path[(i+path.Count-1)%path.Count];var b=path[i];var c=path[(i+1)%path.Count];
                    if((a.X==b.X&&b.X==c.X)||(a.Y==b.Y&&b.Y==c.Y))
                    {
                        if((b.X-a.X)*(c.X-b.X)+(b.Y-a.Y)*(c.Y-b.Y)<=0) Fail("边界含折返或重叠边。");
                        path.RemoveAt(i);changed=true;break;
                    }
                }
            }
            if(path.Count!=6) Fail("轮廓不是标准六角点直角L形。");
            double left=path.Min(p=>p.X),right=path.Max(p=>p.X),bottom=path.Min(p=>p.Y),top=path.Max(p=>p.Y);
            var lower=path.Where(p=>p.Y==bottom).OrderBy(p=>p.X).ToArray();
            if(lower.Length!=2) Fail("底部必须是一条反坎封口边。");
            int direction=lower[0].X==left&&lower[1].X<right?1:lower[1].X==right&&lower[0].X>left?-1:0;
            if(direction==0) Fail("飘板必须连接反坎顶部，且仅向一侧伸出。");
            double outer=direction==1?left:right,inner=direction==1?lower[1].X:lower[0].X,free=direction==1?right:left;
            var middle=path.Where(p=>p.Y!=bottom&&p.Y!=top).ToArray();
            if(middle.Length!=2||middle[0].Y!=middle[1].Y) Fail("飘板上下表面必须水平且等厚。");
            double underside=middle[0].Y;
            var expected=new[]{new ContourPoint(outer,bottom),new ContourPoint(inner,bottom),new ContourPoint(inner,underside),
                new ContourPoint(free,underside),new ContourPoint(free,top),new ContourPoint(outer,top)};
            bool match=false;
            for(int start=0;start<6;start++) foreach(int sign in new[]{1,-1})
            {
                bool ok=true;
                for(int i=0;i<6;i++) { var a=path[(start+sign*i+12)%6];var b=expected[i];if(a.X!=b.X||a.Y!=b.Y) ok=false; }
                match|=ok;
            }
            if(!match) Fail("边界自交、错接或不是顶部连接的标准L形。");
            var geometry=new TopSlabGeometry(outer,bottom,top,Math.Abs(inner-outer),top-underside,Math.Abs(free-inner),elevation,direction);
            return new TopSlabContourInput(geometry,virtualBottom);
        }
    }
}
