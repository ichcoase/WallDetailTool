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
        [CommandMethod("WDTBEAMSIDE",CommandFlags.Session)]
        public void ShowBeamSideForm() { OpenBeamSideForm(); }
        private static async void OpenBeamSideForm()
        {
            Document target=CadApplication.DocumentManager.MdiActiveDocument;
            if(target==null) return;
            try
            {
                var shared=UpstandPanelValues.ReadUserDefaults();var names=UpstandLayers.Read();
                string[] layers={names.Line,names.Dot,names.Text,names.Dimension};
                var saved=ReadBeamSideLast(target.Database,layers,target.Editor);
                int unitCode=(int)target.Database.Insunits;double normalScale=0;
                try { normalScale=MillimetresToDrawingUnits(target.Database); } catch(System.Exception) { /* Explicit mm override remains available. */ }
                var state=new BeamSideDrawState(saved,AdvancedValues(shared),layers,
                    new[]{shared.LineDiameter,shared.LineSpacing,shared.DotDiameter,shared.DotSpacing});
                BeamSideDrawRequest request;
                bool choose=target.Database.Insunits!=UnitsValue.Undefined&&target.Database.Insunits!=UnitsValue.Millimeters;
                using(var form=new BeamSideDrawForm(state,BeamSideUnitName(target.Database.Insunits),unitCode,normalScale,choose))
                {
                    if(CadApplication.ShowModalDialog(form)!=DialogResult.OK) return;
                    request=form.Request;
                }
                if(request==null) return;
                await CadApplication.DocumentManager.ExecuteInCommandContextAsync(_=>
                {
                    Document active=CadApplication.DocumentManager.MdiActiveDocument;
                    if(active==null) return Task.CompletedTask;
                    if(!BeamSideDrawRequest.TargetMatches(target,active,request.SourceUnitCode,(int)active.Database.Insunits))
                        active.Editor.WriteMessage("\n目标图纸或单位已切换，本次梁侧挂板请求已放弃，请重新打开窗口。");
                    else ExecuteBeamSideRequest(target,request);
                    return Task.CompletedTask;
                },null);
            }
            catch(System.Exception ex)
            { if(ReferenceEquals(target,CadApplication.DocumentManager.MdiActiveDocument)) target.Editor.WriteMessage("\n梁侧挂板未启动："+ex.Message); }
        }
        private static string BeamSideUnitName(UnitsValue unit)
        {
            switch(unit)
            {
                case UnitsValue.Undefined:return "未指定（按毫米）";
                case UnitsValue.Millimeters:return "毫米";
                case UnitsValue.Centimeters:return "厘米";
                case UnitsValue.Meters:return "米";
                case UnitsValue.Inches:return "英寸";
                case UnitsValue.Feet:return "英尺";
                default:return unit.ToString();
            }
        }
        private static void ExecuteBeamSideRequest(Document doc,BeamSideDrawRequest request)
        {
            Editor ed=doc.Editor;
            bool committed=false;
            try
            {
                ed.WriteMessage("\n本次单位解释：1 mm = "+request.DrawingUnitsPerMillimetre.ToString("g")+" CAD 单位"+
                    (request.MillimetreOverride?"（已明确覆盖解释）":"")+"；不修改 INSUNITS。");
                var geometry=SelectBeamSideGeometry(doc,request);
                if(geometry==null) return;
                var layout=BeamSideRebarGeometry.Calculate(geometry,request.Parameters);
                ed.WriteMessage("\n"+layout.BoundaryMessage);
                if(!BeamSideDrawRequest.TargetMatches(doc,CadApplication.DocumentManager.MdiActiveDocument,request.SourceUnitCode,(int)doc.Database.Insunits))
                    throw new InvalidOperationException("目标图纸或单位已改变，请重新启动绘制。");
                var failures=new List<string>();
                ed.Command("_.UNDO","_Begin");
                try { CreateBeamSideBlock(doc.Database,request,layout,failures);committed=true; }
                finally { ed.Command("_.UNDO","_End"); }
                ed.WriteMessage("\n已生成梁侧挂板普通块；原轮廓只读，成功参数已按本类型保存。梁内构造请人工审核。");
                foreach(string failure in failures) ed.WriteMessage("\n未完成："+failure);
            }
            catch(System.Exception ex) { ed.WriteMessage(committed?"\n梁侧挂板已提交，但后续操作失败："+ex.Message:"\n未生成梁侧挂板："+ex.Message); }
        }
        internal static List<ContourSegment> ReadBeamSideSegments(Database db,IEnumerable<ObjectId> ids,
            BeamSideUnitTransform unit,out double elevation)
        {
            var segments=new List<ContourSegment>();double? z=null;
            Action<Point3d,Point3d> add=(a,b)=>
            {
                double za=unit.LengthToMillimetres(a.Z),zb=unit.LengthToMillimetres(b.Z);
                if(Math.Abs(za-zb)>1e-8||(z.HasValue&&Math.Abs(z.Value-za)>1e-8))
                    throw new InvalidOperationException("选中图元不在同一 WCS 二维平面。");
                z=za;segments.Add(new ContourSegment(unit.ToMillimetres(new ContourPoint(a.X,a.Y)),unit.ToMillimetres(new ContourPoint(b.X,b.Y))));
            };
            using(var tr=db.TransactionManager.StartTransaction())
            {
                foreach(var id in ids)
                {
                    var entity=tr.GetObject(id,OpenMode.ForRead) as Entity;var line=entity as Line;
                    if(line!=null) { add(line.StartPoint,line.EndPoint);continue; }
                    var poly=entity as Polyline;
                    if(poly==null) throw new InvalidOperationException("仅支持 LINE / LWPOLYLINE；请排除块、文字及其他图元。");
                    if(Math.Abs(poly.Normal.X)>1e-8||Math.Abs(poly.Normal.Y)>1e-8||poly.Normal.Z<1-1e-8)
                        throw new InvalidOperationException("多段线须位于 WCS XY 平面，法向为正 Z。");
                    for(int i=0;i<(poly.Closed?poly.NumberOfVertices:poly.NumberOfVertices-1);i++)
                    {
                        double bulge=poly.GetBulgeAt(i);BeamSideMath.Finite(bulge);
                        if(Math.Abs(bulge)>1e-12) throw new InvalidOperationException("轮廓含圆弧，仅支持正交直线段。");
                        add(poly.GetPoint3dAt(i),poly.GetPoint3dAt((i+1)%poly.NumberOfVertices));
                    }
                }
            }
            elevation=z??0;return segments;
        }
        private static BeamSideGeometry SelectBeamSideGeometry(Document doc,BeamSideDrawRequest r)
        {
            if(r.TwoPointRectangle) { var rectangle=PickNodeRectangle(doc,r.SourceUnitCode,r.DrawingUnitsPerMillimetre);return rectangle==null?null:rectangle.Side(r.Direction); }
            var ed=doc.Editor;
            var options=new PromptSelectionOptions { MessageForAdding=r.FullSection?"\n选择相连的完整梁/块体截面：":"\n选择局部矩形或向梁开口 U（源图元只读）：" };
            var selected=ed.GetSelection(options);if(selected.Status!=PromptStatus.OK) return null;
            var unit=new BeamSideUnitTransform(r.DrawingUnitsPerMillimetre);double z;
            var source=ReadBeamSideSegments(doc.Database,selected.Value.GetObjectIds(),unit,out z);
            var recognition=BeamSideContourRecognizer.Recognize(source,z,r.Direction,r.FullSection);
            if(recognition.SplitRegions.Count>0)
            {
                var point=ed.GetPoint("\n完整等高外框没有连接缝，请明确点取梁与块体的连接面 X：");
                if(point.Status!=PromptStatus.OK) return null;
                Point3d wcs=point.Value.TransformBy(ed.CurrentUserCoordinateSystem);
                var mm=unit.ToMillimetres(new ContourPoint(wcs.X,wcs.Y));
                return recognition.SplitRegions[0].Resolve(mm.X);
            }
            if(recognition.Candidates.Count==1) return recognition.Candidates[0];
            var pick=ed.GetPoint("\n存在多个有效候选，请点选目标块体内部：");
            if(pick.Status!=PromptStatus.OK) return null;
            Point3d world=pick.Value.TransformBy(ed.CurrentUserCoordinateSystem);
            var position=unit.ToMillimetres(new ContourPoint(world.X,world.Y));
            // Recognition is pure; recheck the same source before resolving the selection.
            return BeamSideContourRecognizer.Recognize(source,z,r.Direction,r.FullSection).SelectCandidate(position.X,position.Y);
        }
        internal static List<Entity> BuildBeamSideRebars(BeamSideRebarLayout layout,BeamSideDrawRequest request)
        {
            var unit=new BeamSideUnitTransform(request.DrawingUnitsPerMillimetre);var result=new List<Entity>();
            try
            {
                double z=unit.LengthToDrawing(layout.Elevation),width=unit.LengthToDrawing(layout.LineWidth);
                var poly=new Polyline(layout.Line.Count) { Elevation=z,Layer=request.Layer(0),ColorIndex=256 };
                result.Add(poly);
                for(int i=0;i<layout.Line.Count;i++)
                {
                    var v=layout.Line[i];var point=unit.ToDrawing(v.Point);
                    poly.AddVertexAt(i,new Point2d(point.X,point.Y),v.Bulge,width,width);
                }
                foreach(var dot in layout.Dots)
                    result.Add(UpstandDot(unit.ToDrawing(dot.Center),z,unit.LengthToDrawing(layout.DotDisplayDiameter),request.Layer(1)));
                return result;
            }
            catch { foreach(var e in result) e.Dispose();throw; }
        }
        // No editor commands or independent writes: all geometry, styles, optional
        // annotations, block snapshot and LAST record share the parent transaction.
        internal static ObjectId CreateBeamSideBlock(Database db,BeamSideDrawRequest request,BeamSideRebarLayout layout,List<string> failures)
        {
            if(!ReferenceEquals(request.Parameters,layout.Parameters)||request.Direction!=layout.Geometry.Direction||
                request.FullSection!=(layout.Geometry.InputKind==BeamSideInputKind.FullSection)||(int)db.Insunits!=request.SourceUnitCode)
                throw new InvalidOperationException("计算结果与请求或当前图纸单位不一致。");
            var unit=new BeamSideUnitTransform(request.DrawingUnitsPerMillimetre);
            var basePoint=unit.ToDrawing(layout.Geometry.BasePoint);double z=unit.LengthToDrawing(layout.Elevation);
            var local=Matrix3d.Displacement(new Vector3d(-basePoint.X,-basePoint.Y,-z));
            UpstandAnnotationLayout annotation=null;
            try { annotation=BeamSideAnnotationGeometry.Calculate(layout,unit.Scale); }
            catch(System.Exception ex) { failures.Add("全部自动标注布局："+ex.Message); }
            using(var tr=db.TransactionManager.StartTransaction())
            {
                for(int i=0;i<2;i++)
                {
                    EnsureLayer(db,tr,request.Layer(i),(short)(i+1));
                    var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
                    var layer=(LayerTableRecord)tr.GetObject(layers[request.Layer(i)],OpenMode.ForRead);
                    if(layer.IsLocked) throw new InvalidOperationException("目标钢筋图层已锁定："+request.Layer(i));
                    if(layer.IsFrozen||layer.IsOff) failures.Add("目标图层冻结或关闭，生成后可能不可见："+request.Layer(i));
                }
                var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);
                var block=new BlockTableRecord { Name="WDT-BEAM-SIDE-"+Guid.NewGuid().ToString("N") };
                ObjectId blockId=table.Add(block);tr.AddNewlyCreatedDBObject(block,true);
                table.DowngradeOpen();
                var entities=BuildBeamSideRebars(layout,request);
                try { foreach(var entity in entities) { entity.TransformBy(local);Append(block,tr,entity); } }
                finally { foreach(var entity in entities) if(entity.ObjectId.IsNull) entity.Dispose(); }
                block.DowngradeOpen();
                if(annotation!=null)
                {
                    bool dimensionRight=request.Direction==BeamSideDirection.Right;
                    Action<double,double> reserveRight=(left,right)=> {
                        if(request.Parameters.AnnotationRight==dimensionRight)
                            annotation.HeightLine=BeamSideAnnotationGeometry.HeightLineOutsideLabels(annotation,dimensionRight?right:left,unit.Scale,dimensionRight);
                    };
                    TryAppendAnnotation(db,blockId,local,request.Layer(2),71,"线筋标注",failures,
                        optional=>BuildBeamSideLabel(db,optional,annotation.LineLabel,annotation,request.Layer(2),reserveRight));
                    TryAppendAnnotation(db,blockId,local,request.Layer(2),71,"点筋标注",failures,
                        optional=>BuildBeamSideLabel(db,optional,annotation.DotLabel,annotation,request.Layer(2),reserveRight));
                    TryAppendAnnotation(db,blockId,local,request.Layer(3),70,"宽度尺寸",failures,
                        optional=>BuildBeamSideDimension(db,optional,annotation.WidthA,annotation.WidthB,annotation.WidthLine,annotation.Elevation,unit.Scale,request.Layer(3)));
                    TryAppendAnnotation(db,blockId,local,request.Layer(3),70,"高度尺寸",failures,
                        optional=>BuildBeamSideDimension(db,optional,annotation.HeightA,annotation.HeightB,annotation.HeightLine,annotation.Elevation,unit.Scale,request.Layer(3)));
                }
                AppendEndLengthNotes(db,blockId,local,request.Layer(2),EndLengthAnnotationGeometry.BeamSide(layout,unit.Scale),z,failures);
                var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);
                var reference=new BlockReference(new Point3d(basePoint.X,basePoint.Y,z),blockId) { Rotation=0 };
                reference.SetDatabaseDefaults(db);Append(space,tr,reference);
                reference.CreateExtensionDictionary();
                var data=(DBDictionary)tr.GetObject(reference.ExtensionDictionary,OpenMode.ForWrite);
                var snapshot=new List<TypedValue>{new TypedValue((int)DxfCode.Text,BeamSideRebarLayout.NodeType)};
                snapshot.AddRange(BeamSideDrawRecord.EncodeSnapshot(request,layout.Geometry).Select(v=>new TypedValue((int)DxfCode.Real,v)));
                snapshot.AddRange(request.Layers.Select(v=>new TypedValue((int)DxfCode.Text,v)));
                WriteBeamSideRecord(tr,data,BeamSideDrawRecord.BlockKey,snapshot.ToArray());
                var nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForWrite);
                WriteBeamSideRecord(tr,nod,BeamSideDrawRecord.LastKey,BeamSideDrawRecord.Encode(request).Select(v=>new TypedValue((int)DxfCode.Real,v)).ToArray());
                tr.Commit();return reference.ObjectId;
            }
        }
        private static void CheckBeamSideAnnotationLayer(Database db,Transaction tr,string name)
        {
            var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
            var layer=(LayerTableRecord)tr.GetObject(layers[name],OpenMode.ForRead);
            if(layer.IsLocked) throw new InvalidOperationException("标注目标图层已锁定："+name);
        }
        private static List<Entity> BuildBeamSideLabel(Database db,Transaction tr,UpstandLabelLayout label,UpstandAnnotationLayout annotation,string layer,Action<double,double> reserveRight)
        {
            var other=ReferenceEquals(label,annotation.LineLabel)?annotation.DotLabel:annotation.LineLabel;
            return BuildHorizontalNodeLabel(db,tr,label,annotation,layer,reserveRight,other.Leaders[0].A.Y);
        }
        // Shared horizontal TEXT/leader adapter. Caller supplies an optional
        // neighbouring row; world-positive glyphs and measured width stay here.
        private static List<Entity> BuildHorizontalNodeLabel(Database db,Transaction tr,UpstandLabelLayout label,UpstandAnnotationLayout annotation,string layer,Action<double,double> reserveRight,double? neighbourY=null)
        {
            CheckBeamSideAnnotationLayer(db,tr,layer);
            var entities=BuildUpstandLabel(db,label,annotation,"0",EnsureUpstandTextStyle(db,tr));
            try
            {
                foreach(var item in entities.OfType<DBText>()) { item.Rotation=0;item.IsMirroredInX=false;item.IsMirroredInY=false; }
                var anchor=label.Leaders[0].A;
                var text=entities.OfType<DBText>().Single();text.AdjustAlignment(db);
                var bounds=text.GeometricExtents;
                // Glyphs may descend below their baseline. Place their actual
                // lower bound above the line before checking the other row.
                double clearance=label.TextAnchor.Y-anchor.Y;
                double shift=anchor.Y+clearance-bounds.MinPoint.Y;
                if(shift>0) { text.TransformBy(Matrix3d.Displacement(new Vector3d(0,shift,0)));bounds=text.GeometricExtents; }
                double end=label.Rightward?bounds.MaxPoint.X:bounds.MinPoint.X;
                if(bounds.MinPoint.Y<=anchor.Y || (neighbourY.HasValue&&anchor.Y<neighbourY.Value&&bounds.MaxPoint.Y>=neighbourY.Value) ||
                    (label.Rightward?end-label.TextAnchor.X:label.TextAnchor.X-end)<=1e-8)
                    throw new InvalidOperationException("实际 TEXT 范围不能满足水平引线及行间避让，请人工标注。");
                // Keep the shared builder's intersection slash, but replace its
                // leader pieces with one horizontal LINE to measured TEXT extent.
                foreach(var line in entities.OfType<Line>().ToArray()) { entities.Remove(line);line.Dispose(); }
                entities.Add(new Line(new Point3d(anchor.X,anchor.Y,annotation.Elevation),
                    new Point3d(end,anchor.Y,annotation.Elevation)) { Layer="0",ColorIndex=256 });
                reserveRight(Math.Min(bounds.MinPoint.X,Math.Min(anchor.X,end)),Math.Max(bounds.MaxPoint.X,Math.Max(anchor.X,end)));
                return entities;
            }
            catch { foreach(var entity in entities) entity.Dispose();throw; }
        }
        private static List<Entity> BuildBeamSideDimension(Database db,Transaction tr,AnnotationPoint a,AnnotationPoint b,AnnotationPoint line,double z,double scale,string layer)
        {
            CheckBeamSideAnnotationLayer(db,tr,layer);
            ObjectId text=EnsureUpstandTextStyle(db,tr),arrow=BeamSideArrow(db,tr);
            ObjectId style=EnsureUpstandDimensionStyle(db,tr,text,arrow);
            var dimension=UpstandDimension(db,a,b,line,z,"0",text,style);
            dimension.Dimscale=AnnotationDimensionScale*scale;dimension.Dimlfac=1/scale;
            return new List<Entity>{dimension};
        }
        private static ObjectId BeamSideArrow(Database db,Transaction tr)
        {
            var styles=(DimStyleTable)tr.GetObject(db.DimStyleTableId,OpenMode.ForRead);
            if(styles.Has(AnnotationDimensionStyle)) return ObjectId.Null;
            var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
            if(blocks.Has("_ARCHTICK")) return blocks["_ARCHTICK"];
            const string fallback="WDT-ARCHTICK";
            if(blocks.Has(fallback)) return blocks[fallback];
            // A standard diagonal architectural mark, created entirely inside the
            // optional transaction. Never SETVAR/DIMBLK or replace a native arrow.
            blocks.UpgradeOpen();var block=new BlockTableRecord { Name=fallback };
            ObjectId id=blocks.Add(block);tr.AddNewlyCreatedDBObject(block,true);
            var tick=new Polyline(2) { Layer="0",ColorIndex=0 };
            tick.AddVertexAt(0,new Point2d(-.5,-.5),0,.12,.12);tick.AddVertexAt(1,new Point2d(.5,.5),0,.12,.12);
            Append(block,tr,tick);return id;
        }
        private static void WriteBeamSideRecord(Transaction tr,DBDictionary dictionary,string key,TypedValue[] values)
        {
            Xrecord record;
            if(dictionary.Contains(key)) record=(Xrecord)tr.GetObject(dictionary.GetAt(key),OpenMode.ForWrite);
            else { record=new Xrecord();dictionary.SetAt(key,record);tr.AddNewlyCreatedDBObject(record,true); }
            using(var buffer=new ResultBuffer(values)) record.Data=buffer;
        }
        internal static BeamSideDrawRequest ReadBeamSideLast(Database db,string[] layers,Editor editor=null)
        {
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForRead);
                if(!nod.Contains(BeamSideDrawRecord.LastKey)) return null;
                try
                {
                    var record=tr.GetObject(nod.GetAt(BeamSideDrawRecord.LastKey),OpenMode.ForRead) as Xrecord;
                    if(record==null) throw new InvalidOperationException("记录不是 Xrecord。");
                    using(var buffer=record.Data)
                    {
                        if(buffer==null) throw new InvalidOperationException("记录为空。");
                        var values=buffer.AsArray();
                        if(values.Any(v=>v.TypeCode!=(int)DxfCode.Real)) throw new InvalidOperationException("记录数值类型无效。");
                        return BeamSideDrawRecord.Decode(values.Select(v=>Convert.ToDouble(v.Value)).ToArray(),layers);
                    }
                }
                catch(System.Exception ex) { if(editor!=null) editor.WriteMessage("\n本类型成功参数未读取，将使用首次值："+ex.Message);return null; }
            }
        }
    }
}
