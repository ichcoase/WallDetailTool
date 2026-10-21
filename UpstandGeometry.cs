using System;
using System.Collections.Generic;
using System.Linq;

namespace WallDetailTool
{
    internal enum UpstandInputKind { ClosedRectangle, OpenInvertedU, FullSection }

    // Millimetres in WCS: V0.1 uses exactly one drawing unit per millimetre.
    internal sealed class UpstandGeometry
    {
        internal double Left, Right, Top, BeamTop, Elevation;
        internal double Width { get { return Right - Left; } }
        internal double Height { get { return Top - BeamTop; } }
        internal double BaseX { get { return Left; } }
        internal double BaseY { get { return BeamTop; } }
        internal UpstandInputKind InputKind;
    }

    internal struct ContourPoint
    {
        internal double X, Y;
        internal ContourPoint(double x, double y) { X=x; Y=y; }
    }
    internal struct ContourSegment
    {
        internal ContourPoint A, B;
        internal ContourSegment(ContourPoint a, ContourPoint b) { A=a; B=b; }
    }

    internal static class UpstandRecognizer
    {
        internal const double Tolerance = 1.0;
        private const double Tiny = 1e-8;
        private sealed class Edge { internal int A, B; internal bool Horizontal; }
        internal static UpstandGeometry SelectCandidate(IList<UpstandGeometry> candidates,double x,double y)
        {
            UpstandGeometry found=null;
            foreach (UpstandGeometry c in candidates)
            {
                if (x<=c.Left+Tolerance || x>=c.Right-Tolerance || y<=c.BeamTop+Tolerance || y>=c.Top-Tolerance) continue;
                if (found!=null) throw new InvalidOperationException("点选位置覆盖多个反坎候选，请重选轮廓。");
                found=c;
            }
            if (found==null) throw new InvalidOperationException("点选位置不在有效反坎内部，请重选轮廓。");
            return found;
        }
        internal static List<UpstandGeometry> Recognize(IList<ContourSegment> source, double elevation)
        {
            if (source == null || source.Count < 3) throw new InvalidOperationException("轮廓缺边：至少需要顶部和两侧三段。请重选。");
            var nodes = new List<ContourPoint>();
            var edges = new List<Edge>();
            foreach (ContourSegment s in source)
            {
                double dx=Math.Abs(s.A.X-s.B.X), dy=Math.Abs(s.A.Y-s.B.Y);
                if (Math.Sqrt(dx*dx+dy*dy) <= Tiny) throw new InvalidOperationException("轮廓含零长度线段，请修正后重选。");
                if (dx <= Tolerance && dy <= Tolerance) throw new InvalidOperationException("轮廓含小于识别容差的短边，无法确定方向，请重选。");
                bool horizontal=dy <= Tolerance && dx > Tolerance;
                bool vertical=dx <= Tolerance && dy > Tolerance;
                if (!horizontal && !vertical) throw new InvalidOperationException("轮廓含斜线或旋转边；只支持 WCS 水平、竖直边。请重选。");
                int a=Node(nodes,s.A), b=Node(nodes,s.B);
                if (a==b) throw new InvalidOperationException("轮廓含零长度或重复端点，请重选。");
                edges.Add(new Edge { A=a, B=b, Horizontal=horizontal });
            }
            for (int i=0;i<edges.Count;i++) for (int j=0;j<i;j++)
            {
                Edge a=edges[i], b=edges[j];
                if ((a.A==b.A && a.B==b.B) || (a.A==b.B && a.B==b.A))
                    throw new InvalidOperationException("轮廓含重复线段，请重选。");
                if (Overlap(nodes,a,b)) throw new InvalidOperationException("轮廓含重叠线段，请重选。");
                if (Cross(nodes,a,b)) throw new InvalidOperationException("轮廓自交或存在未拆分的交叉/分支，请重选。");
            }
            var adjacency=new List<List<int>>();
            for (int i=0;i<nodes.Count;i++) adjacency.Add(new List<int>());
            for (int i=0;i<edges.Count;i++) { adjacency[edges[i].A].Add(i); adjacency[edges[i].B].Add(i); }
            var endpoints=new List<int>();
            for (int i=0;i<adjacency.Count;i++)
            {
                if (adjacency[i].Count==1) endpoints.Add(i);
                else if (adjacency[i].Count!=2) throw new InvalidOperationException("轮廓存在分支、多条拼接路径或断点，请重选。");
            }
            if (endpoints.Count!=0 && endpoints.Count!=2) throw new InvalidOperationException("轮廓不连续或缺边，请重选。");
            int first=endpoints.Count==2 ? endpoints[0] : 0;
            var path=new List<int> { first }; var used=new HashSet<int>();
            int current=first;
            while (used.Count<edges.Count)
            {
                int nextEdge=-1;
                foreach (int e in adjacency[current]) if (!used.Contains(e)) { nextEdge=e; break; }
                if (nextEdge<0) throw new InvalidOperationException("选区包含断开的多条轮廓或多余图元，请重选。");
                used.Add(nextEdge); Edge edge=edges[nextEdge]; current=edge.A==current ? edge.B : edge.A;
                path.Add(current);
                if (current==first && used.Count<edges.Count) throw new InvalidOperationException("选区包含多个闭环或多余图元，请重选。");
            }
            if (endpoints.Count==2 && current!=endpoints[1]) throw new InvalidOperationException("开放轮廓断点，请重选。");
            if (endpoints.Count==0 && current!=first) throw new InvalidOperationException("闭合轮廓缺边，请重选。");
            // Collapse collinear subdivision without changing any source entity.
            var points=path.Select(i=>nodes[i]).ToList();
            bool closed=endpoints.Count==0;
            if (closed) points.RemoveAt(points.Count-1);
            bool changed=true;
            while (changed)
            {
                changed=false;
                for (int i=closed ? 0 : 1;i<(closed ? points.Count : points.Count-1);i++)
                {
                    ContourPoint prev=points[(i+points.Count-1)%points.Count], now=points[i], next=points[(i+1)%points.Count];
                    if ((Math.Abs(prev.X-now.X)<=Tolerance && Math.Abs(now.X-next.X)<=Tolerance && Between(prev.Y,now.Y,next.Y)) ||
                        (Math.Abs(prev.Y-now.Y)<=Tolerance && Math.Abs(now.Y-next.Y)<=Tolerance && Between(prev.X,now.X,next.X)))
                    { points.RemoveAt(i); changed=true; break; }
                }
            }
            if (!closed)
            {
                if (points.Count!=4) throw new InvalidOperationException("开放轮廓不是三边倒 U，可能缺边或含多余图元，请重选。");
                if (Math.Abs(points[0].Y-points[3].Y)>Tolerance) throw new InvalidOperationException("倒 U 两侧底端标高差超过 1 mm，请重选。");
                double l=points.Min(p=>p.X), r=points.Max(p=>p.X), t=points.Max(p=>p.Y), b=(points[0].Y+points[3].Y)/2;
                CheckSize(l,r,b,t);
                if (!TwoTopCorners(points,l,r,t) || Math.Abs(points[0].Y-t)<=Tolerance || Math.Abs(points[3].Y-t)<=Tolerance)
                    throw new InvalidOperationException("开放轮廓不是标准倒 U，缺少顶部或侧边，请重选。");
                return new List<UpstandGeometry> { Make(l,r,t,b,elevation,UpstandInputKind.OpenInvertedU) };
            }
            if (points.Count==4)
            {
                double l=points.Min(p=>p.X), r=points.Max(p=>p.X), t=points.Max(p=>p.Y), b=points.Min(p=>p.Y);
                CheckSize(l,r,b,t);
                if (!Corners(points,l,r,b,t)) throw new InvalidOperationException("闭合轮廓不是标准矩形，请重选。");
                return new List<UpstandGeometry> { Make(l,r,t,b,elevation,UpstandInputKind.ClosedRectangle) };
            }
            // A full section is a single simple stepped closed boundary. A top edge
            // with downward sides and outward beam-top runs defines one candidate.
            var result=new List<UpstandGeometry>();
            int n=points.Count;
            for (int i=0;i<n;i++)
            {
                ContourPoint a=points[i], b=points[(i+1)%n];
                if (Math.Abs(a.Y-b.Y)>Tolerance || Math.Abs(a.X-b.X)<=Tolerance) continue;
                double left=Math.Min(a.X,b.X), right=Math.Max(a.X,b.X), top=(a.Y+b.Y)/2;
                ContourPoint pa=points[(i+n-1)%n], nb=points[(i+2)%n];
                ContourPoint leftBase=a.X<b.X ? pa : nb, rightBase=a.X<b.X ? nb : pa;
                if (leftBase.Y>=top-Tolerance || rightBase.Y>=top-Tolerance) continue;
                if (Math.Abs(leftBase.Y-rightBase.Y)>Tolerance) continue;
                double baseY=(leftBase.Y+rightBase.Y)/2;
                // The contour must turn outward from both lower side ends.
                ContourPoint leftOut=a.X<b.X ? points[(i+n-2)%n] : points[(i+3)%n];
                ContourPoint rightOut=a.X<b.X ? points[(i+3)%n] : points[(i+n-2)%n];
                if (leftOut.X>=left-Tolerance || rightOut.X<=right+Tolerance ||
                    Math.Abs(leftOut.Y-baseY)>Tolerance || Math.Abs(rightOut.Y-baseY)>Tolerance) continue;
                CheckSize(left,right,baseY,top);
                result.Add(Make(left,right,top,baseY,elevation,UpstandInputKind.FullSection));
            }
            if (result.Count==0) throw new InvalidOperationException("完整截面没有有效的梁顶向上矩形反坎，请检查选区并重选。");
            return result;
        }
        private static int Node(List<ContourPoint> nodes, ContourPoint p)
        {
            for (int i=0;i<nodes.Count;i++)
            {
                double x=nodes[i].X-p.X, y=nodes[i].Y-p.Y;
                if (Math.Sqrt(x*x+y*y)<=Tolerance) return i;
            }
            nodes.Add(p); return nodes.Count-1;
        }
        private static bool Between(double a,double b,double c) { return b>=Math.Min(a,c)-Tiny && b<=Math.Max(a,c)+Tiny; }
        private static bool Overlap(List<ContourPoint> p, Edge a, Edge b)
        {
            if (a.Horizontal!=b.Horizontal) return false;
            ContourPoint a0=p[a.A],a1=p[a.B],b0=p[b.A],b1=p[b.B];
            if (a.Horizontal) return Math.Abs(a0.Y-b0.Y)<=Tolerance && Math.Min(Math.Max(a0.X,a1.X),Math.Max(b0.X,b1.X))-Math.Max(Math.Min(a0.X,a1.X),Math.Min(b0.X,b1.X))>Tiny;
            return Math.Abs(a0.X-b0.X)<=Tolerance && Math.Min(Math.Max(a0.Y,a1.Y),Math.Max(b0.Y,b1.Y))-Math.Max(Math.Min(a0.Y,a1.Y),Math.Min(b0.Y,b1.Y))>Tiny;
        }
        private static bool Cross(List<ContourPoint> p, Edge a, Edge b)
        {
            if (a.Horizontal==b.Horizontal) return false;
            Edge h=a.Horizontal?a:b, v=a.Horizontal?b:a;
            ContourPoint h0=p[h.A],h1=p[h.B],v0=p[v.A],v1=p[v.B];
            if (!Between(h0.X,v0.X,h1.X) || !Between(v0.Y,h0.Y,v1.Y)) return false;
            int shared=(h.A==v.A || h.A==v.B ? 1:0)+(h.B==v.A || h.B==v.B ? 1:0);
            return shared==0;
        }
        private static bool Corners(List<ContourPoint> points,double l,double r,double b,double t)
        {
            foreach (ContourPoint p in points) if ((Math.Abs(p.X-l)>Tolerance && Math.Abs(p.X-r)>Tolerance) || (Math.Abs(p.Y-b)>Tolerance && Math.Abs(p.Y-t)>Tolerance)) return false;
            return true;
        }
        private static bool TwoTopCorners(List<ContourPoint> points,double l,double r,double t)
        {
            int count=0; foreach (ContourPoint p in points) if (Math.Abs(p.Y-t)<=Tolerance && (Math.Abs(p.X-l)<=Tolerance || Math.Abs(p.X-r)<=Tolerance)) count++;
            return count==2;
        }
        private static void CheckSize(double l,double r,double b,double t)
        {
            if (r-l<=Tolerance || t-b<=Tolerance) throw new InvalidOperationException("反坎宽度或高度无效（不大于 1 mm），请重选。");
        }
        private static UpstandGeometry Make(double l,double r,double t,double b,double z,UpstandInputKind kind)
        { return new UpstandGeometry { Left=l,Right=r,Top=t,BeamTop=b,Elevation=z,InputKind=kind }; }
    }
}
