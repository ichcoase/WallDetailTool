using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace WallDetailTool
{
    public partial class Commands
    {
        [CommandMethod("WDTUPSLABCONTOUR", CommandFlags.Modal)]
        public void ExtractSlabUpstandContour()
        {
            Document doc=CadApplication.DocumentManager.MdiActiveDocument;
            if (doc==null) return;
            Editor ed=doc.Editor;
            try
            {
                SlabUpstandGeometry chosen=SelectSlabGeometry(doc);
                if (chosen==null) return;
                ed.WriteMessage(string.Format("\n板上反坎识别：{0}，反坎宽={1:g} 高={2:g}，板顶={3:g} 板底={4:g} 厚={5:g} mm，板界={6}。不生成钢筋。",
                    chosen.InputKind,chosen.Width,chosen.Height,chosen.SlabTop,chosen.SlabBottom,chosen.SlabThickness,
                    chosen.HasSlabBoundaries?string.Format("左 {0:g}／右 {1:g}",chosen.SlabLeft.Value,chosen.SlabRight.Value):"未知（无法判断钢筋越界）"));
            }
            catch (System.Exception ex) { ed.WriteMessage("\n板上反坎识别失败："+ex.Message); }
        }

        private static SlabUpstandGeometry SelectSlabGeometry(Document doc)
        { return SelectSlabGeometry(doc,null); }

        private static SlabUpstandGeometry SelectSlabGeometry(Document doc,double? providedLocalThickness)
        {
            Editor ed=doc.Editor;
                ed.WriteMessage("\n选择板上反坎轮廓（LINE/LWPOLYLINE）；只读识别，不修改原图元。");
                PromptSelectionResult selected=ed.GetSelection();
                if (selected.Status!=PromptStatus.OK) return null;
                var segments=new List<ContourSegment>();
                double? elevation=null;
                using (Transaction tr=doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject item in selected.Value)
                    {
                        if (item==null) continue;
                        Entity entity=tr.GetObject(item.ObjectId,OpenMode.ForRead) as Entity;
                        Line line=entity as Line;
                        if (line!=null)
                        {
                            AddContourElevation(ref elevation,line.StartPoint.Z,line.EndPoint.Z);
                            segments.Add(new ContourSegment(new ContourPoint(line.StartPoint.X,line.StartPoint.Y),
                                new ContourPoint(line.EndPoint.X,line.EndPoint.Y)));
                            continue;
                        }
                        Polyline polyline=entity as Polyline;
                        if (polyline==null) throw new InvalidOperationException("仅支持 LINE/LWPOLYLINE。");
                        if (polyline.Normal.Z<1-Epsilon || Math.Abs(polyline.Normal.X)>Epsilon || Math.Abs(polyline.Normal.Y)>Epsilon)
                            throw new InvalidOperationException("轮廓不在 WCS 水平二维平面。");
                        int count=polyline.NumberOfVertices;
                        if (count<2) throw new InvalidOperationException("多段线顶点不足。");
                        for (int i=0;i<(polyline.Closed?count:count-1);i++)
                        {
                            if (Math.Abs(polyline.GetBulgeAt(i))>Epsilon) throw new InvalidOperationException("不支持圆弧轮廓。");
                            Point3d a=polyline.GetPoint3dAt(i), b=polyline.GetPoint3dAt((i+1)%count);
                            AddContourElevation(ref elevation,a.Z,b.Z);
                            segments.Add(new ContourSegment(new ContourPoint(a.X,a.Y),new ContourPoint(b.X,b.Y)));
                        }
                    }
                }
                if (doc.Database.Insunits!=UnitsValue.Millimeters && doc.Database.Insunits!=UnitsValue.Undefined)
                    ed.WriteMessage("\n固定 1 CAD 单位 = 1 mm；当前 INSUNITS 非毫米，不自动换算，请核对实际尺寸。");
                // Ask for thickness only for a local outline; a full section supplies it.
                var baseCandidates=UpstandRecognizer.Recognize(segments,elevation??0);
                double? manualThickness=null;
                if (baseCandidates[0].InputKind!=UpstandInputKind.FullSection)
                {
                    if (providedLocalThickness.HasValue)
                    {
                        if (providedLocalThickness.Value<=0) throw new InvalidOperationException("局部轮廓须在本次绘制窗口输入正数板厚 h。");
                        manualThickness=providedLocalThickness;
                    }
                    else
                    {
                        var option=new PromptDoubleOptions("\n楼板厚度 h（mm）: ") { AllowNegative=false,AllowZero=false };
                        PromptDoubleResult thickness=ed.GetDouble(option);
                        if (thickness.Status!=PromptStatus.OK) return null;
                        manualThickness=thickness.Value;
                    }
                }
                var candidates=SlabUpstandRecognizer.Recognize(segments,elevation??0,manualThickness);
                SlabUpstandGeometry chosen=candidates[0];
                if (candidates.Count>1)
                {
                    ed.WriteMessage("\n存在多个反坎候选，请点选目标内部。");
                    PromptPointResult target=ed.GetPoint("\n目标反坎内部: ");
                    if (target.Status!=PromptStatus.OK) return null;
                    chosen=SlabUpstandRecognizer.SelectCandidate(candidates,target.Value.X,target.Value.Y);
                    var checkedCandidates=SlabUpstandRecognizer.Recognize(segments,elevation??0,manualThickness);
                    SlabUpstandGeometry checkedChoice=SlabUpstandRecognizer.SelectCandidate(checkedCandidates,target.Value.X,target.Value.Y);
                    if (Math.Abs(checkedChoice.Left-chosen.Left)>Epsilon || Math.Abs(checkedChoice.Right-chosen.Right)>Epsilon ||
                        Math.Abs(checkedChoice.SlabBottom-chosen.SlabBottom)>Epsilon)
                        throw new InvalidOperationException("点选后几何复核失败。");
                    chosen=checkedChoice;
                    chosen.CandidateState=SlabCandidateState.SelectedAndRevalidated;
                }
                return chosen;
        }
    }
}
