using System;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(GeometryTests))]
public class GeometryTests
{
    private static object Call(string name, params object[] args)
    {
        return typeof(WallDetailTool.Commands).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
    }
    private static Polyline Make(params double[] xy)
    {
        var p = new Polyline();
        for (int i = 0; i < xy.Length; i += 2) p.AddVertexAt(i / 2, new Point2d(xy[i], xy[i + 1]), 0, 0, 0);
        return p;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Invalid(Polyline p)
    {
        using (p)
        {
            try { Call("ValidatePolyline", p); }
            catch (TargetInvocationException ex) { if (ex.InnerException is InvalidOperationException) return; throw; }
            throw new InvalidOperationException("Invalid geometry was accepted");
        }
    }
    [CommandMethod("WDTGEOMETRYTEST")]
    public void Run()
    {
        var ed = Application.DocumentManager.MdiActiveDocument.Editor;
        try
        {
            using (var p = Make(0, 0, 1000, 0, 1000, 1000))
            {
                Call("ValidatePolyline", p);
                foreach (double y in new[] { 50.0, -50.0 })
                {
                    object[] args = { p, new Point3d(500, y, 0), 31.0, 0.0 };
                    Require((bool)Call("TryDirection", args), "Side selection failed");
                    using (var offset = (Polyline)Call("OffsetOne", p, args[3]))
                    {
                        Call("ValidatePolyline", offset);
                        Call("ValidateClearance", p, offset, 31.0);
                        Require(Math.Abs(offset.StartPoint.Y - Math.Sign(y) * 31) < 1e-6, "Wrong side/distance");
                    }
                }
                object[] onLine = { p, new Point3d(500, 0, 0), 31.0, 0.0 };
                Require(!(bool)Call("TryDirection", onLine), "Point on contour accepted");
                object[] corner = { p, new Point3d(950, 50, 0), 31.0, 0.0 };
                Require(!(bool)Call("TryDirection", corner), "Ambiguous corner accepted");
                Require(p.StartPoint.DistanceTo(Point3d.Origin) < 1e-6 && p.NumberOfVertices == 3, "Source changed");
                using (var wide = (Polyline)Call("CreateRebar", p, 31.0, 31.0, 12.0,
                    Application.DocumentManager.MdiActiveDocument.Database))
                {
                    Require(Math.Abs(wide.ConstantWidth - 12.0) < 1e-9, "Constant width wrong");
                    for (int i = 0; i < wide.NumberOfVertices - 1; i++)
                        Require(Math.Abs(wide.GetStartWidthAt(i) - 12.0) < 1e-9 &&
                                Math.Abs(wide.GetEndWidthAt(i) - 12.0) < 1e-9, "Segment width wrong");
                    Require(Math.Abs(Math.Abs(wide.StartPoint.Y) - 31.0) < 1e-6, "Width changed centerline offset");
                    Require(p.ConstantWidth == 0.0 && p.GetStartWidthAt(0) == 0.0, "Source width changed");
                }
                Database database = Application.DocumentManager.MdiActiveDocument.Database;
                UnitsValue originalUnits = database.Insunits;
                try
                {
                    database.Insunits = UnitsValue.Meters;
                    double scale = (double)Call("MillimetresToDrawingUnits", database);
                    Require(Math.Abs(scale - 0.001) < 1e-12, "Metre conversion wrong");
                    using (var metres = (Polyline)Call("CreateRebar", p, 31 * scale, 31 * scale,
                        12 * scale, database))
                    {
                        Require(Math.Abs(metres.ConstantWidth - 0.012) < 1e-9, "Metre width wrong");
                        Require(Math.Abs(Math.Abs(metres.StartPoint.Y) - 0.031) < 1e-9, "Metre offset wrong");
                    }
                }
                finally { database.Insunits = originalUnits; }
                using (var old = p.ConvertTo(false))
                using (var copy = (Polyline)Call("CopyContour", old))
                {
                    Call("ValidatePolyline", copy);
                    Require(copy.NumberOfVertices == 3, "Legacy conversion failed");
                }
            }
            using (var reversed = Make(1000, 0, 0, 0))
            {
                object[] args = { reversed, new Point3d(500, 50, 0), 31.0, 0.0 };
                Require((bool)Call("TryDirection", args), "Reversed direction failed");
                using (var offset = (Polyline)Call("OffsetOne", reversed, args[3]))
                    Require(Math.Abs(offset.StartPoint.Y - 31) < 1e-6, "Reversed side wrong");
            }
            Invalid(Make(0, 0, 0, 0, 100, 0));
            Invalid(Make(0, 0, 100, 100, 0, 100, 100, 0));
            Invalid(Make(0, 0, 100, 0, 50, 0));
            Invalid(Make(0, 0, 100, 0, 100, 100, 50, 0));
            var closed = Make(0, 0, 100, 0, 100, 100); closed.Closed = true; Invalid(closed);
            var arc = Make(0, 0, 100, 0); arc.SetBulgeAt(0, 0.5); Invalid(arc);
            using (var a = Make(0, 0, 100, 0))
            using (var b = Make(0, 10, 100, 10))
            {
                bool rejected = false;
                try { Call("ValidateClearance", a, b, 31.0); }
                catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidOperationException; }
                Require(rejected, "Insufficient cover accepted");
            }
            ed.WriteMessage("\nWDT_GEOMETRY_TESTS_PASS\n");
        }
        catch (System.Exception ex) { ed.WriteMessage("\nWDT_GEOMETRY_TESTS_FAIL: " + ex + "\n"); }
    }

    [CommandMethod("WDTVERIFYDRAWN")]
    public void VerifyDrawn()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        bool pass = false;
        using (var transaction = doc.Database.TransactionManager.StartTransaction())
        {
            var space = (BlockTableRecord)transaction.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForRead);
            Polyline last = null;
            foreach (ObjectId id in space) last = transaction.GetObject(id, OpenMode.ForRead) as Polyline;
            if (last != null && last.NumberOfVertices == 2 && Math.Abs(last.StartPoint.Y - 31) < 1e-6 &&
                Math.Abs(last.ConstantWidth - 12) < 1e-6 &&
                Math.Abs(last.GetStartWidthAt(0) - 12) < 1e-6 && Math.Abs(last.GetEndWidthAt(0) - 12) < 1e-6)
                pass = true;
        }
        doc.Editor.WriteMessage(pass ? "\nWDT_DRAWN_WIDTH_PASS\n" : "\nWDT_DRAWN_WIDTH_FAIL\n");
    }
}
