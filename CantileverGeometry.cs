using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WallDetailTool
{
    internal enum CantileverDirection { Left=-1, Right=1 }
    internal enum CantileverSupportMode { NoInnerUp=1, NoInnerDown=2, InnerContinuous=3, InnerWithAdditional=4 }
    internal enum CantileverBarRole { Top, Bottom, InnerExtension, Additional }

    // Geometry prerequisites only. This is deliberately not a rebar calculator:
    // unresolved support/free-end lengths and dot rules have no defaults here.
    internal static class CantileverNumbers
    {
        internal static double Finite(double value)
        {
            if(double.IsNaN(value)||double.IsInfinity(value)) throw new ArgumentException("悬挑板数值必须有限。");
            return value;
        }
        internal static double Positive(double value)
        { if(Finite(value)<=0) throw new ArgumentException("悬挑板长度或规格必须大于零。"); return value; }
    }
    internal struct CantileverPoint
    {
        internal readonly double X,Y;
        internal CantileverPoint(double x,double y) { X=CantileverNumbers.Finite(x);Y=CantileverNumbers.Finite(y); }
    }
    internal sealed class CantileverFrame
    {
        internal readonly CantileverDirection Direction;
        internal readonly double SupportX,TopY,BeamWidth;
        internal CantileverFrame(CantileverDirection direction,double supportX,double topY,double beamWidth)
        {
            if(direction!=CantileverDirection.Left&&direction!=CantileverDirection.Right) throw new ArgumentException("伸出方向无效。");
            Direction=direction;SupportX=CantileverNumbers.Finite(supportX);TopY=CantileverNumbers.Finite(topY);
            BeamWidth=CantileverNumbers.Positive(beamWidth);
            ToWorld(-BeamWidth,0); // Also reject overflow of derived coordinates.
        }
        // Local +X points outwards; +Y remains world up for BOTH directions.
        internal CantileverPoint ToWorld(double x,double y)
        { return new CantileverPoint(SupportX+(int)Direction*CantileverNumbers.Finite(x),TopY+CantileverNumbers.Finite(y)); }
        internal double WorldBulge(double bulge) { return (int)Direction*CantileverNumbers.Finite(bulge); }
        internal double CutoffLocalX(double lin)
        { return CantileverNumbers.Finite(-BeamWidth-CantileverNumbers.Positive(lin)); }
        internal CantileverLinDimension LinDimension(double lin,double localY)
        {
            return new CantileverLinDimension(ToWorld(-BeamWidth,localY),ToWorld(CutoffLocalX(lin),localY),lin);
        }
        // A separate drawing symbol, never appended to a controlled steel length.
        // Dimensions are explicit caller inputs; no unconfirmed symbol size.
        internal CantileverPoint CutoffSymbolTip(double lin,double localY,double horizontal,double down)
        { return ToWorld(CutoffLocalX(lin)+CantileverNumbers.Positive(horizontal),localY-CantileverNumbers.Positive(down)); }
        // User confirmed 2026-10-28: BOTH projections are 26 mm.
        internal CantileverPoint FixedCutoffSymbolTip(double lin,double localY)
        { return CutoffSymbolTip(lin,localY,26,26); }
    }
    internal sealed class CantileverLinDimension
    {
        internal readonly CantileverPoint BeamFace,Cutoff;
        internal readonly double Lin;
        internal CantileverLinDimension(CantileverPoint face,CantileverPoint cutoff,double lin)
        { BeamFace=face;Cutoff=cutoff;Lin=CantileverNumbers.Positive(lin); }
    }
    internal sealed class CantileverSpecification
    {
        internal readonly CantileverBarRole Role;
        internal readonly double Diameter,Spacing;
        internal CantileverSpecification(CantileverBarRole role,double diameter,double spacing)
        {
            if(!Enum.IsDefined(typeof(CantileverBarRole),role)) throw new ArgumentException("钢筋角色无效。");
            Role=role;Diameter=CantileverNumbers.Positive(diameter);Spacing=CantileverNumbers.Positive(spacing);
        }
    }
    internal sealed class CantileverSpecifications
    {
        internal readonly CantileverSupportMode Mode;
        internal readonly ReadOnlyCollection<CantileverSpecification> Groups;
        internal CantileverSpecifications(CantileverSupportMode mode,CantileverSpecification top,
            CantileverSpecification bottom,CantileverSpecification additional=null)
        {
            if(!Enum.IsDefined(typeof(CantileverSupportMode),mode)) throw new ArgumentException("支座模式无效。");
            var topRole=(int)mode>=3?CantileverBarRole.InnerExtension:CantileverBarRole.Top;
            if(top==null||top.Role!=topRole||bottom==null||bottom.Role!=CantileverBarRole.Bottom)
                throw new ArgumentException("面筋和底筋须独立保存各自规格。");
            var groups=new List<CantileverSpecification>{top,bottom};
            if(mode==CantileverSupportMode.InnerWithAdditional)
            {
                if(additional==null||additional.Role!=CantileverBarRole.Additional) throw new ArgumentException("第四种模式必须保留独立附加筋规格。");
                groups.Add(additional);
            }
            else if(additional!=null) throw new ArgumentException("当前模式不含附加筋。");
            Mode=mode;Groups=groups.AsReadOnly();
        }
    }
}
