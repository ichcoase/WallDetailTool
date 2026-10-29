using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadApplication=Autodesk.AutoCAD.ApplicationServices.Application;
namespace WallDetailTool
{
    public partial class Commands
    {
        private static void CheckRectangleTarget(Document doc,int unit)
        {
            var active=CadApplication.DocumentManager.MdiActiveDocument;
            if(!RectangleInputGeometry.TargetMatches(doc,active,unit,active==null?-1:(int)active.Database.Insunits))
                throw new InvalidOperationException("目标图纸或单位已切换，本次请求已放弃。");
        }
        private static RectangleInputGeometry PickNodeRectangle(Document doc,int unit,double scale)
        {
            CheckRectangleTarget(doc,unit);var ed=doc.Editor;var ucs=ed.CurrentUserCoordinateSystem;
            ed.WriteMessage("\n两点只定义局部范围，无完整梁/板边界；本次1mm="+scale.ToString("g")+"CAD单位，不修改INSUNITS。");
            var axes=ucs.CoordinateSystem3d;
            using(var view=ed.GetCurrentView())
                RectangleInputGeometry.ValidateAxes(axes.Xaxis.X,axes.Xaxis.Y,axes.Xaxis.Z,axes.Yaxis.X,axes.Yaxis.Y,axes.Yaxis.Z,
                    view.ViewDirection.X,view.ViewDirection.Y,view.ViewDirection.Z,view.ViewTwist);
            var first=ed.GetPoint("\n矩形第一个对角点（仅临时计算范围）：");if(first.Status!=PromptStatus.OK)return null;
            CheckRectangleTarget(doc,unit);
            // Native GetCorner owns and clears its rectangle rubber-band preview.
            var second=ed.GetCorner(new PromptCornerOptions("\n矩形另一个对角点：",first.Value));
            if(second.Status!=PromptStatus.OK)return null;
            CheckRectangleTarget(doc,unit);
            if(!ed.CurrentUserCoordinateSystem.IsEqualTo(ucs))throw new InvalidOperationException("拾取期间UCS改变，请重新点取。");
            var a=first.Value.TransformBy(ucs);var b=second.Value.TransformBy(ucs);
            return RectangleInputGeometry.Create(new RectanglePoint(a.X,a.Y,a.Z),new RectanglePoint(b.X,b.Y,b.Z),scale);
        }
    }
}
