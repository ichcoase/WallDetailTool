using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
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
        [CommandMethod("WDTCANTILEVER",CommandFlags.Session)]
        public async void ShowCantileverForm()
        {
            Document target=CadApplication.DocumentManager.MdiActiveDocument;if(target==null) return;
            try
            {
                var shared=UpstandPanelValues.ReadUserDefaults();var names=UpstandLayers.Read();
                string[] layers={names.Line,names.Dot,names.Text,names.Dimension};
                var saved=ReadCantileverLast(target.Database,layers,target.Editor);
                var state=new CantileverDrawState(saved,AdvancedValues(shared),layers,new[]{shared.LineDiameter,shared.LineSpacing,shared.DotDiameter,shared.DotSpacing});
                double scale=0;try { scale=MillimetresToDrawingUnits(target.Database); } catch(System.Exception) { }
                int units=(int)target.Database.Insunits;CantileverDrawRequest request;
                using(var form=new CantileverDrawForm(state,BeamSideUnitName(target.Database.Insunits),units,scale,units!=0&&units!=4))
                { if(CadApplication.ShowModalDialog(form)!=DialogResult.OK) return;request=form.Request; }
                if(request==null) return;
                await CadApplication.DocumentManager.ExecuteInCommandContextAsync(_=>
                {
                    var active=CadApplication.DocumentManager.MdiActiveDocument;
                    if(active!=null&&CantileverDrawRequest.TargetMatches(target,active,request.UnitCode,(int)active.Database.Insunits)) ExecuteCantilever(target,request);
                    else if(active!=null) active.Editor.WriteMessage("\n图纸或单位已切换，本次悬挑板请求已放弃。");
                    return Task.CompletedTask;
                },null);
            }
            catch(System.Exception ex) { if(ReferenceEquals(target,CadApplication.DocumentManager.MdiActiveDocument)) target.Editor.WriteMessage("\n悬挑板未启动："+ex.Message); }
        }
        private static void ExecuteCantilever(Document doc,CantileverDrawRequest r)
        {
            try
            {
                var ed=doc.Editor;
                ed.WriteMessage("\n仅选择悬挑板侧局部矩形或向梁开口U形轮廓；原混凝土只读。1mm="+r.Scale.ToString("g")+"CAD单位。");
                var selected=ed.GetSelection(new PromptSelectionOptions { MessageForAdding="\n选择悬挑板局部轮廓（不含内跨和梁外框）：" });
                if(selected.Status!=PromptStatus.OK) return;
                double elevation;var unit=new BeamSideUnitTransform(r.Scale);
                var source=ReadBeamSideSegments(doc.Database,selected.Value.GetObjectIds(),unit,out elevation);
                var found=BeamSideContourRecognizer.Recognize(source,elevation,(BeamSideDirection)(int)r.Direction,false);
                BeamSideGeometry g;
                if(found.Candidates.Count==1) g=found.Candidates[0];
                else
                {
                    var pick=ed.GetPoint("\n存在多个悬挑区域，请点选目标区域内部：");if(pick.Status!=PromptStatus.OK) return;
                    var point=pick.Value.TransformBy(ed.CurrentUserCoordinateSystem);
                    g=found.SelectCandidate(point.X/r.Scale,point.Y/r.Scale);
                }
                double x=r.Direction==CantileverDirection.Right?g.Block.Left:g.Block.Right;
                var frame=new CantileverFrame(r.Direction,x,g.Block.Top,r.Value(0));var p=r.Inputs(g.Block.Width,g.Block.Height);
                var steel=CantileverRebarGeometry.Calculate(frame,r.Specifications,p);
                var dots=CantileverDots.Calculate(frame,steel,p,r.Advanced(2),r.Value(14));
                if(!CantileverDrawRequest.TargetMatches(doc,CadApplication.DocumentManager.MdiActiveDocument,r.UnitCode,(int)doc.Database.Insunits))
                    throw new InvalidOperationException("目标图纸或单位改变，请重新打开悬挑板窗口。");
                var failures=new List<string>();
                ed.Command("_.UNDO","_Begin");
                try { CreateCantileverBlock(doc.Database,r,frame,p,steel,dots,elevation,failures); }
                finally { ed.Command("_.UNDO","_End"); }
                ed.WriteMessage("\n已生成普通悬挑板独立普通块；上下各"+dots.Upper.Count+"根点筋，源轮廓只读。成功参数仅保存至悬挑板类型；构造请人工审核。");
                foreach(string failure in failures) ed.WriteMessage("\n未完成："+failure);
            }
            catch(System.Exception ex) { doc.Editor.WriteMessage("\n悬挑板生成未完成："+ex.Message); }
        }
        internal static List<Entity> BuildCantileverRebars(CantileverFrame frame,CantileverDrawRequest r,CantileverPathInputs p,
            CantileverRebarLayout steel,CantileverDotLayout dots,double elevation)
        {
            var result=new List<Entity>();double scale=r.Scale,z=elevation*scale,w=p.DisplayWidth*scale;
            try
            {
                foreach(var bar in steel.Bars)
                {
                    var poly=new Polyline(bar.Vertices.Count) { Elevation=z,Layer=r.Layer(0),ColorIndex=256 };result.Add(poly);
                    for(int i=0;i<bar.Vertices.Count;i++)
                    { var v=bar.Vertices[i];poly.AddVertexAt(i,new Point2d(v.Point.X*scale,v.Point.Y*scale),v.Bulge,w,w); }
                }
                foreach(var dot in dots.Upper.Concat(dots.Lower)) result.Add(UpstandDot(new ContourPoint(dot.X*scale,dot.Y*scale),z,r.Advanced(2)*scale,r.Layer(1)));
                if(r.Mode==CantileverSupportMode.InnerWithAdditional)
                {
                    var start=steel.Bars[2].Vertices[0].Point;var end=frame.FixedCutoffSymbolTip(p.Lin,start.Y-frame.TopY);
                    var hook=new Polyline(2) { Elevation=z,Layer=r.Layer(0),ColorIndex=256 };result.Add(hook);
                    hook.AddVertexAt(0,new Point2d(start.X*scale,start.Y*scale),0,w,w);hook.AddVertexAt(1,new Point2d(end.X*scale,end.Y*scale),0,w,w);
                }
                return result;
            }
            catch { foreach(var entity in result) entity.Dispose();throw; }
        }
        internal static ObjectId CreateCantileverBlock(Database db,CantileverDrawRequest r,CantileverFrame frame,CantileverPathInputs p,
            CantileverRebarLayout steel,CantileverDotLayout dots,double elevation,List<string> failures)
        {
            if((int)db.Insunits!=r.UnitCode||frame.Direction!=r.Direction||frame.BeamWidth!=r.Value(0)) throw new InvalidOperationException("悬挑板请求与图纸不一致。");
            double scale=r.Scale;var origin=new Point3d(frame.SupportX*scale,frame.TopY*scale,elevation*scale);
            var local=Matrix3d.Displacement(Point3d.Origin-origin);
            var annotation=CantileverAnnotationGeometry.Calculate(frame,r,p,steel,dots,elevation);
            using(var tr=db.TransactionManager.StartTransaction())
            {
                for(int i=0;i<2;i++)
                {
                    EnsureLayer(db,tr,r.Layer(i),(short)(i+1));var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
                    var layer=(LayerTableRecord)tr.GetObject(layers[r.Layer(i)],OpenMode.ForRead);
                    if(layer.IsLocked) throw new InvalidOperationException("目标钢筋图层已锁定："+r.Layer(i));
                    if(layer.IsOff||layer.IsFrozen) failures.Add("图层关闭或冻结，可能不可见："+r.Layer(i));
                }
                var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);
                var block=new BlockTableRecord { Name="WDT-CANTILEVER-"+Guid.NewGuid().ToString("N") };
                var id=table.Add(block);tr.AddNewlyCreatedDBObject(block,true);table.DowngradeOpen();
                var entities=BuildCantileverRebars(frame,r,p,steel,dots,elevation);
                try { foreach(var entity in entities) { entity.TransformBy(local);Append(block,tr,entity); } }
                finally { foreach(var entity in entities) if(entity.ObjectId.IsNull) entity.Dispose(); }
                block.DowngradeOpen();
                foreach(var item in annotation.Labels)
                {
                    var label=item.Layout;double y=label.Leaders.Last().A.Y;
                    var above=annotation.Labels.Where(q=>q.Layout.Rightward==label.Rightward&&q.Layout.Leaders.Last().A.Y>y+1e-8).Select(q=>q.Layout.Leaders.Last().A.Y).ToArray();
                    double? neighbour=above.Length==0?(double?)null:above.Min();
                    TryAppendAnnotation(db,id,local,r.Layer(2),71,item.Name+"规格",failures,optional=>
                        BuildCantileverLabel(db,optional,label,annotation.Dimensions,r.Layer(2),(left,right)=>
                        {
                            if(label.Rightward==(r.Direction==CantileverDirection.Right))
                                annotation.Dimensions.HeightLine=BeamSideAnnotationGeometry.HeightLineOutsideLabels(annotation.Dimensions,label.Rightward?right:left,scale,label.Rightward);
                        },neighbour));
                }
                var a=annotation.Dimensions;
                TryAppendAnnotation(db,id,local,r.Layer(3),70,"悬挑长度尺寸",failures,optional=>BuildBeamSideDimension(db,optional,a.WidthA,a.WidthB,a.WidthLine,a.Elevation,scale,r.Layer(3)));
                TryAppendAnnotation(db,id,local,r.Layer(3),70,"板厚尺寸",failures,optional=>BuildBeamSideDimension(db,optional,a.HeightA,a.HeightB,a.HeightLine,a.Elevation,scale,r.Layer(3)));
                if(annotation.Lin!=null)
                {
                    var lin=annotation.Lin;
                    TryAppendAnnotation(db,id,local,r.Layer(3),70,"附加筋Lin尺寸",failures,optional=>BuildBeamSideDimension(db,optional,
                        new AnnotationPoint(lin.BeamFace.X*scale,lin.BeamFace.Y*scale),new AnnotationPoint(lin.Cutoff.X*scale,lin.Cutoff.Y*scale),annotation.LinLine,a.Elevation,scale,r.Layer(3)));
                }
                var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);
                var reference=new BlockReference(origin,id) { Rotation=0 };reference.SetDatabaseDefaults(db);Append(space,tr,reference);
                reference.CreateExtensionDictionary();var data=(DBDictionary)tr.GetObject(reference.ExtensionDictionary,OpenMode.ForWrite);
                var snapshot=new List<TypedValue>{new TypedValue((int)DxfCode.Text,"CantileverSlab")};
                snapshot.AddRange(CantileverDrawRecord.EncodeSnapshot(r,frame,p,elevation).Select(v=>new TypedValue((int)DxfCode.Real,v)));
                for(int i=0;i<4;i++) snapshot.Add(new TypedValue((int)DxfCode.Text,r.Layer(i)));
                WriteBeamSideRecord(tr,data,CantileverDrawRecord.BlockKey,snapshot.ToArray());
                var nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForWrite);
                WriteBeamSideRecord(tr,nod,CantileverDrawRecord.LastKey,CantileverDrawRecord.Encode(r).Select(v=>new TypedValue((int)DxfCode.Real,v)).ToArray());
                tr.Commit();return reference.ObjectId;
            }
        }
        // V0.5-only folded leader adapter. The shared node rules are unchanged.
        // BuildUpstandLabel supplies TEXT, phi symbol, slash and both LINE pieces.
        private static List<Entity> BuildCantileverLabel(Database db,Transaction tr,UpstandLabelLayout label,
            UpstandAnnotationLayout layout,string layer,Action<double,double> reserve,double? neighbour)
        {
            CheckBeamSideAnnotationLayer(db,tr,layer);
            var entities=BuildUpstandLabel(db,label,layout,"0",EnsureUpstandTextStyle(db,tr));
            try
            {
                var text=entities.OfType<DBText>().Single();text.Rotation=0;text.IsMirroredInX=false;text.IsMirroredInY=false;text.AdjustAlignment(db);
                var bounds=text.GeometricExtents;var elbow=label.Leaders.Last().B;
                double row=elbow.Y,gap=label.TextAnchor.Y-row;
                double rise=row+gap-bounds.MinPoint.Y;
                if(rise>0) { text.TransformBy(Matrix3d.Displacement(new Vector3d(0,rise,0)));bounds=text.GeometricExtents; }
                double end=label.Rightward?bounds.MaxPoint.X:bounds.MinPoint.X;
                if(bounds.MinPoint.Y<=row || (neighbour.HasValue&&row<neighbour.Value&&bounds.MaxPoint.Y>=neighbour.Value) ||
                    (label.Rightward?end-label.TextAnchor.X:label.TextAnchor.X-end)<=1e-8)
                    throw new InvalidOperationException("悬挑板规格文字空间不足，请人工调整标注。");
                // Preserve the diagonal segment; only the final horizontal end
                // follows measured TEXT width. No movement of the real anchor.
                var horizontal=entities.OfType<Line>().Last();
                horizontal.EndPoint=new Point3d(end,row,layout.Elevation);
                reserve(Math.Min(bounds.MinPoint.X,entities.OfType<Line>().Min(l=>Math.Min(l.StartPoint.X,l.EndPoint.X))),
                    Math.Max(bounds.MaxPoint.X,entities.OfType<Line>().Max(l=>Math.Max(l.StartPoint.X,l.EndPoint.X))));
                return entities;
            }
            catch { foreach(var entity in entities) entity.Dispose();throw; }
        }
        internal static CantileverDrawRequest ReadCantileverLast(Database db,string[] layers,Editor editor=null)
        {
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForRead);if(!nod.Contains(CantileverDrawRecord.LastKey)) return null;
                try
                {
                    var record=tr.GetObject(nod.GetAt(CantileverDrawRecord.LastKey),OpenMode.ForRead) as Xrecord;
                    if(record==null) throw new InvalidOperationException("记录不是Xrecord。");
                    using(var buffer=record.Data)
                    {
                        if(buffer==null) throw new InvalidOperationException("记录为空。");var values=buffer.AsArray();
                        if(values.Any(v=>v.TypeCode!=(int)DxfCode.Real)) throw new InvalidOperationException("记录类型错误。");
                        return CantileverDrawRecord.Decode(values.Select(v=>Convert.ToDouble(v.Value)).ToArray(),layers);
                    }
                }
                catch(System.Exception ex) { if(editor!=null) editor.WriteMessage("\n悬挑板成功参数未读取："+ex.Message);return null; }
            }
        }
    }
}
