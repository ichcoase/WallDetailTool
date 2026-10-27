using System;
using System.Collections.Generic;

namespace WallDetailTool
{
    internal static class BeamSideTest
    {
        internal static int Checks;
        internal static void Check(bool ok,string message)
        { Checks++;if(!ok) throw new Exception(message); }
        internal static void Near(double a,double b,string message="coordinate")
        { Check(Math.Abs(a-b)<1e-6,message+": "+a+" / "+b); }
        internal static void Reject(Action action,string message)
        {
            Checks++;
            try { action(); } catch(InvalidOperationException) { return; }
            throw new Exception("Expected rejection: "+message);
        }
        internal static BeamSideGeometry Geometry(double width=300,double height=600,double x=0,double y=0,
            BeamSideDirection direction=BeamSideDirection.Right,bool full=false,double depth=500,double margin=200)
        {
            var block=direction==BeamSideDirection.Right?new BeamSideRect(x,x+width,y,y+height):new BeamSideRect(x-width,x,y,y+height);
            BeamSideRect beam=null;
            if(full) beam=direction==BeamSideDirection.Right?new BeamSideRect(x-depth,x,y-margin,y+height+margin):new BeamSideRect(x,x+depth,y-margin,y+height+margin);
            return new BeamSideGeometry(block,beam,direction,full?BeamSideInputKind.FullSection:BeamSideInputKind.LocalRectangle,37);
        }
        internal static List<ContourSegment> Polygon(params double[] xy)
        {
            var list=new List<ContourSegment>();
            for(int i=0;i<xy.Length/2;i++) { int j=(i+1)%(xy.Length/2);list.Add(new ContourSegment(new ContourPoint(xy[2*i],xy[2*i+1]),new ContourPoint(xy[2*j],xy[2*j+1]))); }
            return list;
        }
        internal static List<ContourSegment> Rectangle(double left,double right,double bottom,double top)
        { return Polygon(left,bottom,right,bottom,right,top,left,top); }
    }
}
