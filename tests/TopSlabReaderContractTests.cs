// Runs the verbatim production ReadBeamSideSegments method against small API doubles.
// This verifies segment iteration/read-only contracts, NOT AutoCAD database behaviour.
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using WallDetailTool;

namespace Autodesk.AutoCAD.Geometry
{
    internal struct Point3d { internal double X,Y,Z;internal Point3d(double x,double y,double z) { X=x;Y=y;Z=z; } }
}
namespace Autodesk.AutoCAD.DatabaseServices
{
    internal enum OpenMode { ForRead,ForWrite }
    internal sealed class ObjectId { internal Entity Value;internal ObjectId(Entity value) { Value=value; } }
    internal class Entity { }
    internal sealed class Line:Entity { internal Point3d StartPoint,EndPoint; }
    internal sealed class Polyline:Entity
    {
        internal Point3d[] Points;
        internal double[] Bulges;
        internal Point3d Normal=new Point3d(0,0,1);
        internal bool Closed;
        internal int NumberOfVertices { get { return Points.Length; } }
        internal readonly List<int> Reads=new List<int>();
        internal double GetBulgeAt(int i) { return Bulges[i]; }
        internal Point3d GetPoint3dAt(int i) { Reads.Add(i);return Points[i]; }
    }
    internal sealed class Database { internal readonly TransactionManager TransactionManager=new TransactionManager(); }
    internal sealed class TransactionManager
    {
        internal int Reads,Disposals;
        internal Transaction StartTransaction() { return new Transaction(this); }
    }
    internal sealed class Transaction:IDisposable
    {
        private readonly TransactionManager owner;
        internal Transaction(TransactionManager owner) { this.owner=owner; }
        internal object GetObject(ObjectId id,OpenMode mode)
        { if(mode!=OpenMode.ForRead)throw new Exception("source opened for write");owner.Reads++;return id.Value; }
        public void Dispose() { owner.Disposals++; }
    }
}
internal static class TopSlabReaderContractTests
{
    private static int checks;
    private static void Check(bool ok,string name) { checks++;if(!ok)throw new Exception(name); }
    private static void Reject(Action action,string name)
    { try { action(); }catch(InvalidOperationException) { checks++;return; }throw new Exception(name); }
    private static Polyline Source(bool closed,int direction)
    {
        return new Polyline { Closed=closed,Bulges=new double[6],Points=new[]{new Point3d(300*direction,0,7),new Point3d(300*direction,600,7),
            new Point3d(1200*direction,600,7),new Point3d(1200*direction,800,7),new Point3d(0,800,7),new Point3d(0,0,7)} };
    }
    private static void Main()
    {
        foreach(int direction in new[]{1,-1})foreach(bool reverse in new[]{false,true})
        {
            var db=new Database();var poly=Source(false,direction);if(reverse)Array.Reverse(poly.Points);
            var points=poly.Points.ToArray();poly.Bulges[5]=.5; // unused final vertex bulge of an open polyline
            double z;var open=Commands.ReadBeamSideSegments(db,new[]{new ObjectId(poly)},new BeamSideUnitTransform(1),out z);
            Check(open.Count==5&&z==7,"exactly five real edges and elevation");
            Check(open[4].A.X==points[4].X&&open[4].A.Y==points[4].Y&&open[4].B.X==points[5].X&&open[4].B.Y==points[5].Y,"last real segment read");
            Check(poly.Reads.SequenceEqual(new[]{0,1,1,2,2,3,3,4,4,5}),"no invented last-to-first segment");
            Check(!poly.Closed&&poly.Points.SequenceEqual(points)&&poly.Bulges[5]==.5,"source properties unchanged");
            Check(db.TransactionManager.Reads==1&&db.TransactionManager.Disposals==1,"read-only transaction disposed");
            Check(TopSlabRecognizer.RecognizeInput(open,z).HasVirtualBottom,"real reader output recognized as open");
            var closed=Source(true,direction);if(reverse)Array.Reverse(closed.Points);
            var complete=Commands.ReadBeamSideSegments(db,new[]{new ObjectId(closed)},new BeamSideUnitTransform(1),out z);
            Check(complete.Count==6&&!TopSlabRecognizer.RecognizeInput(complete,z).HasVirtualBottom,"closed reader regression");
            closed.Bulges[5]=.5;Reject(()=>Commands.ReadBeamSideSegments(db,new[]{new ObjectId(closed)},new BeamSideUnitTransform(1),out z),"closed final arc rejected");
            poly.Bulges[4]=.5;Reject(()=>Commands.ReadBeamSideSegments(db,new[]{new ObjectId(poly)},new BeamSideUnitTransform(1),out z),"open last real arc rejected");
            poly.Bulges[4]=0;poly.Normal=new Point3d(0,1,0);Reject(()=>Commands.ReadBeamSideSegments(db,new[]{new ObjectId(poly)},new BeamSideUnitTransform(1),out z),"non-WCS normal rejected");
            poly.Normal=new Point3d(0,0,1);poly.Points[5]=new Point3d(points[5].X,points[5].Y,8);
            Reject(()=>Commands.ReadBeamSideSegments(db,new[]{new ObjectId(poly)},new BeamSideUnitTransform(1),out z),"different WCS elevation rejected");
            var lines=points.Take(5).Select((p,i)=>new ObjectId(new Line { StartPoint=points[i+1],EndPoint=p })).Reverse().ToArray();
            Check(TopSlabRecognizer.RecognizeInput(Commands.ReadBeamSideSegments(db,lines,new BeamSideUnitTransform(1),out z),z).HasVirtualBottom,"LINE shuffle/reversed endpoints read");
            Reject(()=>Commands.ReadBeamSideSegments(db,new[]{new ObjectId(new Entity())},new BeamSideUnitTransform(1),out z),"unsupported entity rejected");
        }
        Console.WriteLine("TOP_SLAB_READER_CONTRACT_PASS checks="+checks+" (API doubles, no CAD)");
    }
}
