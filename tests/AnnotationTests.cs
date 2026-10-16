using System;
using System.Collections;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(AnnotationTests))]
public class AnnotationTests
{
    private static readonly Assembly Plugin = typeof(WallDetailTool.Commands).Assembly;
    private static readonly Type Data = Plugin.GetType("WallDetailTool.DrawingData", true);
    private static readonly Type Service = Plugin.GetType("WallDetailTool.AnnotationService", true);
    private static object Call(Type type, string name, params object[] args)
    {
        try { return type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    private static object Field(object value, string name)
    {
        return value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value);
    }
    private static object First(IList items)
    {
        if (items.Count != 1) throw new InvalidOperationException("Expected one item, got " + items.Count);
        return items[0];
    }
    private static object Index(Database db, Transaction tr) { return Call(Data, "Read", db, tr); }
    private static object Rebar(Database db, Transaction tr) { return First((IList)Field(Index(db, tr), "Rebars")); }
    private static object Annotation(Database db, Transaction tr) { return First((IList)Field(Index(db, tr), "Annotations")); }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Report(string marker, Action action)
    {
        try { action(); Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n" + marker + "_PASS\n"); }
        catch (System.Exception ex) { Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n" + marker + "_FAIL: " + ex + "\n"); }
    }

    [CommandMethod("WDTLABELCREATECHECK")]
    public void Create()
    {
        Report("WDT_LABEL_CREATE", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object rebar = Rebar(db, tr);
                TextStyleTable styles = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                if (!styles.Has("TSSD"))
                {
                    styles.UpgradeOpen();
                    var style = new TextStyleTableRecord { Name = "TSSD", FileName = "txt.shx" };
                    styles.Add(style);
                    tr.AddNewlyCreatedDBObject(style, true);
                }
                using (var label = new DBText { Position = new Point3d(400, 130, 0),
                    Height = 62.5, TextString = "%%c12" })
                using (var leader = new Line(new Point3d(400, 31, 0), new Point3d(400, 130, 0)))
                    Call(Service, "Create", db, tr, Field(rebar, "Id"), db.CurrentSpaceId, label, leader, "Φ12");
                tr.Commit();
            }
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object annotation = Annotation(db, tr);
                object rebar = Rebar(db, tr);
                DBText text = tr.GetObject((ObjectId)Field(annotation, "TextId"), OpenMode.ForRead) as DBText;
                Line leader = tr.GetObject((ObjectId)Field(annotation, "LeaderId"), OpenMode.ForRead) as Line;
                Polyline steel = tr.GetObject((ObjectId)Field(rebar, "EntityId"), OpenMode.ForRead) as Polyline;
                Require(text != null && leader != null && text.TextString == "%%c12" &&
                    text.Layer == "S-TEXT" && leader.Layer == "s-floor_de_dim" &&
                    Math.Abs(text.Height - 62.5) < 1e-9, "Annotation style, layer or height wrong");
                Require(steel != null && Math.Abs(steel.ConstantWidth - 12) < 1e-9 &&
                    (double)Field(rebar, "Diameter") == 12, "Steel changed by annotation");
            }
            using (var other = new Database(true, true))
            using (Transaction tr = other.TransactionManager.StartTransaction())
            {
                object index = Index(other, tr);
                Require(((IList)Field(index, "Annotations")).Count == 0,
                    "Annotation leaked into another drawing");
            }
        });
    }

    [CommandMethod("WDTLABELEDITCHECK")]
    public void Edit()
    {
        Report("WDT_LABEL_EDIT", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object annotation = Annotation(db, tr);
                DBText text = (DBText)tr.GetObject((ObjectId)Field(annotation, "TextId"), OpenMode.ForRead);
                Call(Service, "Edit", db, tr, annotation, text.TextString, text.Position, "Φ16");
                tr.Commit();
            }
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object annotation = Annotation(db, tr);
                object rebar = Rebar(db, tr);
                DBText text = (DBText)tr.GetObject((ObjectId)Field(annotation, "TextId"), OpenMode.ForRead);
                Polyline steel = (Polyline)tr.GetObject((ObjectId)Field(rebar, "EntityId"), OpenMode.ForRead);
                Require(text.TextString == "%%c16" && (string)Field(annotation, "Label") == "Φ16", "Text not updated");
                Require((double)Field(rebar, "Diameter") == 12 && Math.Abs(steel.ConstantWidth - 12) < 1e-9 &&
                    Math.Abs(Math.Abs(steel.StartPoint.Y) - 31) < 1e-6, "Steel changed by text edit");
            }
        });
    }

    [CommandMethod("WDTLABELUNDOCHECK")]
    public void Undo()
    {
        Report("WDT_LABEL_UNDO", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object annotation = Annotation(db, tr);
                DBText text = (DBText)tr.GetObject((ObjectId)Field(annotation, "TextId"), OpenMode.ForRead);
                Require(text.TextString == "%%c12" && (string)Field(annotation, "Label") == "Φ12", "Undo mismatch");
            }
        });
    }

    [CommandMethod("WDTLABELREDOCHECK")]
    public void Redo()
    {
        Report("WDT_LABEL_REDO", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object annotation = Annotation(db, tr);
                DBText text = (DBText)tr.GetObject((ObjectId)Field(annotation, "TextId"), OpenMode.ForRead);
                Require(text.TextString == "%%c16" && (string)Field(annotation, "Label") == "Φ16", "Redo mismatch");
            }
        });
    }

    [CommandMethod("WDTLABELVERIFY")]
    public void Verify()
    {
        Report("WDT_LABEL_VERIFY", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object annotation = Annotation(db, tr);
                DBText text = (DBText)tr.GetObject((ObjectId)Field(annotation, "TextId"), OpenMode.ForRead);
                Require(text.TextString == "%%c16" && text.Layer == "S-TEXT" &&
                    (string)Field(annotation, "Label") == "Φ16", "Saved annotation missing");
            }
        });
    }

    [CommandMethod("WDTLABELDELETECHECK")]
    public void Delete()
    {
        Report("WDT_LABEL_DELETE", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object rebar = Rebar(db, tr);
                Entity steel = (Entity)tr.GetObject((ObjectId)Field(rebar, "EntityId"), OpenMode.ForWrite);
                steel.Erase();
                tr.Commit();
            }
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object rebar = Rebar(db, tr);
                object annotation = Annotation(db, tr);
                Require(((ObjectId)Field(rebar, "EntityId")).IsErased, "Steel not erased");
                Require(!((ObjectId)Field(annotation, "TextId")).IsErased &&
                    !((ObjectId)Field(annotation, "LeaderId")).IsErased,
                    "Annotation deleted without confirmation");
            }
        });
    }
}
