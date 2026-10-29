using System;
using System.Collections.Generic;
using System.Linq;
namespace WallDetailTool
{
    internal struct RectanglePoint
    {
        internal readonly double X,Y,Z;
        internal RectanglePoint(double x,double y,double z) { X=x;Y=y;Z=z; }
    }
    internal sealed class RectangleInputGeometry
    {
        internal readonly double Left,Right,Bottom,Top,Elevation;
        private RectangleInputGeometry(RectanglePoint a,RectanglePoint b,double scale)
        {
            BeamSideMath.Finite(a.X,a.Y,a.Z,b.X,b.Y,b.Z,scale);
            if(scale<=0)throw new InvalidOperationException("单位比例无效。");
            double ax=a.X/scale,ay=a.Y/scale,bx=b.X/scale,by=b.Y/scale,z=a.Z/scale;
            BeamSideMath.Finite(ax,ay,bx,by,z,b.Z/scale);
            if(Math.Abs(z-b.Z/scale)>1e-8)throw new InvalidOperationException("两点不在同一WCS水平平面。");
            Left=Math.Min(ax,bx);Right=Math.Max(ax,bx);Bottom=Math.Min(ay,by);Top=Math.Max(ay,by);Elevation=z;
            BeamSideMath.Finite(Right-Left,Top-Bottom);
            if(Right-Left<=1||Top-Bottom<=1)throw new InvalidOperationException("矩形宽高须大于1mm；不会猜测方向或调整尺寸。");
        }
        internal static RectangleInputGeometry Create(RectanglePoint a,RectanglePoint b,double scale)
        { return new RectangleInputGeometry(a,b,scale); }
        internal static bool TryCreate(RectanglePoint? a,RectanglePoint? b,double scale,out RectangleInputGeometry result)
        { result=null;if(!a.HasValue||!b.HasValue)return false;result=Create(a.Value,b.Value,scale);return true; }
        internal List<ContourSegment> Segments(bool hanging=false)
        {
            var p=new[]{new ContourPoint(Left,Bottom),new ContourPoint(Right,Bottom),new ContourPoint(Right,Top),new ContourPoint(Left,Top)};
            var s=Enumerable.Range(0,4).Select(i=>new ContourSegment(p[i],p[(i+1)%4])).ToList();
            return hanging?NodeOrientation.Reflect(s):s;
        }
        internal UpstandGeometry Beam(bool hanging) { return UpstandRecognizer.Recognize(Segments(hanging),Elevation).Single(); }
        internal SlabUpstandGeometry Slab(bool hanging,double thickness)
        { return SlabUpstandRecognizer.Recognize(Segments(hanging),Elevation,thickness).Single(); }
        internal BeamSideGeometry Side(BeamSideDirection direction)
        { return BeamSideContourRecognizer.Recognize(Segments(),Elevation,direction,false).Candidates.Single(); }
        internal static void ValidateAxes(double xx,double xy,double xz,double yx,double yy,double yz,double vx,double vy,double vz,double twist)
        {
            BeamSideMath.Finite(xx,xy,xz,yx,yy,yz,vx,vy,vz,twist);
            if(Math.Abs(xx-1)+Math.Abs(xy)+Math.Abs(xz)+Math.Abs(yx)+Math.Abs(yy-1)+Math.Abs(yz)>1e-8||
                Math.Abs(vx)+Math.Abs(vy)>1e-8||vz<=0||Math.Abs(Math.Sin(twist))>1e-8||Math.Cos(twist)<0)
                throw new InvalidOperationException("两点矩形仅支持WCS轴向顶视图；请恢复WCS方向和无扭转顶视图后重试。");
        }
        internal static bool TargetMatches(object target,object active,int expected,int actual)
        { return target!=null&&ReferenceEquals(target,active)&&(expected<0||expected==actual); }
    }
}
