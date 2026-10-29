using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace WallDetailTool
{
    public partial class Commands
    {
        private static void AppendEndLengthNotes(Database db,ObjectId block,Matrix3d local,string layer,
            IEnumerable<EndLengthNote> notes,double elevation,List<string> failures)
        {
            foreach(var note in notes)
                TryAppendAnnotation(db,block,local,layer,71,note.Name+"长度文字",failures,
                    tr=>BuildEndLengthText(db,tr,block,local,note,elevation,layer));
        }
        private static List<Entity> BuildEndLengthText(Database db,Transaction tr,ObjectId block,Matrix3d local,EndLengthNote note,double z,string layer)
        {
            CheckBeamSideAnnotationLayer(db,tr,layer);
            var text=new DBText();
            try
            {
                text.SetDatabaseDefaults(db);text.Layer="0";text.ColorIndex=256;
                text.TextStyleId=EnsureUpstandTextStyle(db,tr);text.Height=note.Height;text.TextString=note.Text;
                text.Rotation=note.Rotation;text.IsMirroredInX=false;text.IsMirroredInY=false;
                text.Position=new Point3d((note.A.X+note.B.X)/2,(note.A.Y+note.B.Y)/2,z);text.AdjustAlignment(db);
                var ext=text.GeometricExtents;
                var box=EndLengthAnnotationGeometry.Place(note,ext.MaxPoint.X-ext.MinPoint.X,ext.MaxPoint.Y-ext.MinPoint.Y,
                    candidate=>EndTextBlocked(tr,block,local,candidate,note.Gap/2,z));
                text.TransformBy(Matrix3d.Displacement(new Vector3d(box.CenterX-(ext.MinPoint.X+ext.MaxPoint.X)/2,
                    box.CenterY-(ext.MinPoint.Y+ext.MaxPoint.Y)/2,0)));
                return new List<Entity>{text};
            }
            catch { text.Dispose();throw; }
        }
        private static bool EndTextBlocked(Transaction tr,ObjectId block,Matrix3d local,EndNoteBox world,double gap,double z)
        {
            var origin=new Point3d(world.Left,world.Bottom,z).TransformBy(local);
            var corner=new Point3d(world.Right,world.Top,z).TransformBy(local);
            var box=new EndNoteBox(origin.X,origin.Y,corner.X,corner.Y).Expand(gap);
            var definition=(BlockTableRecord)tr.GetObject(block,OpenMode.ForRead);
            foreach(ObjectId id in definition)
            {
                var entity=tr.GetObject(id,OpenMode.ForRead) as Entity;if(entity==null)continue;
                var curve=entity as Curve;
                if(curve!=null)
                {
                    double width=0;var poly=curve as Polyline;
                    if(poly!=null)for(int i=0;i<poly.NumberOfVertices;i++) width=Math.Max(width,Math.Max(poly.GetStartWidthAt(i),poly.GetEndWidthAt(i)));
                    var expanded=box.Expand(width/2);
                    var centre=new Point3d(expanded.CenterX,expanded.CenterY,origin.Z);
                    var closest=curve.GetClosestPointTo(centre,false);
                    if(expanded.Contains(closest.X,closest.Y))return true;
                    using(var border=new Polyline(4) { Closed=true,Elevation=origin.Z })
                    {
                        border.AddVertexAt(0,new Point2d(expanded.Left,expanded.Bottom),0,0,0);
                        border.AddVertexAt(1,new Point2d(expanded.Right,expanded.Bottom),0,0,0);
                        border.AddVertexAt(2,new Point2d(expanded.Right,expanded.Top),0,0,0);
                        border.AddVertexAt(3,new Point2d(expanded.Left,expanded.Top),0,0,0);
                        var hits=new Point3dCollection();curve.IntersectWith(border,Intersect.OnBothOperands,hits,IntPtr.Zero,IntPtr.Zero);
                        if(hits.Count>0)return true;
                    }
                }
                else
                {
                    // Specifications, prior length TEXT and native dimensions use
                    // actual resident extents, including their generated glyphs.
                    var e=EndObstacleExtents(entity);
                    if(box.Overlaps(new EndNoteBox(e.MinPoint.X,e.MinPoint.Y,e.MaxPoint.X,e.MaxPoint.Y)))return true;
                }
            }
            return false;
        }
        private static Extents3d EndObstacleExtents(Entity entity)
        {
            try { return entity.GeometricExtents; }
            catch(Autodesk.AutoCAD.Runtime.Exception)
            {
                // Only the current new block is scanned. A just-created native
                // dimension may need its graphics evaluated before bounds exist.
                var dimension=entity as Dimension;if(dimension==null)throw;
                if(!dimension.IsWriteEnabled)dimension.UpgradeOpen();
                dimension.RecomputeDimensionBlock(true);
                return dimension.GeometricExtents;
            }
        }
    }
}
