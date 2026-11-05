using System;
using System.Collections.Generic;

namespace WallDetailTool
{
    // Selection metadata only: not part of the immutable generation request or DWG record.
    internal sealed class TopSlabContourInput
    {
        internal readonly TopSlabGeometry Geometry;
        internal readonly bool HasVirtualBottom;
        internal ContourPoint BottomA { get { return Geometry.World(0,0); } }
        internal ContourPoint BottomB { get { return Geometry.World(Geometry.WallThickness,0); } }
        internal TopSlabContourInput(TopSlabGeometry geometry,bool hasVirtualBottom)
        { Geometry=geometry;HasVirtualBottom=hasVirtualBottom; }

        // Explicit dash geometry needs no database linetype, layer or style changes.
        // Lengths here control the temporary display only, not structural geometry.
        internal IEnumerable<ContourSegment> BottomDashes()
        {
            if(!HasVirtualBottom)yield break;
            double length=Geometry.WallThickness,dash=length/20;
            for(int i=0;i<10;i++)
            {
                double start=2*i*dash;if(start>=length)yield break;
                yield return new ContourSegment(Geometry.World(start,0),Geometry.World(Math.Min(start+dash,length),0));
            }
        }
    }

    internal enum TopSlabConfirmation { Confirm, Reselect, Cancel }

    internal static class TopSlabContourConfirmation
    {
        internal static TopSlabConfirmation Run(TopSlabContourInput input,Func<bool> targetMatches,
            Func<IDisposable> showPreview,Func<TopSlabConfirmation> ask)
        {
            if(!targetMatches())return TopSlabConfirmation.Cancel;
            using(showPreview())
            {
                if(!targetMatches())return TopSlabConfirmation.Cancel;
                if(!input.HasVirtualBottom)return TopSlabConfirmation.Confirm;
                var choice=ask();
                return targetMatches()?choice:TopSlabConfirmation.Cancel;
            }
        }
    }
}
