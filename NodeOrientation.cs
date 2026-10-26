using System.Collections.Generic;

namespace WallDetailTool
{
    // Reflection is its own inverse. Calculators always see a protrusion above
    // its connection face; only this boundary adapter knows the world direction.
    internal static class NodeOrientation
    {
        internal static double Y(double y, bool hanging) { return hanging ? -y : y; }

        internal static List<ContourSegment> Reflect(IList<ContourSegment> source)
        {
            var result=new List<ContourSegment>();
            foreach (var s in source)
                result.Add(new ContourSegment(new ContourPoint(s.A.X,-s.A.Y),new ContourPoint(s.B.X,-s.B.Y)));
            return result;
        }

        internal static void Reflect(List<RebarVertex> line, params List<ContourPoint>[] rows)
        {
            // Keep traversal order; reflection reverses arc handedness.
            for (int i=0;i<line.Count;i++)
            {
                var v=line[i]; line[i]=new RebarVertex(v.X,-v.Y,-v.Bulge);
            }
            foreach (var row in rows)
                for (int i=0;i<row.Count;i++) row[i]=new ContourPoint(row[i].X,-row[i].Y);
        }

        private static AnnotationPoint Reflect(AnnotationPoint p) { return new AnnotationPoint(p.X,-p.Y); }
        internal static void Reflect(UpstandAnnotationLayout a)
        {
            if (a==null) return;
            foreach (var label in new[]{a.LineLabel,a.DotLabel})
            {
                // Reflect leaders, then place a freshly constructed upright TEXT
                // baseline 4 mm above its underline (never mirror a text entity).
                label.TextAnchor=new AnnotationPoint(label.TextAnchor.X,-label.TextAnchor.Y+8);
                label.SlashCenter=Reflect(label.SlashCenter);
                label.SlashA=Reflect(label.SlashA); label.SlashB=Reflect(label.SlashB);
                for (int i=0;i<label.Leaders.Count;i++)
                {
                    var s=label.Leaders[i];
                    label.Leaders[i]=new AnnotationSegment(Reflect(s.A),Reflect(s.B));
                }
                if (label.KeepLastLeaderEnd)
                {
                    // The end-row label now sits below the concrete. Reserve
                    // upright glyph height so text cannot grow back into it.
                    label.TextAnchor.Y-=a.TextHeight;
                    for (int i=0;i<label.Leaders.Count;i++)
                    {
                        var s=label.Leaders[i];
                        s.B.Y-=a.TextHeight;
                        if (i==label.Leaders.Count-1) s.A.Y-=a.TextHeight;
                        label.Leaders[i]=s;
                    }
                }
            }
            a.WidthA=Reflect(a.WidthA); a.WidthB=Reflect(a.WidthB); a.WidthLine=Reflect(a.WidthLine);
            // Order vertical dimension points from bottom to top for upright CAD dimensions.
            var bottom=Reflect(a.HeightB); a.HeightB=Reflect(a.HeightA); a.HeightA=bottom;
            a.HeightLine=Reflect(a.HeightLine);
        }
    }
}
