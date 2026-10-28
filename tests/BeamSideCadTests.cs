using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using WallDetailTool;
using CadApplication=Autodesk.AutoCAD.ApplicationServices.Application;

// Test-only assembly, not part of the plugin. Refuses any document except the
// expressly named disposable copy; side databases and saved DWGs are new files.
public static class BeamSideCadTests
{
    static int checks;
    static readonly string[] Layers={"WDT-REBAR-LINE","WDT-REBAR-DOT","S-TEXT","s-floor_de_dim"};
    static readonly string[] OtherKeys={"WDT_UPSTAND_LAST","WDT_BEAM_HANGING_LAST","WDT_SLAB_UPSTAND_LAST","WDT_SLAB_HANGING_LAST"};
    static void Check(bool ok,string message) { checks++;if(!ok)throw new InvalidOperationException(message); }
    static void Near(double a,double b) { Check(Math.Abs(a-b)<1e-7,"coordinate/width mismatch "+a+" / "+b); }
    [CommandMethod("WDTBEAMSIDEAUTOTEST")]
    public static void Run()
    {
        var doc=CadApplication.DocumentManager.MdiActiveDocument;
        if(doc==null||!String.Equals(Path.GetFileName(doc.Name),"V04_CAD_Copy.dwg",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only V04_CAD_Copy.dwg may run this disposable fixture.");
        try
        {
            foreach(var direction in new[]{BeamSideDirection.Left,BeamSideDirection.Right})
            foreach(bool full in new[]{false,true}) foreach(bool top in new[]{false,true}) foreach(bool bottom in new[]{false,true})
            foreach(double scale in new[]{1.0,.001}) foreach(bool annotationRight in new[]{false,true})
                TestDatabase(direction,full,top,bottom,scale,annotationRight);
            foreach(var direction in new[]{BeamSideDirection.Left,BeamSideDirection.Right})
            foreach(bool side in new[]{false,true}) foreach(double width in new[]{100.0,600.0})
                TestDatabase(direction,false,false,true,.001,side,width);
            var request=new BeamSideDrawRequest(BeamSideDirection.Right,false,new BeamSideRebarParameters(400,100),1,(int)doc.Database.Insunits,
                doc.Database.Insunits!=UnitsValue.Millimeters&&doc.Database.Insunits!=UnitsValue.Undefined,Layers);
            var layout=BeamSideRebarGeometry.Calculate(Geometry(BeamSideDirection.Right,false),request.Parameters);
            ObjectId id;
            doc.Editor.Command("_.UNDO","_Begin");
            try { id=Commands.CreateBeamSideBlock(doc.Database,request,layout,new List<string>()); }
            finally { doc.Editor.Command("_.UNDO","_End"); }
            doc.Editor.Command("_.U");Check(id.IsErased,"one UNDO removes generated block");
            doc.Editor.Command("_.REDO");Check(!id.IsErased,"one REDO restores generated block");
            doc.Editor.WriteMessage("\nBEAM_SIDE_CAD_PASS checks="+checks);
        }
        catch(System.Exception ex) { doc.Editor.WriteMessage("\nBEAM_SIDE_CAD_FAIL "+ex.Message); }
    }
    static BeamSideGeometry Geometry(BeamSideDirection direction,bool full,double width=300)
    {
        double x=-900,y=1700;
        var block=direction==BeamSideDirection.Right?new BeamSideRect(x,x+width,y,y+600):new BeamSideRect(x-width,x,y,y+600);
        var beam=!full?null:direction==BeamSideDirection.Right?new BeamSideRect(x-500,x,y-200,y+800):new BeamSideRect(x,x+500,y-200,y+800);
        return new BeamSideGeometry(block,beam,direction,full?BeamSideInputKind.FullSection:BeamSideInputKind.LocalRectangle,-37);
    }
    static void Lock(Database db,string layer,bool locked)
    {
        using(var tr=db.TransactionManager.StartTransaction())
        { var table=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);var item=(LayerTableRecord)tr.GetObject(table[layer],OpenMode.ForWrite);item.IsLocked=locked;tr.Commit(); }
    }
    static int Blocks(Database db)
    { using(var tr=db.TransactionManager.StartTransaction()) return ((BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead)).Cast<ObjectId>().Count(); }
    static void TestDatabase(BeamSideDirection direction,bool full,bool top,bool bottom,double scale,bool annotationRight,double width=300)
    {
        Database previous=HostApplicationServices.WorkingDatabase;
        using(var db=new Database(true,true))
        try
        {
            HostApplicationServices.WorkingDatabase=db;db.Insunits=scale==1?UnitsValue.Millimeters:UnitsValue.Meters;
            var p=new BeamSideRebarParameters(400,100,topUp:top,bottomUp:bottom,annotationRight:annotationRight);
            var request=new BeamSideDrawRequest(direction,full,p,scale,(int)db.Insunits,false,Layers);
            var g=Geometry(direction,full,width);var layout=BeamSideRebarGeometry.Calculate(g,p);var unit=new BeamSideUnitTransform(scale);
            if(width==100)Check(layout.DotSpans[0].MinimumAdditionalCount==0&&layout.DotSpans[2].MinimumAdditionalCount==0,"narrow CAD block has no horizontal straight dots");
            ObjectId source;
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForWrite);
                foreach(string key in OtherKeys)
                {
                    var record=new Xrecord();nod.SetAt(key,record);tr.AddNewlyCreatedDBObject(record,true);
                    using(var buffer=new ResultBuffer(new TypedValue((int)DxfCode.Real,99.0))) record.Data=buffer;
                }
                var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForWrite);
                var keepLayer=new LayerTableRecord { Name=Layers[0],Color=Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci,3) };
                layers.Add(keepLayer);tr.AddNewlyCreatedDBObject(keepLayer,true);
                var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);var poly=new Polyline { Closed=true,Elevation=unit.LengthToDrawing(g.Elevation) };
                var points=new[]{new ContourPoint(g.Block.Left,g.Block.Bottom),new ContourPoint(g.Block.Right,g.Block.Bottom),new ContourPoint(g.Block.Right,g.Block.Top),new ContourPoint(g.Block.Left,g.Block.Top)};
                for(int i=0;i<4;i++) { var point=unit.ToDrawing(points[i]);poly.AddVertexAt(i,new Point2d(point.X,point.Y),0,0,0); }
                source=space.AppendEntity(poly);tr.AddNewlyCreatedDBObject(poly,true);tr.Commit();
            }
            double z;var segments=Commands.ReadBeamSideSegments(db,new[]{source},unit,out z);Near(z,g.Elevation);
            Near(BeamSideContourRecognizer.Recognize(segments,z,direction,false).Candidates.Single().Block.Width,width);
            var failures=new List<string>();ObjectId reference=Commands.CreateBeamSideBlock(db,request,layout,failures);
            Check(failures.Count==0,"complete annotations: "+String.Join(";",failures));
            var saved=Commands.ReadBeamSideLast(db,Layers);Check(saved!=null&&saved.Direction==direction&&saved.FullSection==full,"LAST roundtrip");
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForRead);
                foreach(string key in OtherKeys)
                { var old=(Xrecord)tr.GetObject(nod.GetAt(key),OpenMode.ForRead);using(var buffer=old.Data) Near(Convert.ToDouble(buffer.AsArray()[0].Value),99); }
                var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
                Check(((LayerTableRecord)tr.GetObject(layers[Layers[0]],OpenMode.ForRead)).Color.ColorIndex==3,"existing layer color untouched");
                var original=(Polyline)tr.GetObject(source,OpenMode.ForRead);Check(original.NumberOfVertices==4&&original.Closed,"source untouched");
                var blockRef=(BlockReference)tr.GetObject(reference,OpenMode.ForRead);var block=(BlockTableRecord)tr.GetObject(blockRef.BlockTableRecord,OpenMode.ForRead);
                Check(block.Name.StartsWith("WDT-BEAM-SIDE-"),"independent ordinary block");
                var entities=block.Cast<ObjectId>().Select(id=>(Entity)tr.GetObject(id,OpenMode.ForRead)).ToList();
                var line=entities.OfType<Polyline>().Single(e=>e.Layer==Layers[0]);
                for(int i=0;i<line.NumberOfVertices;i++)
                {
                    var point=line.GetPoint3dAt(i).TransformBy(blockRef.BlockTransform);
                    Near(point.X,layout.Line[i].X*scale);Near(point.Y,layout.Line[i].Y*scale);Near(point.Z,layout.Elevation*scale);
                    Near(line.GetStartWidthAt(i),layout.LineWidth*scale);Near(line.GetEndWidthAt(i),layout.LineWidth*scale);Near(line.GetBulgeAt(i),layout.Line[i].Bulge);
                }
                Check(entities.OfType<Polyline>().Count(e=>e.Layer==Layers[1]&&e.Closed)==layout.Dots.Count,"one entity per dot");
                Check(entities.OfType<DBText>().Count()==2&&entities.OfType<RotatedDimension>().Count()==2,"two TEXT and two native dimensions");
                foreach(var text in entities.OfType<DBText>()) { Near(text.Rotation,0);Check(!text.IsMirroredInX&&!text.IsMirroredInY,"upright TEXT"); }
                Check(entities.OfType<DBText>().Any(t=>t.TextString==layout.DotSpecification),"always actual diameter @ input spacing");
                var leaders=entities.OfType<Line>().ToArray();Check(leaders.Length==2,"two single straight leaders, no diagonal connector");
                foreach(var leader in leaders)
                {
                    Near(leader.StartPoint.Y,leader.EndPoint.Y);
                    var start=leader.StartPoint.TransformBy(blockRef.BlockTransform);
                    bool dot=Math.Abs(start.Y-layout.DotLabelAnchor.Y*scale)<1e-7;
                    Near(start.X,(dot?layout.DotLabelAnchor.X:layout.LineLabelAnchor.X)*scale);
                    Near(start.Y,(dot?layout.DotLabelAnchor.Y:layout.LineLabelAnchor.Y)*scale);
                    var text=entities.OfType<DBText>().Single(t=>t.TextString==(dot?layout.DotSpecification:layout.LineSpecification));
                    var bounds=text.GeometricExtents;
                    Near(leader.EndPoint.X,annotationRight?bounds.MaxPoint.X:bounds.MinPoint.X);
                    Check(bounds.MinPoint.Y>leader.StartPoint.Y,"TEXT above actual horizontal line");
                    foreach(var other in leaders.Where(l=>l!=leader))
                        Check(other.StartPoint.Y<bounds.MinPoint.Y||other.StartPoint.Y>bounds.MaxPoint.Y,"other leader misses TEXT");
                }
                var heightDim=entities.OfType<RotatedDimension>().Single(d=>Math.Abs(d.Rotation-Math.PI/2)<1e-7);
                var heightPoint=heightDim.DimLinePoint.TransformBy(blockRef.BlockTransform);
                Check(direction==BeamSideDirection.Left?heightPoint.X>g.Block.Right*scale:heightPoint.X<g.Block.Left*scale,"dimension side independent from labels");
                Near(heightDim.XLine1Point.TransformBy(blockRef.BlockTransform).X,g.ConnectionX*scale);
                foreach(var dimension in entities.OfType<RotatedDimension>()) { Near(dimension.Dimlfac,1/scale);Near(dimension.Dimscale,25*scale); }
                var dictionary=(DBDictionary)tr.GetObject(blockRef.ExtensionDictionary,OpenMode.ForRead);
                var record=(Xrecord)tr.GetObject(dictionary.GetAt(BeamSideDrawRecord.BlockKey),OpenMode.ForRead);
                using(var data=record.Data)
                {
                    var values=data.AsArray();Check((string)values[0].Value==BeamSideRebarLayout.NodeType,"snapshot type");
                    BeamSideDrawRequest restored;var geometry=BeamSideDrawRecord.DecodeSnapshot(values.Skip(1).Take(31).Select(v=>Convert.ToDouble(v.Value)).ToArray(),values.Skip(32).Select(v=>(string)v.Value).ToArray(),out restored);
                    Near(geometry.BasePoint.X,g.BasePoint.X);Near(geometry.BasePoint.Y,g.BasePoint.Y);Near(geometry.Elevation,g.Elevation);
                }
            }
            var before=BeamSideDrawRecord.Encode(Commands.ReadBeamSideLast(db,Layers));int count=Blocks(db);
            var changed=new BeamSideDrawRequest(direction,full,new BeamSideRebarParameters(450,100,topUp:top,bottomUp:bottom),scale,(int)db.Insunits,false,Layers);
            var changedLayout=BeamSideRebarGeometry.Calculate(g,changed.Parameters);
            Lock(db,Layers[0],true);bool rejected=false;
            try { Commands.CreateBeamSideBlock(db,changed,changedLayout,new List<string>()); } catch(InvalidOperationException) { rejected=true; }
            Check(rejected&&Blocks(db)==count,"critical failure leaves no definition");Check(before.SequenceEqual(BeamSideDrawRecord.Encode(Commands.ReadBeamSideLast(db,Layers))),"failure does not save LAST");Lock(db,Layers[0],false);
            Lock(db,Layers[2],true);failures.Clear();Commands.CreateBeamSideBlock(db,changed,changedLayout,failures);
            Check(failures.Count==2,"locked TEXT annotations fail independently while steel persists");Lock(db,Layers[2],false);
            Near(Commands.ReadBeamSideLast(db,Layers).Parameters.Length,450);
            string output=Path.Combine(Path.GetDirectoryName(typeof(BeamSideCadTests).Assembly.Location),"Output");Directory.CreateDirectory(output);
            string file=Path.Combine(output,Guid.NewGuid().ToString("N")+".dwg");db.SaveAs(file,DwgVersion.Current);
            using(var reopened=new Database(false,true))
            { reopened.ReadDwgFile(file,FileOpenMode.OpenForReadAndAllShare,false,"");reopened.CloseInput(true);Near(Commands.ReadBeamSideLast(reopened,Layers).Parameters.Length,450); }
        }
        finally { HostApplicationServices.WorkingDatabase=previous; }
    }
}
