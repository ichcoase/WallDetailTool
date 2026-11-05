using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using TransientManager=Autodesk.AutoCAD.GraphicsInterface.TransientManager;
using TransientDrawingMode=Autodesk.AutoCAD.GraphicsInterface.TransientDrawingMode;
using Autodesk.AutoCAD.Runtime;
using CadApplication=Autodesk.AutoCAD.ApplicationServices.Application;

namespace WallDetailTool
{
    public partial class Commands
    {
        private const string TopSlabBlockKey="WDT_TOP_SLAB_V1";
        [CommandMethod("WDTTOPSLAB",CommandFlags.Session)]
        public async void ShowTopSlab()
        {
            Document target=CadApplication.DocumentManager.MdiActiveDocument;if(target==null)return;
            try
            {
                int units=(int)target.Database.Insunits;
                if(units!=0&&units!=4)throw new InvalidOperationException("反坎带飘板第一版仅支持毫米或无单位图纸；本次不修改图纸单位。");
                var shared=UpstandPanelValues.ReadUserDefaults();var names=UpstandLayers.Read();
                string[] layers={names.Line,names.Dot,names.Text,names.Dimension};
                double[] advanced=AdvancedValues(shared);
                var drafts=new[]{ReadTopSlabLast(target.Database,TopSlabBase.Beam,shared),ReadTopSlabLast(target.Database,TopSlabBase.Slab,shared)};
                await CadApplication.DocumentManager.ExecuteInCommandContextAsync(_=>
                {
                    RunTopSlabDialog(target,units,drafts,advanced,layers);return Task.CompletedTask;
                },null);
            }
            catch(System.Exception ex) { target.Editor.WriteMessage("\n反坎带飘板未完成："+ex.Message); }
        }
        private static bool TopSlabTarget(Document doc,int units)
        { return ReferenceEquals(doc,CadApplication.DocumentManager.MdiActiveDocument)&&(int)doc.Database.Insunits==units; }
        private static void RunTopSlabDialog(Document target,int units,string[][] drafts,double[] advanced,string[] layers)
        {
            TopSlabGeometry geometry=null;TopSlabBase type=TopSlabBase.Beam;
            while(TopSlabTarget(target,units))
            {
                DialogResult result;double[] values=null;
                using(var form=new TopSlabForm(drafts,units))
                {
                    form.SetBase(type);form.Geometry=geometry;form.RefreshGeometry();
                    result=CadApplication.ShowModalDialog(form);drafts=form.Drafts();type=form.Base;
                    if(result==DialogResult.Retry)values=form.Values;
                }
                if(result!=DialogResult.Retry)return;
                try
                {
                    var selected=SelectTopSlabInput(target,units);
                    if(selected==null)continue;
                    geometry=selected.Geometry;
                }
                catch(System.Exception ex)
                {
                    if(!TopSlabTarget(target,units))return;
                    target.Editor.WriteMessage("\n轮廓识别失败："+ex.Message);
                    MessageBox.Show(ex.Message+"\n保留配筋草稿。","请重新选择轮廓");continue;
                }
                TopSlabRequest request;TopSlabLayout layout;
                try { request=new TopSlabRequest(target,geometry,type,units,values,advanced,layers);layout=TopSlabRebar.Calculate(request); }
                catch(System.Exception ex) { target.Editor.WriteMessage("\n参数未生成："+ex.Message);MessageBox.Show(ex.Message,"请调整参数");continue; }
                if(!request.Matches(CadApplication.DocumentManager.MdiActiveDocument,(int)target.Database.Insunits))return;
                target.Editor.Command("_.UNDO","_Begin");
                try { CreateTopSlabBlock(target.Database,request,layout); }
                finally { target.Editor.Command("_.UNDO","_End"); }
                target.Editor.WriteMessage("\n已生成反坎带飘板普通节点块；源轮廓保留。WDTTOPSLABINFO查看参数，BEDIT原生编辑；尚不支持参数驱动重生成。请人工审核构造。");return;
            }
        }
        private static TopSlabContourInput SelectTopSlabInput(Document target,int units)
        {
            while(TopSlabTarget(target,units))
            {
                var selection=target.Editor.GetSelection(new PromptSelectionOptions { MessageForAdding="\n选择完整顶部L形边界（闭合或仅底部开放；单个轻量多段线或一组LINE），回车结束：" });
                if(selection.Status!=PromptStatus.OK||!TopSlabTarget(target,units))return null;
                var ids=selection.Value.GetObjectIds();
                using(var tr=target.Database.TransactionManager.StartTransaction())foreach(var id in ids)
                {
                    var poly=tr.GetObject(id,OpenMode.ForRead) as Polyline;
                    if(poly!=null&&ids.Length!=1)throw new InvalidOperationException("请选择单个轻量多段线，或仅选择组成边界的LINE；不支持混选或多个多段线。");
                }
                double z;var source=ReadBeamSideSegments(target.Database,ids,new BeamSideUnitTransform(1),out z);
                var recognized=TopSlabRecognizer.RecognizeInput(source,z);
                TopSlabSelectionDisplay display=null;
                var choice=TopSlabContourConfirmation.Run(recognized,()=>TopSlabTarget(target,units)&&(display==null||display.Active),
                    ()=>display=new TopSlabSelectionDisplay(target,ids,recognized),()=>ConfirmTopSlabBottom(target));
                if(choice==TopSlabConfirmation.Reselect)continue;
                return choice==TopSlabConfirmation.Confirm?recognized:null;
            }
            return null;
        }
        private static TopSlabConfirmation ConfirmTopSlabBottom(Document target)
        {
            var options=new PromptKeywordOptions("\n虚线为反坎底部计算边界，确认生成？[确认/重选] <确认>：") { AppendKeywordsToMessage=false };
            options.Keywords.Add("Confirm","确认","确认");options.Keywords.Add("Reselect","重选","重选");
            options.Keywords.Default="Confirm";options.AllowNone=true;
            var result=target.Editor.GetKeywords(options);
            if(result.Status==PromptStatus.None)return TopSlabConfirmation.Confirm;
            if(result.Status!=PromptStatus.OK)return TopSlabConfirmation.Cancel;
            return result.StringResult=="Reselect"||result.StringResult=="重选"?TopSlabConfirmation.Reselect:TopSlabConfirmation.Confirm;
        }
        // No database append, source edits, layer/linetype writes or parameter writes.
        private sealed class TopSlabSelectionDisplay:IDisposable
        {
            private readonly Document document;
            private readonly ObjectId[] ids;
            private readonly List<Line> dashes=new List<Line>();
            private readonly TransientManager manager;
            private readonly IntegerCollection viewports=new IntegerCollection();
            private bool disposed;
            internal bool Active { get { return !disposed; } }
            internal TopSlabSelectionDisplay(Document document,ObjectId[] ids,TopSlabContourInput input)
            {
                this.document=document;this.ids=ids;manager=TransientManager.CurrentTransientManager;
                try
                {
                    CadApplication.DocumentManager.DocumentToBeDeactivated+=DocumentLeaving;
                    CadApplication.DocumentManager.DocumentToBeDestroyed+=DocumentLeaving;
                    HighlightTopSlab(document.Database,ids,true);
                    foreach(var dash in input.BottomDashes())
                    {
                        var line=new Line(new Point3d(dash.A.X,dash.A.Y,input.Geometry.Elevation),new Point3d(dash.B.X,dash.B.Y,input.Geometry.Elevation)) { ColorIndex=4 };
                        dashes.Add(line);
                        if(!manager.AddTransient(line,TransientDrawingMode.DirectShortTerm,128,viewports))
                            throw new InvalidOperationException("无法显示临时虚线底边，本次不生成；请重新选择。");
                    }
                    document.Editor.UpdateScreen();
                }
                catch { Dispose();throw; }
            }
            private void DocumentLeaving(object sender,DocumentCollectionEventArgs e)
            { if(ReferenceEquals(e.Document,document))Dispose(); }
            public void Dispose()
            {
                if(disposed)return;disposed=true;
                CadApplication.DocumentManager.DocumentToBeDeactivated-=DocumentLeaving;
                CadApplication.DocumentManager.DocumentToBeDestroyed-=DocumentLeaving;
                foreach(var line in dashes)
                {
                    try { manager.EraseTransient(line,viewports); }
                    catch(System.Exception ex) { System.Diagnostics.Trace.WriteLine("TopSlab transient cleanup: "+ex.Message); }
                    finally { line.Dispose(); }
                }
                try { HighlightTopSlab(document.Database,ids,false); }
                catch(System.Exception ex) { System.Diagnostics.Trace.WriteLine("TopSlab highlight cleanup: "+ex.Message); }
            }
        }
        private static void HighlightTopSlab(Database db,ObjectId[] ids,bool on)
        {
            using(var tr=db.TransactionManager.StartTransaction())foreach(var id in ids)
            {
                if(id.IsNull||id.IsErased)continue;var entity=tr.GetObject(id,OpenMode.ForRead) as Entity;
                if(entity!=null) { if(on)entity.Highlight();else entity.Unhighlight(); }
            }
        }
        private static string[] ReadTopSlabLast(Database db,TopSlabBase type,UpstandPanelValues shared)
        {
            // Reuse existing beam/slab trial meanings; saved values override these.
            double[] values=TopSlabForm.TrialValues(type,shared.LineDiameter,shared.LineSpacing,shared.DotDiameter,shared.DotSpacing,shared.Cover,shared.LineDisplayWidth);
            bool innerRecorded=false;
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForRead);string key=TopSlabRequest.LastKey(type);
                if(nod.Contains(key))
                {
                    var record=tr.GetObject(nod.GetAt(key),OpenMode.ForRead) as Xrecord;
                    if(record!=null)using(var buffer=record.Data)if(buffer!=null)
                    {
                        var a=buffer.AsArray();
                        if(a.Length==23&&(Convert.ToDouble(a[0].Value)==1||Convert.ToDouble(a[0].Value)==2||Convert.ToDouble(a[0].Value)==3)&&Convert.ToDouble(a[1].Value)==(int)type)
                            for(int i=0;i<16;i++) { double v=Convert.ToDouble(a[i+2].Value);if(!double.IsNaN(v)&&!double.IsInfinity(v)&&v>0) { values[i]=v;if(i==13)innerRecorded=true; } }
                    }
                }
            }
            if(!innerRecorded)values[13]=values[12];
            return values.Select(v=>v>0?v.ToString("g",CultureInfo.CurrentCulture):"").ToArray();
        }
        internal static ObjectId CreateTopSlabBlock(Database db,TopSlabRequest r,TopSlabLayout layout)
        {
            var g=r.Geometry;
            using(var tr=db.TransactionManager.StartTransaction())
            {
                for(int i=0;i<4;i++)EnsureLayer(db,tr,r.Layer(i),(short)(i==0?1:i==1?4:i==2?71:70));
                var layerTable=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
                for(int i=0;i<4;i++)
                {
                    var layer=(LayerTableRecord)tr.GetObject(layerTable[r.Layer(i)],OpenMode.ForRead);
                    if(layer.IsLocked)throw new InvalidOperationException("目标图层已锁定："+r.Layer(i));
                }
                var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);
                var block=new BlockTableRecord { Name="WDT-TOP-SLAB-"+Guid.NewGuid().ToString("N") };table.Add(block);tr.AddNewlyCreatedDBObject(block,true);
                // Geometry is WCS; use zero block origin to avoid duplicate translation.
                foreach(var vertices in layout.Bars)
                {
                    var poly=new Polyline(vertices.Count) { Layer=r.Layer(0),Elevation=g.Elevation,ColorIndex=256 };
                    for(int i=0;i<vertices.Count;i++) { var v=vertices[i];poly.AddVertexAt(i,new Point2d(v.X,v.Y),v.Bulge,r.A(1),r.A(1)); }
                    Append(block,tr,poly);
                }
                foreach(var p in layout.WallDots.Concat(layout.SlabDots))Append(block,tr,UpstandDot(p,g.Elevation,r.A(2),r.Layer(1)));
                var annotation=new UpstandAnnotationLayout { TextHeight=r.A(4),Elevation=g.Elevation };
                var sizes=MeasureTopSlabText(db,tr,r);
                var dimSizes=MeasureTopSlabDimensions(db,tr,r);
                var plan=TopSlabAnnotationGeometry.Calculate(r,layout,sizes,dimSizes);
                foreach(var label in plan.Labels)
                {
                    var entities=BuildUpstandLabel(db,label,annotation,"0",EnsureUpstandTextStyle(db,tr));
                    try
                    {
                        // Instance-only colors recover the accepted defaults even if
                        // an earlier V1 created the shared layer with color 7.
                        foreach(var entity in entities) { entity.Layer=r.Layer(2);entity.ColorIndex=71; }
                        AlignTopSlabText(db,label,entities);
                        foreach(var entity in entities)Append(block,tr,entity);
                    }
                    finally { foreach(var entity in entities)if(entity.ObjectId.IsNull)entity.Dispose(); }
                }
                foreach(var d in plan.Dimensions)
                {
                    var entities=BuildBeamSideDimension(db,tr,d.A,d.B,d.Line,g.Elevation,1,r.Layer(3));
                    try
                    {
                        foreach(var entity in entities)
                        {
                            entity.Layer=r.Layer(3);entity.ColorIndex=70;
                            var dimension=entity as Dimension;
                            if(dimension!=null)
                            {
                                dimension.Dimjust=0;dimension.Dimtix=true;
                                dimension.Dimclrd=Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci,70);
                                dimension.Dimclre=Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci,70);
                                dimension.Dimclrt=Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci,7);
                            }
                            Append(block,tr,entity);
                        }
                    }
                    finally { foreach(var entity in entities)if(entity.ObjectId.IsNull)entity.Dispose(); }
                }
                var reference=new BlockReference(Point3d.Origin,block.ObjectId);Append((BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite),tr,reference);
                reference.CreateExtensionDictionary();var data=(DBDictionary)tr.GetObject(reference.ExtensionDictionary,OpenMode.ForWrite);
                var snapshot=r.Record(layout.EffectiveA).Concat(new[]{g.OuterX,g.Bottom,g.Top,g.WallThickness,g.SlabThickness,g.Projection,g.Elevation,(double)g.Direction}).Select(v=>new TypedValue((int)DxfCode.Real,v)).ToList();
                snapshot.Insert(0,new TypedValue((int)DxfCode.Text,"UpstandTopSlab"));
                for(int i=0;i<4;i++)snapshot.Add(new TypedValue((int)DxfCode.Text,r.Layer(i)));
                WriteBeamSideRecord(tr,data,TopSlabBlockKey,snapshot.ToArray());
                var nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForWrite);
                WriteBeamSideRecord(tr,nod,TopSlabRequest.LastKey(r.Base),r.Record().Select(v=>new TypedValue((int)DxfCode.Real,v)).ToArray());
                tr.Commit();return reference.ObjectId;
            }
        }
        private static List<TopSlabTextSize> MeasureTopSlabText(Database db,Transaction tr,TopSlabRequest r)
        {
            return Enumerable.Range(0,5).Select(i=>MeasureTopSlabString(db,tr,TopSlabAnnotationGeometry.Spec(r,i),r.A(4))).ToList();
        }
        private static List<TopSlabTextSize> MeasureTopSlabDimensions(Database db,Transaction tr,TopSlabRequest r)
        {
            var g=r.Geometry;
            return new[]{g.WallThickness,g.Projection,g.Height,g.SlabThickness}.Select(v=>
                MeasureTopSlabString(db,tr,v.ToString("0",CultureInfo.InvariantCulture),AnnotationDimensionTextHeight*AnnotationDimensionScale)).ToList();
        }
        private static TopSlabTextSize MeasureTopSlabString(Database db,Transaction tr,string value,double height)
        {
            var label=new UpstandLabelLayout { Text=value,Rightward=true,TextAnchor=new AnnotationPoint(0,0) };
            var entities=BuildUpstandLabel(db,label,new UpstandAnnotationLayout { TextHeight=height },"0",EnsureUpstandTextStyle(db,tr));
            try
            {
                var text=entities.OfType<DBText>().Single();text.AdjustAlignment(db);var bounds=text.GeometricExtents;
                return new TopSlabTextSize(bounds.MaxPoint.X-bounds.MinPoint.X,bounds.MaxPoint.Y-bounds.MinPoint.Y);
            }
            finally { foreach(var entity in entities)entity.Dispose(); }
        }
        private static void AlignTopSlabText(Database db,UpstandLabelLayout label,List<Entity> entities)
        {
            var text=entities.OfType<DBText>().Single();text.Rotation=0;text.IsMirroredInX=false;text.IsMirroredInY=false;text.AdjustAlignment(db);
            var bounds=text.GeometricExtents;
            double dx=label.TextAnchor.X-(label.Rightward?bounds.MinPoint.X:bounds.MaxPoint.X);
            double dy=label.TextAnchor.Y-bounds.MinPoint.Y;
            text.TransformBy(Matrix3d.Displacement(new Vector3d(dx,dy,0)));bounds=text.GeometricExtents;
            var last=entities.OfType<Line>().Last();
            last.EndPoint=new Point3d(label.Rightward?bounds.MaxPoint.X:bounds.MinPoint.X,last.EndPoint.Y,last.EndPoint.Z);
        }
        [CommandMethod("WDTTOPSLABINFO")]
        public void TopSlabInfo()
        {
            var doc=CadApplication.DocumentManager.MdiActiveDocument;if(doc==null)return;
            var pick=doc.Editor.GetEntity("\n选择反坎带飘板节点块：");if(pick.Status!=PromptStatus.OK)return;
            using(var tr=doc.Database.TransactionManager.StartTransaction())
            {
                var block=tr.GetObject(pick.ObjectId,OpenMode.ForRead) as BlockReference;
                if(block==null||block.ExtensionDictionary.IsNull) { doc.Editor.WriteMessage("\n不是反坎带飘板节点块。");return; }
                var data=(DBDictionary)tr.GetObject(block.ExtensionDictionary,OpenMode.ForRead);
                if(!data.Contains(TopSlabBlockKey)) { doc.Editor.WriteMessage("\n不是反坎带飘板节点块。");return; }
                var record=(Xrecord)tr.GetObject(data.GetAt(TopSlabBlockKey),OpenMode.ForRead);
                using(var buffer=record.Data)
                {
                    var a=buffer.AsArray();if(a.Length!=36) { doc.Editor.WriteMessage("\n节点记录不完整。");return; }
                    doc.Editor.WriteMessage("\n反坎带飘板，底部"+(Convert.ToDouble(a[2].Value)==1?"梁上":"板上")+"；以下为生成时参数，原生编辑后不自动反算：");
                    for(int i=0;i<16;i++)doc.Editor.WriteMessage("\n"+TopSlabForm.Names[i]+"="+(i==10&&Convert.ToDouble(a[1].Value)>=2?(object)("自动，生成值 "+a[i+3].Value+" mm"):a[i+3].Value));
                }
            }
        }
    }
}
