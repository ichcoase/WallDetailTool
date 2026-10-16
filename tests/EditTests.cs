using System;
using System.Collections;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(EditTests))]
public class EditTests
{
    private static readonly Assembly Plugin = typeof(WallDetailTool.Commands).Assembly;
    private static readonly Type Data = Plugin.GetType("WallDetailTool.DrawingData", true);
    private static readonly Type Layers = Plugin.GetType("WallDetailTool.LayerTools", true);
    private static readonly Type Edit = Plugin.GetType("WallDetailTool.RebarEditService", true);
    private static object Call(Type type, string name, params object[] args)
    {
        try { return type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    private static object Field(object value, string name)
    {
        return value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value);
    }
    private static void Set(object value, string name, object item)
    {
        value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(value, item);
    }
    private static object Rebar(Database db, Transaction tr)
    {
        object index = Call(Data, "Read", db, tr);
        var rebars = (IList)Field(index, "Rebars");
        Require(rebars.Count == 1, "Expected one linked rebar");
        return rebars[0];
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Report(string name, Action run)
    {
        try { run(); Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n" + name + "_PASS\n"); }
        catch (System.Exception ex) { Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n" + name + "_FAIL: " + ex + "\n"); }
    }
    private static object Command(string name, params object[] args) { return Call(typeof(WallDetailTool.Commands), name, args); }
    private static double DisplayWidth()
    {
        Type settings = Plugin.GetType("WallDetailTool.PluginSettings", true);
        return (double)settings.GetMethod("ReadLineWidthMillimetres", BindingFlags.NonPublic | BindingFlags.Static,
            null, Type.EmptyTypes, null).Invoke(null, null);
    }

    [CommandMethod("WDTEDITCREATE")]
    public void Create()
    {
        Report("WDT_EDIT_CREATE", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var source = new Polyline();
                source.AddVertexAt(0, new Point2d(0, 0), 0, 0, 0);
                source.AddVertexAt(1, new Point2d(1000, 0), 0, 0, 0);
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                space.AppendEntity(source);
                tr.AddNewlyCreatedDBObject(source, true);
                object[] args = { source, new Point3d(500, 50, 0), 31.0, 0.0 };
                Require((bool)Command("TryDirection", args), "Direction failed");
                double side = Math.Sign((double)args[3]);
                using (var steel = (Polyline)Command("CreateRebar", source, side * 31, 31.0,
                    DisplayWidth() * (double)Command("MillimetresToDrawingUnits", db), db))
                {
                    Call(Layers, "Ensure", db, tr, "WDT_TEST_STEEL");
                    steel.Layer = "WDT_TEST_STEEL";
                    space.AppendEntity(steel);
                    tr.AddNewlyCreatedDBObject(steel, true);
                    Guid node = (Guid)Call(Data, "AddNode", db, tr, "Edit test", 25.0, 12.0);
                    Guid group = (Guid)Call(Data, "AddGroup", db, tr, node, "Edit group", 25.0, 12.0);
                    string hash = (string)Call(Data, "GeometryHash", source);
                    Call(Data, "AddLinkedRebar", db, tr, group, steel, source.ObjectId, side, hash, 25.0, 12.0);
                }
                tr.Commit();
            }
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object rebar = Rebar(db, tr);
                Require((string)Field(rebar, "Mode") == "Contour", "Mode not saved");
                Require(!(bool)Field(rebar, "SourceId").Equals(ObjectId.Null), "Source missing");
            }
        });
    }

