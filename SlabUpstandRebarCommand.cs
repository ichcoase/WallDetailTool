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
        // Kept as an alias for existing stage 2 test drawings.
        [CommandMethod("WDTUPSLABREBARTEST",CommandFlags.Modal)]
        [CommandMethod("WDTUPSLABDRAW",CommandFlags.Modal)]
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
                bool hasPrevious=ReadSlabLast(doc.Database,p);
                ed.WriteMessage(string.Format("\n使用已保存设置：保护层 c={0:g} mm，圆弧半径 R={1:g} mm。",p.Cover,p.Radius));
                double value;
                if (!AskSlabDouble(ed,"板底至水平筋中心线 H",p.EffectiveH,out value)) return; p.H=value;
                if (!AskSlabDouble(ed,"左水平直段 L左",p.LeftLength,out value)) return; p.LeftLength=value;
                if (!AskSlabDouble(ed,"右水平直段 L右（首次默认随左侧）",
                    hasPrevious?p.RightLength:p.LeftLength,out value)) return; p.RightLength=value;
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
                UpstandAnnotationLayout annotations=null;
                var failures=new List<string>();
                var v01=new UpstandGeometry { Left=g.Left,Right=g.Right,Top=g.Top,BeamTop=g.SlabTop,
                    Elevation=g.Elevation,InputKind=g.InputKind };
                var annotationSteel=SlabAnnotationSteel(layout,g);
                var annotationParameters=new UpstandRebarParameters { Cover=p.Cover,LineDiameter=p.LineDiameter,
                    LineSpacing=p.LineSpacing,DotDiameter=p.DotDiameter,DotSpacing=p.DotSpacing,
                    LineDisplayWidth=p.LineDisplayWidth,DotDisplayDiameter=p.DotDisplayDiameter,Radius=p.Radius,
                    LeftAnchor=1,RightAnchor=1 };
                ObjectId dimStyleId=ObjectId.Null;
                try
                {
                    dimStyleId=UpstandDimensionStyle(doc.Database);
                    using (ViewTableRecord view=ed.GetCurrentView())
                    {
                        double left=view.CenterPoint.X-view.Width/2;
                        annotations=UpstandAnnotationGeometry.Calculate(v01,annotationSteel,annotationParameters,
                            saved.TextHeight,left,left+view.Width);
                    }
                    foreach (string warning in annotations.Warnings) ed.WriteMessage("\n"+warning);
                }
                catch (System.Exception ex) { failures.Add("自动标注布局："+ex.Message); }
                string lineLayer=LineLayer, dotLayer=DotLayer;
                var entities=new List<Entity>();
                try
                {
                    ed.Command("_.UNDO","_Begin");
                    try
                    {
                    if (annotations!=null && dimStyleId.IsNull)
                    {
                        try { PrepareArchitecturalTick(doc); }
                        catch (System.Exception ex) { failures.Add("建筑标记准备："+ex.Message); }
                    }
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
                        BlockTable blocks=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForWrite);
                        var definition=new BlockTableRecord { Name="WDT-SLAB-UPSTAND-"+Guid.NewGuid().ToString("N") };
                        ObjectId definitionId=blocks.Add(definition);
                        tr.AddNewlyCreatedDBObject(definition,true);
                        Matrix3d local=Matrix3d.Displacement(new Vector3d(-g.Left,-g.SlabTop,-g.Elevation));
                        foreach (Entity entity in entities)
                        {
                            entity.TransformBy(local);
                            definition.AppendEntity(entity);
                            tr.AddNewlyCreatedDBObject(entity,true);
                        }
                        definition.DowngradeOpen();
                        if (annotations!=null)
                        {
                            TryAppendAnnotation(doc.Database,definitionId,local,LabelLayer,71,"线筋标注",failures,
                                optional=>BuildUpstandLabel(doc.Database,annotations.LineLabel,annotations,"0",
                                    EnsureUpstandTextStyle(doc.Database,optional)));
                            TryAppendAnnotation(doc.Database,definitionId,local,LabelLayer,71,"点筋标注",failures,
                                optional=>BuildUpstandLabel(doc.Database,annotations.DotLabel,annotations,"0",
                                    EnsureUpstandTextStyle(doc.Database,optional)));
                            TryAppendAnnotation(doc.Database,definitionId,local,DimensionLayer,70,"宽度尺寸",failures,
                                optional=>BuildCommittedUpstandDimension(doc.Database,optional,
                                    annotations.WidthA,annotations.WidthB,annotations.WidthLine,annotations.Elevation));
                            TryAppendAnnotation(doc.Database,definitionId,local,DimensionLayer,70,"高度尺寸",failures,
                                optional=>BuildCommittedUpstandDimension(doc.Database,optional,
                                    annotations.HeightA,annotations.HeightB,annotations.HeightLine,annotations.Elevation));
                        }
                        BlockTableRecord space=(BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId,OpenMode.ForWrite);
                        var reference=new BlockReference(new Point3d(g.Left,g.SlabTop,g.Elevation),definitionId)
                            { Rotation=0,Layer=((LayerTableRecord)tr.GetObject(doc.Database.Clayer,OpenMode.ForRead)).Name };
                        space.AppendEntity(reference); tr.AddNewlyCreatedDBObject(reference,true);
                        WriteSlabBlockParameters(tr,reference,g,p);
                        WriteSlabLast(doc.Database,tr,p);
                        tr.Commit();
                    }
                    }
                    finally { ed.Command("_.UNDO","_End"); }
                    ed.WriteMessage("\n板上反坎已生成独立普通块，包含钢筋和已成功的标注；原混凝土轮廓未修改。");
                    foreach (string failure in failures) ed.WriteMessage("\n未完成："+failure);
                }
                finally { foreach (Entity entity in entities) entity.Dispose(); }
            }
            catch (System.Exception ex) { ed.WriteMessage("\n未生成板上反坎钢筋："+ex.Message); }
        }

        private static UpstandRebarLayout SlabAnnotationSteel(SlabUpstandRebarLayout layout,SlabUpstandGeometry g)
        {
            var result=new UpstandRebarLayout { LineWidth=layout.LineWidth,DotDiameter=layout.DotDiameter,
                Elevation=layout.Elevation };
            RebarVertex left=layout.Line[2], right=layout.Line[7];
            result.Line.Add(new RebarVertex(left.X,g.SlabTop-1,0));
            result.Line.Add(new RebarVertex(left.X,g.SlabTop,0));
            for (int i=3;i<=6;i++) result.Line.Add(layout.Line[i]);
            result.Line.Add(new RebarVertex(right.X,g.SlabTop,0));
            result.Line.Add(new RebarVertex(right.X,g.SlabTop-1,0));
            result.LeftDots.AddRange(layout.LeftDots); result.RightDots.AddRange(layout.RightDots);
            return result;
        }

        private static void WriteSlabBlockParameters(Transaction tr,BlockReference reference,
            SlabUpstandGeometry g,SlabUpstandRebarParameters p)
        {
            reference.CreateExtensionDictionary();
            DBDictionary data=(DBDictionary)tr.GetObject(reference.ExtensionDictionary,OpenMode.ForWrite);
            var record=new Xrecord(); data.SetAt(BlockParamRecord,record); tr.AddNewlyCreatedDBObject(record,true);
            record.Data=new ResultBuffer(new TypedValue((int)DxfCode.Int16,2),
                new TypedValue((int)DxfCode.Real,p.Cover),new TypedValue((int)DxfCode.Real,p.Radius),
                new TypedValue((int)DxfCode.Real,g.SlabThickness),new TypedValue((int)DxfCode.Real,p.EffectiveH),
                new TypedValue((int)DxfCode.Real,p.LeftLength),new TypedValue((int)DxfCode.Real,p.RightLength),
                new TypedValue((int)DxfCode.Real,p.LineDiameter),new TypedValue((int)DxfCode.Real,p.LineSpacing),
                new TypedValue((int)DxfCode.Real,p.DotDiameter),new TypedValue((int)DxfCode.Real,p.DotSpacing),
                new TypedValue((int)DxfCode.Real,p.LineDisplayWidth),new TypedValue((int)DxfCode.Real,p.DotDisplayDiameter),
                new TypedValue((int)DxfCode.Real,g.Left),new TypedValue((int)DxfCode.Real,g.Right),
                new TypedValue((int)DxfCode.Real,g.Top),new TypedValue((int)DxfCode.Real,g.SlabTop),
                new TypedValue((int)DxfCode.Real,g.SlabBottom),
                new TypedValue((int)DxfCode.Int16,(short)g.InputKind),
                new TypedValue((int)DxfCode.Int16,(short)(g.HasSlabBoundaries?1:0)),
                new TypedValue((int)DxfCode.Real,g.SlabLeft??0),
                new TypedValue((int)DxfCode.Real,g.SlabRight??0));
        }

        private const string SlabLastRecord="WDT_SLAB_UPSTAND_LAST";
        private static bool ReadSlabLast(Database db,SlabUpstandRebarParameters p)
        {
            using (Transaction tr=db.TransactionManager.StartTransaction())
            {
                DBDictionary nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForRead);
                if (!nod.Contains(SlabLastRecord)) return false;
                Xrecord record=tr.GetObject(nod.GetAt(SlabLastRecord),OpenMode.ForRead) as Xrecord;
                if (record==null || record.Data==null) return false;
                TypedValue[] values=record.Data.AsArray();
                if (values.Length!=3) return false;
                try
                {
                    double h=Convert.ToDouble(values[0].Value), left=Convert.ToDouble(values[1].Value),
                        right=Convert.ToDouble(values[2].Value);
                    if (double.IsNaN(h)||double.IsInfinity(h)||h<=0 || double.IsNaN(left)||double.IsInfinity(left)||left<=0 ||
                        double.IsNaN(right)||double.IsInfinity(right)||right<=0) return false;
                    p.H=h; p.LeftLength=left; p.RightLength=right;
                    return true;
                }
                catch { return false; }
            }
        }

        private static void WriteSlabLast(Database db,Transaction tr,SlabUpstandRebarParameters p)
        {
            DBDictionary nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForWrite);
            Xrecord record;
            if (nod.Contains(SlabLastRecord)) record=(Xrecord)tr.GetObject(nod.GetAt(SlabLastRecord),OpenMode.ForWrite);
            else { record=new Xrecord(); nod.SetAt(SlabLastRecord,record); tr.AddNewlyCreatedDBObject(record,true); }
            record.Data=new ResultBuffer(new TypedValue((int)DxfCode.Real,p.EffectiveH),
                new TypedValue((int)DxfCode.Real,p.LeftLength),new TypedValue((int)DxfCode.Real,p.RightLength));
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
