using System;
using System.Collections;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(PersistenceTests))]
public class PersistenceTests
{
    private static readonly Type Data = typeof(WallDetailTool.Commands).Assembly.GetType("WallDetailTool.DrawingData", true);
    private static object Call(string method, params object[] args)
    {
        try { return Data.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    private static object Field(object value, string name)
    {
        return value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value);
    }
    private static IList List(object value, string name) { return (IList)Field(value, name); }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Report(string name, Action test)
    {
        try { test(); Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n" + name + "_PASS\n"); }
        catch (System.Exception ex) { Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n" + name + "_FAIL: " + ex + "\n"); }
    }
    private static object Read(Database db, Transaction tr) { return Call("Read", db, tr); }

    [CommandMethod("WDTSTORAGECREATE")]
    public void Create()
    {
        Report("WDT_STORAGE_CREATE", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object empty = Read(db, tr);
                Check(List(empty, "Nodes").Count == 0, "New drawing not empty");
                Guid node = (Guid)Call("AddNode", db, tr, "测试节点", 25.0, 12.0);
                Guid group = (Guid)Call("AddGroup", db, tr, node, "测试钢筋组", 25.0, 12.0);
                Call("UpdateDefaults", db, tr, group, "已编辑的钢筋组", 30.0, 16.0);
                var rebar = new Polyline();
                rebar.AddVertexAt(0, new Point2d(0, 31), 0, 12, 12);
                rebar.AddVertexAt(1, new Point2d(1000, 31), 0, 12, 12);
                BlockTableRecord space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                space.AppendEntity(rebar);
                tr.AddNewlyCreatedDBObject(rebar, true);
                Call("AddRebar", db, tr, group, rebar.ObjectId, 25.0, 12.0);
                object index = Read(db, tr);
                Check(List(index, "Nodes").Count == 1 && List(index, "Groups").Count == 1 &&
                    List(index, "Rebars").Count == 1, "Hierarchy count wrong");
                Check((string)Field(List(index, "Groups")[0], "Name") == "已编辑的钢筋组" &&
                    Math.Abs((double)Field(List(index, "Groups")[0], "Diameter") - 16) < 1e-9,
                    "Group edit not saved");
                tr.Commit();
            }
            using (var other = new Database(true, true))
            using (Transaction tr = other.TransactionManager.StartTransaction())
                Check(List(Read(other, tr), "Nodes").Count == 0, "Data leaked to another drawing");
        });
    }

    [CommandMethod("WDTSTORAGEGUARD")]
    public void Guard()
    {
        Report("WDT_STORAGE_GUARD", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var named = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
                var root = (DBDictionary)tr.GetObject(named.GetAt("WDT_DATA"), OpenMode.ForRead);
                var schema = (Xrecord)tr.GetObject(root.GetAt("SCHEMA"), OpenMode.ForWrite);
                schema.Data = new ResultBuffer(new TypedValue((int)DxfCode.Int32, 99));
                bool rejected = false;
                try { Read(db, tr); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Unknown schema accepted");
                // Intentionally roll back the artificial corruption.
            }
            using (Transaction tr = db.TransactionManager.StartTransaction())
                Check(List(Read(db, tr), "Nodes").Count == 1, "Guard changed saved data");
        });
    }

    [CommandMethod("WDTSTORAGEVERIFY")]
    public void Verify()
    {
        Report("WDT_STORAGE_VERIFY", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object data = Read(db, tr);
                Check(List(data, "Nodes").Count == 1 && List(data, "Groups").Count == 1 &&
                    List(data, "Rebars").Count == 1, "Saved hierarchy missing");
                object rebar = List(data, "Rebars")[0];
                ObjectId id = (ObjectId)Field(rebar, "EntityId");
                Check(!id.IsNull && !id.IsErased, "Saved steel pointer missing");
                Check(Math.Abs((double)Field(rebar, "Diameter") - 12) < 1e-9, "Saved diameter wrong");
                Check(tr.GetObject(id, OpenMode.ForRead) is Polyline, "Linked object not a polyline");
            }
        });
    }

    [CommandMethod("WDTSTORAGEERASE")]
    public void Erase()
    {
        Report("WDT_STORAGE_ERASE", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object data = Read(db, tr);
                ObjectId id = (ObjectId)Field(List(data, "Rebars")[0], "EntityId");
                tr.GetObject(id, OpenMode.ForWrite).Erase();
                tr.Commit();
            }
        });
    }

    [CommandMethod("WDTSTORAGEMISSING")]
    public void Missing()
    {
        Report("WDT_STORAGE_MISSING", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object data = Read(db, tr);
                ObjectId id = (ObjectId)Field(List(data, "Rebars")[0], "EntityId");
                Check(id.IsErased, "Deleted steel not marked missing");
            }
        });
    }
}