    [CommandMethod("WDTEDITAPPLY")]
    public void Apply()
    {
        Report("WDT_EDIT_APPLY", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object data = Rebar(db, tr);
                var sourceId = (ObjectId)Field(data, "SourceId");
                var oldId = (ObjectId)Field(data, "EntityId");
                var source = (Polyline)tr.GetObject(sourceId, OpenMode.ForRead);
                var old = (Polyline)tr.GetObject(oldId, OpenMode.ForRead);
                double side = (double)Field(data, "Side");
                using (var proposed = (Polyline)Command("CreateRebar", source, side * 38, 38.0, 16.0, db))
                {
                    Call(Edit, "Apply", db, tr, data, old, proposed, sourceId, side,
                        Field(data, "SourceHash"), 30.0, 16.0, "WDT_TEST_EDITED", true, false);
                }
                Require(oldId.IsErased, "Old steel not erased");
                object updated = Rebar(db, tr);
                var newId = (ObjectId)Field(updated, "EntityId");
                var steel = (Polyline)tr.GetObject(newId, OpenMode.ForRead);
                Require(newId != oldId && Math.Abs(Math.Abs(steel.StartPoint.Y) - 38) < 1e-6, "Offset not updated");
                Require(Math.Abs(steel.ConstantWidth - DisplayWidth()) < 1e-9 && steel.Layer == "WDT_TEST_EDITED", "Width/layer not updated");
                Require(source.StartPoint.Y == 0, "Source changed");
                tr.Commit();
            }
        });
    }

    [CommandMethod("WDTEDITDIRECT")]
    public void Direct()
    {
        Report("WDT_EDIT_DIRECT", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object data = Rebar(db, tr);
                var steel = (Polyline)tr.GetObject((ObjectId)Field(data, "EntityId"), OpenMode.ForWrite);
                steel.SetPointAt(1, new Point2d(900, steel.GetPoint2dAt(1).Y));
                tr.Commit();
            }
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object data = Rebar(db, tr);
                var steel = (Polyline)tr.GetObject((ObjectId)Field(data, "EntityId"), OpenMode.ForRead);
                Require((string)Field(data, "GeometryHash") != (string)Call(Data, "GeometryHash", steel),
                    "Direct geometry edit was not detected");
            }
        });
    }

    [CommandMethod("WDTEDITLAYERONLY")]
    public void LayerOnly()
    {
        Report("WDT_EDIT_LAYER_ONLY", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object data = Rebar(db, tr);
                var steel = (Polyline)tr.GetObject((ObjectId)Field(data, "EntityId"), OpenMode.ForRead);
                using (var proposed = (Polyline)steel.Clone())
                    Call(Edit, "Apply", db, tr, data, steel, proposed, Field(data, "SourceId"),
                        Field(data, "Side"), Field(data, "SourceHash"), 30.0, 16.0,
                        "WDT_TEST_LAYER_ONLY", false, true);
                tr.Commit();
            }
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object data = Rebar(db, tr);
                var steel = (Polyline)tr.GetObject((ObjectId)Field(data, "EntityId"), OpenMode.ForRead);
                Require(steel.GetPoint2dAt(1).X == 900 && steel.Layer == "WDT_TEST_LAYER_ONLY", "Manual path overwritten");
                Require((string)Field(data, "Mode") == "Free", "Manual path mode not recorded");
            }
        });
    }

    [CommandMethod("WDTEDITVERIFY")]
    public void Verify()
    {
        Report("WDT_EDIT_VERIFY", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object data = Rebar(db, tr);
                var steel = (Polyline)tr.GetObject((ObjectId)Field(data, "EntityId"), OpenMode.ForRead);
                Require(steel.Layer == "WDT_TEST_LAYER_ONLY" && steel.GetPoint2dAt(1).X == 900, "Saved edit missing");
                Require((string)Field(data, "Mode") == "Free", "Saved mode missing");
                var source = (Polyline)tr.GetObject((ObjectId)Field(data, "SourceId"), OpenMode.ForRead);
                Require(source.GetPoint2dAt(1).X == 1000, "Saved source modified");
            }
        });
    }

    [CommandMethod("WDTEDITBEFORE")]
    public void Before()
    {
        Report("WDT_EDIT_UNDO", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object data = Rebar(db, tr);
                var steel = (Polyline)tr.GetObject((ObjectId)Field(data, "EntityId"), OpenMode.ForRead);
                Require((double)Field(data, "Cover") == 25 && (double)Field(data, "Diameter") == 12,
                    "Metadata not undone");
                Require(steel.Layer == "WDT_TEST_STEEL" && Math.Abs(steel.ConstantWidth - DisplayWidth()) < 1e-9 &&
                    Math.Abs(Math.Abs(steel.StartPoint.Y) - 31) < 1e-6, "Entity not undone");
            }
        });
    }

    [CommandMethod("WDTEDITREDO")]
    public void Redo()
    {
        Report("WDT_EDIT_REDO", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object data = Rebar(db, tr);
                var steel = (Polyline)tr.GetObject((ObjectId)Field(data, "EntityId"), OpenMode.ForRead);
                Require((double)Field(data, "Cover") == 30 && (double)Field(data, "Diameter") == 16,
                    "Metadata not redone");
                Require(steel.Layer == "WDT_TEST_EDITED" && Math.Abs(steel.ConstantWidth - DisplayWidth()) < 1e-9 &&
                    Math.Abs(Math.Abs(steel.StartPoint.Y) - 38) < 1e-6, "Entity not redone");
            }
        });
    }

    [CommandMethod("WDTMIGRATEOLD")]
    public void MigrateOld()
    {
        Report("WDT_OLD_MIGRATE", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object data = Rebar(db, tr);
                Require((string)Field(data, "Mode") == "Legacy", "Input is not old schema");
                var steel = (Polyline)tr.GetObject((ObjectId)Field(data, "EntityId"), OpenMode.ForRead);
                using (var proposed = (Polyline)steel.Clone())
                    Call(Edit, "Apply", db, tr, data, steel, proposed, ObjectId.Null, 0.0, "",
                        Field(data, "Cover"), Field(data, "Diameter"), "WDT_MIGRATED", false, false);
                tr.Commit();
            }
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object data = Rebar(db, tr);
                var steel = (Polyline)tr.GetObject((ObjectId)Field(data, "EntityId"), OpenMode.ForRead);
                Require((string)Field(data, "Mode") == "Free" && steel.Layer == "WDT_MIGRATED", "Migration lost entity");
                var named = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
                var root = (DBDictionary)tr.GetObject(named.GetAt("WDT_DATA"), OpenMode.ForRead);
                var schema = (Xrecord)tr.GetObject(root.GetAt("SCHEMA"), OpenMode.ForRead);
                Require(Convert.ToInt32(schema.Data.AsArray()[0].Value) == 4, "Schema not upgraded");
            }
        });
    }
}
