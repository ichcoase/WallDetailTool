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
    private static readonly Type Geometry = Plugin.GetType("WallDetailTool.AnnotationGeometry", true);
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

    [CommandMethod("WDTLABELGEOMCHECK")]
    public void GeometryCheck()
    {
        Report("WDT_LABEL_GEOM", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object rebar = Rebar(db, tr);
                Polyline steel = (Polyline)tr.GetObject((ObjectId)Field(rebar, "EntityId"), OpenMode.ForRead);
                Point3d a = steel.GetPoint3dAt(0), b = steel.GetPoint3dAt(1);
                Point3d anchor = new Point3d((a.X + b.X) / 2, (a.Y + b.Y) / 2, a.Z);
                TextStyleTable styles = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                ObjectId style = styles.Has("TSSD") ? styles["TSSD"] : db.Textstyle;
                foreach (bool elbow in new[] { false, true })
                foreach (double scale in new[] { 1.0, .001 })
                {
                    Point3d end = new Point3d(anchor.X + 400 * scale,
                        anchor.Y + (elbow ? 200 * scale : 0), anchor.Z);
                    object[] args = { db, steel, anchor, end, "%%c12", style, 62.5 * scale, null, null, null };
                    Call(Geometry, "Build", args);
                    using (var text = (DBText)args[7])
                    using (var leader = (Polyline)args[8])
                    using (var slash = (Polyline)args[9])
                    {
                        Require(text.HorizontalMode == TextHorizontalMode.TextRight &&
                            Math.Abs(text.AlignmentPoint.X - end.X) < 1e-7,
                            "Text must end at underline endpoint");
                        Require(leader.NumberOfVertices == (elbow ? 3 : 2) &&
                            Math.Abs(leader.GetPoint2dAt(leader.NumberOfVertices - 1).Y - end.Y) < 1e-7,
                            "Leader shape wrong");
                        if (elbow) Require(Math.Abs(leader.GetPoint2dAt(0).X - leader.GetPoint2dAt(1).X) < 1e-7 &&
                            Math.Abs(leader.GetPoint2dAt(1).Y - leader.GetPoint2dAt(2).Y) < 1e-7,
                            "Leader is not orthogonal");
                        Require(Math.Abs(slash.GetStartWidthAt(0) - 40 * scale) < 1e-7 &&
                            Math.Abs(slash.GetPoint2dAt(0).GetDistanceTo(slash.GetPoint2dAt(1)) - 150 * scale) < 1e-7,
                            "Slash size wrong");
                    }
                }
            }
        });
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
                using (var label = new DBText { Position = new Point3d(800, 142.5, 0),
                    Height = 62.5, TextString = "%%c12", HorizontalMode = TextHorizontalMode.TextRight,
                    AlignmentPoint = new Point3d(800, 142.5, 0) })
                using (var leader = new Polyline())
                using (var slash = new Polyline())
                {
                    leader.AddVertexAt(0, new Point2d(400, 31), 0, 0, 0);
                    leader.AddVertexAt(1, new Point2d(400, 135), 0, 0, 0);
                    leader.AddVertexAt(2, new Point2d(800, 135), 0, 0, 0);
                    slash.AddVertexAt(0, new Point2d(347, -22), 0, 40, 40);
                    slash.AddVertexAt(1, new Point2d(453, 84), 0, 0, 0);
                    Call(Service, "Create", db, tr, Field(rebar, "Id"), db.CurrentSpaceId, label, leader, slash, "Φ12");
                }
                tr.Commit();
            }
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object annotation = Annotation(db, tr);
                object rebar = Rebar(db, tr);
                DBText text = tr.GetObject((ObjectId)Field(annotation, "TextId"), OpenMode.ForRead) as DBText;
                Polyline leader = tr.GetObject((ObjectId)Field(annotation, "LeaderId"), OpenMode.ForRead) as Polyline;
                Polyline slash = tr.GetObject((ObjectId)Field(annotation, "SlashId"), OpenMode.ForRead) as Polyline;
                Polyline steel = tr.GetObject((ObjectId)Field(rebar, "EntityId"), OpenMode.ForRead) as Polyline;
                Require(text != null && leader != null && slash != null && text.TextString == "%%c12" &&
                    text.Layer == "S-TEXT" && leader.Layer == "S-TEXT" && slash.Layer == "S-TEXT" &&
                    Math.Abs(text.Height - 62.5) < 1e-9, "Annotation style, layer or height wrong");
                Require(leader.NumberOfVertices == 3 && leader.GetPoint2dAt(0).X == leader.GetPoint2dAt(1).X &&
                    leader.GetPoint2dAt(1).Y == leader.GetPoint2dAt(2).Y &&
                    Math.Abs(slash.GetStartWidthAt(0) - 40) < 1e-9 &&
                    Math.Abs(slash.GetPoint2dAt(0).GetDistanceTo(slash.GetPoint2dAt(1)) - 150) < 1,
                    "Leader elbow or slash dimension wrong");
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

    [CommandMethod("WDTLABELLAYOUTCHECK")]
    public void Layout()
    {
        Report("WDT_LABEL_LAYOUT", () =>
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            ObjectId oldLeader, oldSlash;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object annotation = Annotation(db, tr);
                object rebar = Rebar(db, tr);
                DBText saved = (DBText)tr.GetObject((ObjectId)Field(annotation, "TextId"), OpenMode.ForRead);
                Polyline steel = (Polyline)tr.GetObject((ObjectId)Field(rebar, "EntityId"), OpenMode.ForRead);
                Point3d a = steel.GetPoint3dAt(0), b = steel.GetPoint3dAt(1);
                Point3d anchor = new Point3d((a.X + b.X) / 2, (a.Y + b.Y) / 2, a.Z);
                ObjectId style = saved.TextStyleId;
                object[] args = { db, steel, anchor,
                    new Point3d(anchor.X + 500, anchor.Y + 220, anchor.Z),
                    saved.TextString, style, 62.5, null, null, null };
                Call(Geometry, "Build", args);
                oldLeader = (ObjectId)Field(annotation, "LeaderId");
                oldSlash = (ObjectId)Field(annotation, "SlashId");
                using (var text = (DBText)args[7])
                using (var leader = (Polyline)args[8])
                using (var slash = (Polyline)args[9])
                    Call(Service, "Relayout", db, tr, annotation, saved.TextString, saved.Position,
                        text, leader, slash, "Φ16");
                tr.Commit();
            }
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                object annotation = Annotation(db, tr);
                DBText text = (DBText)tr.GetObject((ObjectId)Field(annotation, "TextId"), OpenMode.ForRead);
                Require(oldLeader.IsErased && (oldSlash.IsNull || oldSlash.IsErased) &&
                    !((ObjectId)Field(annotation, "LeaderId")).IsErased &&
                    !((ObjectId)Field(annotation, "SlashId")).IsErased &&
                    text.HorizontalMode == TextHorizontalMode.TextRight,
                    "Relayout did not replace geometry atomically");
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
                    !((ObjectId)Field(annotation, "LeaderId")).IsErased &&
                    !((ObjectId)Field(annotation, "SlashId")).IsErased,
                    "Annotation deleted without confirmation");
            }
        });
    }
}
