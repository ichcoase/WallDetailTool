using System;
using System.Collections.Generic;
using System.Linq;
namespace WallDetailTool
{
    internal static class BeamSideContourTests
    {
        static ContourSegment S(double x,double y,double xx,double yy)
        { return new ContourSegment(new ContourPoint(x,y),new ContourPoint(xx,yy)); }
        static List<ContourSegment> Transform(IEnumerable<ContourSegment> list,BeamSideDirection direction,double x,double y)
        { return list.Select(s=>S(x+(int)direction*s.A.X,y+s.A.Y,x+(int)direction*s.B.X,y+s.B.Y)).ToList(); }
        static List<ContourSegment> Union(double bb,double bt,double cb,double ct)
        {
            var list=new List<ContourSegment> { S(-500,bb,0,bb),S(0,bt,-500,bt),S(-500,bt,-500,bb),
                S(0,cb,300,cb),S(300,cb,300,ct),S(300,ct,0,ct) };
            var ys=new[]{bb,bt,cb,ct}.Distinct().OrderBy(v=>v).ToArray();
            for(int i=1;i<ys.Length;i++)
            {
                double mid=(ys[i]+ys[i-1])/2;
                if((mid>bb&&mid<bt) != (mid>cb&&mid<ct)) list.Add(S(0,ys[i-1],0,ys[i]));
            }
            return list;
        }
        static void Main() { try { Run(); } catch(Exception ex) { Console.WriteLine("FAIL: "+ex.Message);Environment.ExitCode=1; } }
        static void Run()
        {
            foreach(var direction in new[]{BeamSideDirection.Left,BeamSideDirection.Right})
            foreach(var origin in new[]{new ContourPoint(0,0),new ContourPoint(1200,-350),new ContourPoint(-700,800)})
            {
                var open=Transform(new[]{S(300,600,300,0),S(300,0,0,0),S(0,600,300,600)},direction,origin.X,origin.Y);
                foreach(bool closed in new[]{false,true})
                {
                    var list=new List<ContourSegment>(open);
                    if(closed) list.Add(S(origin.X,origin.Y,origin.X,origin.Y+600));
                    list.Reverse();
                    var saved=list.Select(s=>s.A.X).ToArray();
                    var result=BeamSideContourRecognizer.Recognize(list,19,direction,false);
                    var g=result.Candidates.Single();
                    BeamSideTest.Near(g.ConnectionX,origin.X);BeamSideTest.Near(g.Block.Bottom,origin.Y);
                    BeamSideTest.Near(g.Block.Width,300);BeamSideTest.Near(g.Block.Height,600);BeamSideTest.Near(g.Elevation,19);
                    BeamSideTest.Check(g.Beam==null,"local unknown beam");
                    BeamSideTest.Check(g.InputKind==(closed?BeamSideInputKind.LocalRectangle:BeamSideInputKind.LocalOpenU),"local kind");
                    BeamSideTest.Check(list.Select(s=>s.A.X).SequenceEqual(saved),"source readonly");
                }
                foreach(var heights in new[]{new[]{0.0,600.0},new[]{5.0,595.0},new[]{100.0,700.0},
                    new[]{-100.0,500.0},new[]{100.0,500.0},new[]{-100.0,700.0}})
                foreach(bool seam in new[]{false,true})
                {
                    var list=seam?BeamSideTest.Rectangle(-500,0,0,600):Union(0,600,heights[0],heights[1]);
                    if(seam) list.AddRange(BeamSideTest.Rectangle(0,300,heights[0],heights[1]));
                    list=Transform(list,direction,origin.X,origin.Y);
                    // Input order and traversal direction do not define the node.
                    list=list.Select((s,i)=>i%2==0?new ContourSegment(s.B,s.A):s).Reverse().ToList();
                    var result=BeamSideContourRecognizer.Recognize(list,7,direction,true);
                    BeamSideGeometry g;
                    if(!seam && heights[0]==0 && heights[1]==600)
                    {
                        BeamSideTest.Check(result.Candidates.Count==0&&result.SplitRegions.Count==1,"equal-height no seam needs split");
                        g=result.SplitRegions[0].Resolve(origin.X);
                        BeamSideTest.Reject(()=>result.SplitRegions[0].Resolve(result.SplitRegions[0].Outline.Left),"invalid split");
                    }
                    else { BeamSideTest.Check(result.SplitRegions.Count==0,"identified full connection");g=result.Candidates.Single(); }
                    BeamSideTest.Near(g.ConnectionX,origin.X);BeamSideTest.Near(g.Block.Width,300);
                    BeamSideTest.Near(g.Beam.Width,500);BeamSideTest.Near(g.Beam.Bottom,origin.Y);
                    BeamSideTest.Near(g.Block.Bottom,origin.Y+heights[0]);BeamSideTest.Near(g.Block.Top,origin.Y+heights[1]);
                    BeamSideTest.Check(g.InputKind==BeamSideInputKind.FullSection,"full kind");
                    BeamSideTest.Near(g.ConnectionBottom,origin.Y+Math.Max(0,heights[0]));
                    BeamSideTest.Near(g.ConnectionTop,origin.Y+Math.Min(600,heights[1]));
                }
            }
            var two=BeamSideTest.Rectangle(0,300,0,600);two.AddRange(BeamSideTest.Rectangle(500,800,100,700));
            var candidates=BeamSideContourRecognizer.Recognize(two,0,BeamSideDirection.Right,false);
            BeamSideTest.Check(candidates.Candidates.Count==2,"multiple local candidates");
            BeamSideTest.Near(candidates.SelectCandidate(600,400).Block.Left,500);
            BeamSideTest.Reject(()=>candidates.SelectCandidate(400,400),"candidate miss");
            var detached=BeamSideTest.Rectangle(-500,0,0,600);detached.AddRange(BeamSideTest.Rectangle(10,310,0,600));
            BeamSideTest.Reject(()=>BeamSideContourRecognizer.Recognize(detached,0,BeamSideDirection.Right,true),"disconnected full");
            var repeated=Union(0,600,50,550);repeated.Add(repeated[0]);
            BeamSideTest.Reject(()=>BeamSideContourRecognizer.Recognize(repeated,0,BeamSideDirection.Right,true),"duplicate outer");
            var partial=Union(0,600,50,550);partial.Add(S(0,100,0,300));
            BeamSideTest.Reject(()=>BeamSideContourRecognizer.Recognize(partial,0,BeamSideDirection.Right,true),"partial seam");
            var branches=Union(0,600,50,550);branches.Add(S(-250,0,-250,600));
            BeamSideTest.Reject(()=>BeamSideContourRecognizer.Recognize(branches,0,BeamSideDirection.Right,true),"wrong seam");
            var complex=BeamSideTest.Polygon(-500,0,0,0,0,50,300,50,300,200,500,200,500,400,300,400,300,550,0,550,0,600,-500,600);
            BeamSideTest.Reject(()=>BeamSideContourRecognizer.Recognize(complex,0,BeamSideDirection.Right,true),"complex section");
            var spur=Union(0,600,50,550);spur.Add(S(300,200,400,200));
            BeamSideTest.Reject(()=>BeamSideContourRecognizer.Recognize(spur,0,BeamSideDirection.Right,true),"spur");
            foreach(var bad in new[]{new List<ContourSegment>{S(0,0,300,20),S(300,20,300,600),S(300,600,0,0)},
                new List<ContourSegment>{S(Double.NaN,0,300,0),S(300,0,300,600),S(300,600,0,0)}})
                BeamSideTest.Reject(()=>BeamSideContourRecognizer.Recognize(bad,0,BeamSideDirection.Right,false),"illegal input");
            var subdivided=new List<ContourSegment>{S(0,0,150,0),S(150,0,300,0),S(300,0,300,600),S(300,600,0,600),S(0,600,0,0)};
            BeamSideTest.Near(BeamSideContourRecognizer.Recognize(subdivided,0,BeamSideDirection.Right,false).Candidates.Single().Block.Width,300);
            var near=Union(0,600,50,550);var segment=near[0];near[0]=S(segment.A.X,segment.A.Y+.4,segment.B.X,segment.B.Y);
            BeamSideTest.Check(BeamSideContourRecognizer.Recognize(near,0,BeamSideDirection.Right,true).Candidates.Count==1,"1mm recognition tolerance");
            BeamSideTest.Reject(()=>BeamSideContourRecognizer.Recognize(null,0,BeamSideDirection.Right,false),"null");
            BeamSideTest.Reject(()=>new BeamSideGeometry(new BeamSideRect(10,300,0,600),new BeamSideRect(-500,0,0,600),BeamSideDirection.Right,BeamSideInputKind.FullSection,0),"invalid connection");
            Console.WriteLine("BEAM_SIDE_CONTOUR_PASS checks="+BeamSideTest.Checks);
        }
    }
}
