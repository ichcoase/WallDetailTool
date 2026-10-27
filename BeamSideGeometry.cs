using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WallDetailTool
{
    internal enum BeamSideDirection { Left=-1, Right=1 }
    internal enum BeamSideInputKind { LocalRectangle, LocalOpenU, FullSection }
    internal enum BeamSideBoundaryStatus { Unknown, Checked }

    // Assembly-facing geometry is always WCS millimetres. The calculator alone
    // uses a right-facing local frame at the connection's lower block endpoint.
    internal sealed class BeamSideRect
    {
        internal readonly double Left, Right, Bottom, Top;
        internal double Width { get { return Right-Left; } }
        internal double Height { get { return Top-Bottom; } }
        internal BeamSideRect(double left,double right,double bottom,double top)
        {
            BeamSideMath.Finite(left,right,bottom,top);
            BeamSideMath.Finite(right-left,top-bottom);
            if(right-left<=BeamSideMath.Epsilon || top-bottom<=BeamSideMath.Epsilon)
                throw new InvalidOperationException("矩形宽度或高度无效。");
            Left=left; Right=right; Bottom=bottom; Top=top;
        }
    }

    internal sealed class BeamSideGeometry
    {
        internal readonly BeamSideRect Block, Beam;
        internal readonly BeamSideDirection Direction;
        internal readonly BeamSideInputKind InputKind;
        internal readonly double Elevation;
        internal double ConnectionX { get { return Direction==BeamSideDirection.Right ? Block.Left : Block.Right; } }
        internal ContourPoint BasePoint { get { return new ContourPoint(ConnectionX,Block.Bottom); } }
        internal double ConnectionBottom { get { return Beam==null ? Block.Bottom : Math.Max(Block.Bottom,Beam.Bottom); } }
        internal double ConnectionTop { get { return Beam==null ? Block.Top : Math.Min(Block.Top,Beam.Top); } }
        internal BeamSideGeometry(BeamSideRect block,BeamSideRect beam,BeamSideDirection direction,
            BeamSideInputKind kind,double elevation)
        {
            if(block==null || (direction!=BeamSideDirection.Right && direction!=BeamSideDirection.Left))
                throw new InvalidOperationException("缺少块体或方向无效。");
            BeamSideMath.Finite(elevation);
            if(kind==BeamSideInputKind.FullSection && beam==null)
                throw new InvalidOperationException("完整截面必须提供梁边界。");
            if(kind!=BeamSideInputKind.FullSection && beam!=null)
                throw new InvalidOperationException("局部截面不能冒充已知梁边界。");
            if(kind!=BeamSideInputKind.FullSection && kind!=BeamSideInputKind.LocalRectangle && kind!=BeamSideInputKind.LocalOpenU)
                throw new InvalidOperationException("节点输入类型无效。");
            if(beam!=null)
            {
                double connection=direction==BeamSideDirection.Right ? block.Left : block.Right;
                double beamSide=direction==BeamSideDirection.Right ? beam.Right : beam.Left;
                if(Math.Abs(connection-beamSide)>BeamSideMath.Epsilon ||
                    Math.Min(block.Top,beam.Top)-Math.Max(block.Bottom,beam.Bottom)<=BeamSideMath.Epsilon)
                    throw new InvalidOperationException("梁与块体未沿有效连接面相接。");
            }
            Block=block; Beam=beam; Direction=direction; InputKind=kind; Elevation=elevation;
        }
        internal ContourPoint ToWorld(ContourPoint local)
        { return new ContourPoint(ConnectionX+(int)Direction*local.X,Block.Bottom+local.Y); }
        internal ContourPoint ToLocal(ContourPoint world)
        { return new ContourPoint((int)Direction*(world.X-ConnectionX),world.Y-Block.Bottom); }
    }

    // A full equal-height outer rectangle without a seam has infinitely many
    // splits. The caller must request connection X within this explicit region.
    internal sealed class BeamSideSplitRegion
    {
        internal readonly BeamSideRect Outline;
        internal readonly BeamSideDirection Direction;
        internal readonly double Elevation;
        internal BeamSideSplitRegion(BeamSideRect outline,BeamSideDirection direction,double elevation)
        { Outline=outline; Direction=direction; Elevation=elevation; }
        internal BeamSideGeometry Resolve(double connectionX)
        {
            BeamSideMath.Finite(connectionX);
            if(connectionX<=Outline.Left+BeamSideContourRecognizer.Tolerance || connectionX>=Outline.Right-BeamSideContourRecognizer.Tolerance)
                throw new InvalidOperationException("连接面必须位于完整矩形内部，梁及块体宽度均须大于 1 mm。");
            var left=new BeamSideRect(Outline.Left,connectionX,Outline.Bottom,Outline.Top);
            var right=new BeamSideRect(connectionX,Outline.Right,Outline.Bottom,Outline.Top);
            return new BeamSideGeometry(Direction==BeamSideDirection.Right?right:left,
                Direction==BeamSideDirection.Right?left:right,Direction,BeamSideInputKind.FullSection,Elevation);
        }
    }
    internal sealed class BeamSideRecognition
    {
        internal readonly ReadOnlyCollection<BeamSideGeometry> Candidates;
        internal readonly ReadOnlyCollection<BeamSideSplitRegion> SplitRegions;
        internal BeamSideRecognition(List<BeamSideGeometry> candidates,List<BeamSideSplitRegion> split)
        { Candidates=new List<BeamSideGeometry>(candidates).AsReadOnly(); SplitRegions=new List<BeamSideSplitRegion>(split).AsReadOnly(); }
        internal BeamSideGeometry SelectCandidate(double x,double y)
        {
            BeamSideMath.Finite(x,y);
            BeamSideGeometry found=null;
            foreach(var c in Candidates)
                if(x>c.Block.Left && x<c.Block.Right && y>c.Block.Bottom && y<c.Block.Top)
                {
                    if(found!=null) throw new InvalidOperationException("点选位置覆盖多个候选，请重新选择。");
                    found=c;
                }
            if(found==null) throw new InvalidOperationException("点选位置不在候选块体内部。");
            return found;
        }
    }
    internal static class BeamSideMath
    {
        internal const double Epsilon=1e-8;
        internal static void Finite(params double[] values)
        { foreach(double v in values) if(Double.IsNaN(v)||Double.IsInfinity(v)) throw new InvalidOperationException("坐标或参数必须为有限数值。"); }
        internal static double Distance(ContourPoint a,ContourPoint b)
        { double x=a.X-b.X,y=a.Y-b.Y; return Math.Sqrt(x*x+y*y); }
        internal static double Clamp(double value,double low,double high) { return Math.Max(low,Math.Min(high,value)); }
    }
}
