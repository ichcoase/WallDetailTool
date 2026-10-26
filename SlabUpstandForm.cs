using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using CadApplication=Autodesk.AutoCAD.ApplicationServices.Application;

namespace WallDetailTool
{
    internal sealed class SlabDrawValues
    {
        internal double LineDiameter,LineSpacing,DotDiameter,DotSpacing,LocalThickness,H,LeftLength,RightLength;
        internal double Cover,LineDisplayWidth,DotDisplayDiameter,Radius,TextHeight;
    }

}
