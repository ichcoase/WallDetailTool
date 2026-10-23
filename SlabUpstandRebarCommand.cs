using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadApplication=Autodesk.AutoCAD.ApplicationServices.Application;

namespace WallDetailTool
{
    public partial class Commands
    {
        // Stage 2 verification entry. Later phases will add annotations, a block and a toolbar button.
        [CommandMethod("WDTUPSLABREBARTEST",CommandFlags.Modal)]
        public void GenerateSlabUpstandRebarForTest()
        {
            Document doc=CadApplication.DocumentManager.MdiActiveDocument;
            if (doc==null) return;
            Editor ed=doc.Editor;
            try
            {
                SlabUpstandGeometry g=SelectSlabGeometry(doc);
                if (g==null) return;
                // Read on every invocation so saved changes and restored defaults take effect immediately.
                UpstandPanelValues saved=UpstandPanelValues.ReadUserDefaults();
                var p=new SlabUpstandRebarParameters {
                    Cover=saved.Cover, Radius=saved.Radius,
                    LineDiameter=saved.LineDiameter, LineSpacing=saved.LineSpacing,
                    DotDiameter=saved.DotDiameter, DotSpacing=saved.DotSpacing,
                    LineDisplayWidth=saved.LineDisplayWidth,
                    DotDisplayDiameter=saved.DotDisplayDiameter };
                ed.WriteMessage(string.Format("\n使用已保存设置：保护层 c={0:g} mm，圆弧半径 R={1:g} mm。",p.Cover,p.Radius));
                double value;
                if (!AskSlabDouble(ed,"板底至水平筋中心线 H",p.EffectiveH,out value)) return; p.H=value;
                if (!AskSlabDouble(ed,"左水平直段 L左",p.LeftLength,out value)) return; p.LeftLength=value;
                if (!AskSlabDouble(ed,"右水平直段 L右（默认随左侧）",p.LeftLength,out value)) return; p.RightLength=value;
                SlabUpstandRebarLayout layout=SlabUpstandRebarGeometry.Calculate(g,p);
                if (layout.HasSlabBoundaries && (layout.LeftOverrun>0 || layout.RightOverrun>0))
                {
                    var options=new PromptKeywordOptions(string.Format(
                        "\n水平筋超出楼板：左 {0:g} mm，右 {1:g} mm。继续生成或取消 [Continue/Cancel] <Cancel>: ",
                        layout.LeftOverrun,layout.RightOverrun));
                    options.Keywords.Add("Continue"); options.Keywords.Add("Cancel");
                    options.AllowNone=true;
                    PromptResult decision=ed.GetKeywords(options);
                    if (decision.Status!=PromptStatus.OK || decision.StringResult!="Continue") return;
                }
                else if (!layout.HasSlabBoundaries)
                {
                    var options=new PromptKeywordOptions("\n未提供楼板左右边界，水平段需人工审核。继续生成或取消 [Continue/Cancel] <Cancel>: ");
                    options.Keywords.Add("Continue"); options.Keywords.Add("Cancel"); options.AllowNone=true;
                    PromptResult decision=ed.GetKeywords(options);
                    if (decision.Status!=PromptStatus.OK || decision.StringResult!="Continue") return;
                }
                string lineLayer=LineLayer, dotLayer=DotLayer;
                var entities=new List<Entity>();
                try
                {
                    using (Transaction tr=doc.Database.TransactionManager.StartTransaction())
                    {
                        // Layer names must exist before assigning them to new CAD entities.
                        EnsureLayer(doc.Database,tr,lineLayer,1);
                        EnsureLayer(doc.Database,tr,dotLayer,2);
                        var line=new Polyline(layout.Line.Count) { Elevation=layout.Elevation,Layer=lineLayer };
                        entities.Add(line);
                        for (int i=0;i<layout.Line.Count;i++)
                        {
                            RebarVertex v=layout.Line[i];
                            line.AddVertexAt(i,new Point2d(v.X,v.Y),v.Bulge,layout.LineWidth,layout.LineWidth);
                        }
                        foreach (ContourPoint c in layout.LeftDots) entities.Add(UpstandDot(c,layout.Elevation,layout.DotDiameter,dotLayer));
                        foreach (ContourPoint c in layout.RightDots) entities.Add(UpstandDot(c,layout.Elevation,layout.DotDiameter,dotLayer));
                        BlockTableRecord space=(BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId,OpenMode.ForWrite);
                        foreach (Entity entity in entities)
                        {
                            space.AppendEntity(entity);
                            tr.AddNewlyCreatedDBObject(entity,true);
                        }
                        tr.Commit();
                    }
                    ed.WriteMessage("\n板上反坎阶段 2 测试钢筋已生成；尚无标注或普通块。");
                }
                finally { foreach (Entity entity in entities) entity.Dispose(); }
            }
            catch (System.Exception ex) { ed.WriteMessage("\n未生成板上反坎钢筋："+ex.Message); }
        }

        private static bool AskSlabDouble(Editor ed,string label,double defaultValue,out double value)
        {
            value=defaultValue;
            var option=new PromptDoubleOptions("\n"+label+"（mm）: ") { DefaultValue=defaultValue,
                UseDefaultValue=true,AllowNegative=false,AllowZero=false };
            PromptDoubleResult result=ed.GetDouble(option);
            if (result.Status!=PromptStatus.OK) return false;
            value=result.Value; return true;
        }
    }
}
