using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;

namespace WallDetailTool
{
    // All records live in the drawing's named-object dictionary. A soft pointer keeps
    // the entity link across save/reopen while allowing ordinary erase and undo.
    internal static class DrawingData
    {
        private const string RootKey = "WDT_DATA";
        private const string VersionKey = "SCHEMA";
        private const int Version = 3;

        internal sealed class Node
        {
            internal Guid Id;
            internal string Name;
            internal double Cover;
            internal double Diameter;
        }
        internal sealed class Group
        {
            internal Guid Id;
            internal Guid NodeId;
            internal string Name;
            internal double Cover;
            internal double Diameter;
        }
        internal sealed class Rebar
        {
            internal Guid Id;
            internal Guid GroupId;
            internal ObjectId EntityId;
            internal double Cover;
            internal double Diameter;
            internal ObjectId SourceId;
            internal double Side;
            internal string SourceHash;
            internal string GeometryHash;
            internal string Layer;
            internal string Mode;
        }
        internal sealed class Annotation
        {
            internal Guid Id;
            internal Guid RebarId;
            internal ObjectId TextId;
            internal ObjectId LeaderId;
            internal string Label;
        }
        internal sealed class Index
        {
            internal readonly List<Node> Nodes = new List<Node>();
            internal readonly List<Group> Groups = new List<Group>();
            internal readonly List<Rebar> Rebars = new List<Rebar>();
            internal readonly List<Annotation> Annotations = new List<Annotation>();
        }

        private static int Code(DxfCode code) { return (int)code; }
        private static TypedValue Text(string value) { return new TypedValue(Code(DxfCode.Text), value); }
        private static TypedValue Real(double value) { return new TypedValue(Code(DxfCode.Real), value); }
        private static string Name(string value)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0 || value.Length > 120)
                throw new InvalidOperationException("名称须为1至120个字符。");
            return value;
        }
        private static void Sizes(double cover, double diameter)
        {
            if (!Commands.Finite(cover) || !Commands.Finite(diameter) || cover < 0 || diameter <= 0)
                throw new InvalidOperationException("保护层或钢筋直径无效。");
        }
        private static Guid Id(TypedValue value)
        {
            Guid id;
            if (value.TypeCode != Code(DxfCode.Text) ||
                !Guid.TryParse(value.Value as string, out id) || id == Guid.Empty)
                throw new InvalidOperationException("图纸中的 WallDetailTool 标识损坏。");
            return id;
        }
        private static string Label(TypedValue value)
        {
            if (value.TypeCode != Code(DxfCode.Text)) throw new InvalidOperationException("图纸中的名称记录损坏。");
            return Name(value.Value as string);
        }
        private static double Number(TypedValue value)
        {
            if (value.TypeCode != Code(DxfCode.Real)) throw new InvalidOperationException("图纸中的尺寸记录损坏。");
            double result = Convert.ToDouble(value.Value);
            if (!Commands.Finite(result)) throw new InvalidOperationException("图纸中的尺寸记录无效。");
            return result;
        }
        private static string OptionalText(TypedValue value)
        {
            if (value.TypeCode != Code(DxfCode.Text) || !(value.Value is string))
                throw new InvalidOperationException("图纸中的钢筋状态记录损坏。");
            return (string)value.Value;
        }
        internal static string GeometryHash(Polyline polyline)
        {
            var data = new StringBuilder();
            data.Append(polyline.NumberOfVertices).Append('|').Append(polyline.Closed).Append('|');
            data.Append(polyline.Normal.X.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            data.Append(polyline.Normal.Y.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            data.Append(polyline.Normal.Z.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            data.Append(polyline.Elevation.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                var p = polyline.GetPoint2dAt(i);
                data.Append(p.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(p.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(polyline.GetBulgeAt(i).ToString("R", CultureInfo.InvariantCulture)).Append(';');
            }
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(data.ToString())));
        }
        private static DBDictionary Root(Database database, Transaction tr, bool create)
        {
            DBDictionary named = (DBDictionary)tr.GetObject(database.NamedObjectsDictionaryId,
                create ? OpenMode.ForWrite : OpenMode.ForRead);
            if (!named.Contains(RootKey))
            {
                if (!create) return null;
                var root = new DBDictionary();
                named.SetAt(RootKey, root);
                tr.AddNewlyCreatedDBObject(root, true);
                var schema = new Xrecord { Data = new ResultBuffer(new TypedValue(Code(DxfCode.Int32), Version)) };
                root.SetAt(VersionKey, schema);
                tr.AddNewlyCreatedDBObject(schema, true);
                return root;
            }
            return (DBDictionary)tr.GetObject(named.GetAt(RootKey), create ? OpenMode.ForWrite : OpenMode.ForRead);
        }
        private static TypedValue[] Values(DBDictionary root, string key, Transaction tr)
        {
            Xrecord record = tr.GetObject(root.GetAt(key), OpenMode.ForRead) as Xrecord;
            if (record == null || record.Data == null)
                throw new InvalidOperationException("图纸中的 WallDetailTool 记录损坏。");
            return record.Data.AsArray();
        }
        private static void Add(DBDictionary root, string key, Transaction tr, params TypedValue[] data)
        {
            if (root.Contains(key)) throw new InvalidOperationException("图纸中存在重复标识。");
            var record = new Xrecord { Data = new ResultBuffer(data) };
            root.SetAt(key, record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        internal static Index Read(Database database, Transaction tr)
        {
            var index = new Index();
            DBDictionary root = Root(database, tr, false);
            if (root == null) return index;
            if (!root.Contains(VersionKey)) throw new InvalidOperationException("缺少 WallDetailTool 数据版本，已禁止写入。");
            TypedValue[] version = Values(root, VersionKey, tr);
            if (version.Length != 1 || version[0].TypeCode != Code(DxfCode.Int32) ||
                (Convert.ToInt32(version[0].Value) < 1 || Convert.ToInt32(version[0].Value) > Version))
                throw new InvalidOperationException("WallDetailTool 图纸数据版本未知，已禁止写入。请保留图纸副本。");
            int schema = Convert.ToInt32(version[0].Value);
            var ids = new HashSet<Guid>();
            foreach (DBDictionaryEntry entry in root)
            {
                if (entry.Key == VersionKey) continue;
                TypedValue[] v = Values(root, entry.Key, tr);
                if (entry.Key.StartsWith("N_", StringComparison.Ordinal))
                {
                    if (v.Length != 4) throw new InvalidOperationException("节点记录损坏。");
                    var n = new Node { Id = Id(v[0]), Name = Label(v[1]), Cover = Number(v[2]), Diameter = Number(v[3]) };
                    Sizes(n.Cover, n.Diameter);
                    if (entry.Key != "N_" + n.Id.ToString("N") || !ids.Add(n.Id)) throw new InvalidOperationException("节点标识损坏或重复。");
                    index.Nodes.Add(n);
                }
                else if (entry.Key.StartsWith("G_", StringComparison.Ordinal))
                {
                    if (v.Length != 5) throw new InvalidOperationException("钢筋组记录损坏。");
                    var g = new Group { Id = Id(v[0]), NodeId = Id(v[1]), Name = Label(v[2]), Cover = Number(v[3]), Diameter = Number(v[4]) };
                    Sizes(g.Cover, g.Diameter);
                    if (entry.Key != "G_" + g.Id.ToString("N") || !ids.Add(g.Id)) throw new InvalidOperationException("钢筋组标识损坏或重复。");
                    index.Groups.Add(g);
                }
                else if (entry.Key.StartsWith("R_", StringComparison.Ordinal))
                {
                    if ((v.Length != 5 && !(schema >= 2 && v.Length == 11)) || v[2].TypeCode != Code(DxfCode.SoftPointerId))
                        throw new InvalidOperationException("钢筋关联记录损坏。");
                    var r = new Rebar { Id = Id(v[0]), GroupId = Id(v[1]), EntityId = (ObjectId)v[2].Value,
                        Cover = Number(v[3]), Diameter = Number(v[4]) };
                    Sizes(r.Cover, r.Diameter);
                    if (v.Length == 11)
                    {
                        if (v[5].TypeCode == Code(DxfCode.SoftPointerId)) r.SourceId = (ObjectId)v[5].Value;
                        else if (OptionalText(v[5]) != "") throw new InvalidOperationException("源轮廓关联记录损坏。");
                        r.Side = Number(v[6]);
                        r.SourceHash = OptionalText(v[7]);
                        r.GeometryHash = OptionalText(v[8]);
                        r.Layer = Label(v[9]);
                        r.Mode = OptionalText(v[10]);
                        if ((r.SourceId.IsNull && r.Side != 0) || (!r.SourceId.IsNull && Math.Abs(r.Side) != 1) ||
                            (r.Mode != "Contour" && r.Mode != "Free"))
                            throw new InvalidOperationException("钢筋路径模式记录损坏。");
                    }
                    else { r.SourceHash = ""; r.GeometryHash = ""; r.Layer = ""; r.Mode = "Legacy"; }
                    if (entry.Key != "R_" + r.Id.ToString("N") || !ids.Add(r.Id)) throw new InvalidOperationException("钢筋标识损坏或重复。");
                    index.Rebars.Add(r);
                }
                else if (entry.Key.StartsWith("A_", StringComparison.Ordinal))
                {
                    if (schema < 3 || v.Length != 5 ||
                        v[2].TypeCode != Code(DxfCode.SoftPointerId) ||
                        v[3].TypeCode != Code(DxfCode.SoftPointerId))
                        throw new InvalidOperationException("钢筋标注关联记录损坏。");
                    var a = new Annotation { Id = Id(v[0]), RebarId = Id(v[1]),
                        TextId = (ObjectId)v[2].Value, LeaderId = (ObjectId)v[3].Value,
                        Label = Label(v[4]) };
                    if (entry.Key != "A_" + a.Id.ToString("N") || !ids.Add(a.Id) ||
                        a.TextId.IsNull || a.LeaderId.IsNull)
                        throw new InvalidOperationException("钢筋标注标识损坏或重复。");
                    index.Annotations.Add(a);
                }
                else throw new InvalidOperationException("图纸含未知 WallDetailTool 记录，已禁止写入。");
            }
            foreach (Group g in index.Groups)
                if (!index.Nodes.Exists(n => n.Id == g.NodeId)) throw new InvalidOperationException("钢筋组缺少所属节点。");
            foreach (Rebar r in index.Rebars)
                if (!index.Groups.Exists(g => g.Id == r.GroupId)) throw new InvalidOperationException("钢筋缺少所属组。");
            foreach (Annotation a in index.Annotations)
                if (!index.Rebars.Exists(r => r.Id == a.RebarId)) throw new InvalidOperationException("标注缺少所属钢筋。");
            return index;
        }

        internal static Guid AddNode(Database database, Transaction tr, string name, double cover, double diameter)
        {
            Name(name); Sizes(cover, diameter); Read(database, tr);
            Guid id = Guid.NewGuid();
            Add(Root(database, tr, true), "N_" + id.ToString("N"), tr,
                Text(id.ToString("D")), Text(Name(name)), Real(cover), Real(diameter));
            return id;
        }
        internal static Guid AddGroup(Database database, Transaction tr, Guid nodeId, string name, double cover, double diameter)
        {
            Name(name); Sizes(cover, diameter);
            if (!Read(database, tr).Nodes.Exists(n => n.Id == nodeId)) throw new InvalidOperationException("所属节点不存在。");
            Guid id = Guid.NewGuid();
            Add(Root(database, tr, true), "G_" + id.ToString("N"), tr,
                Text(id.ToString("D")), Text(nodeId.ToString("D")), Text(Name(name)), Real(cover), Real(diameter));
            return id;
        }
        internal static Guid AddRebar(Database database, Transaction tr, Guid groupId, ObjectId entityId, double cover, double diameter)
        {
            Sizes(cover, diameter);
            if (entityId.IsNull || !Read(database, tr).Groups.Exists(g => g.Id == groupId))
                throw new InvalidOperationException("钢筋组或实体不存在。");
            Guid id = Guid.NewGuid();
            Add(Root(database, tr, true), "R_" + id.ToString("N"), tr,
                Text(id.ToString("D")), Text(groupId.ToString("D")),
                new TypedValue(Code(DxfCode.SoftPointerId), entityId), Real(cover), Real(diameter));
            return id;
        }

        private static void Upgrade(Database database, Transaction tr)
        {
            DBDictionary root = Root(database, tr, true);
            Xrecord schema = (Xrecord)tr.GetObject(root.GetAt(VersionKey), OpenMode.ForWrite);
            TypedValue[] values = schema.Data.AsArray();
            if (values.Length != 1 || values[0].TypeCode != Code(DxfCode.Int32) ||
                (Convert.ToInt32(values[0].Value) < 1 || Convert.ToInt32(values[0].Value) > Version))
                throw new InvalidOperationException("未知图纸数据版本，拒绝更新。");
            schema.Data = new ResultBuffer(new TypedValue(Code(DxfCode.Int32), Version));
        }
        private static TypedValue Source(ObjectId sourceId)
        {
            return sourceId.IsNull ? Text("") : new TypedValue(Code(DxfCode.SoftPointerId), sourceId);
        }
        private static ResultBuffer RebarValues(Rebar rebar)
        {
            return new ResultBuffer(Text(rebar.Id.ToString("D")), Text(rebar.GroupId.ToString("D")),
                new TypedValue(Code(DxfCode.SoftPointerId), rebar.EntityId), Real(rebar.Cover), Real(rebar.Diameter),
                Source(rebar.SourceId), Real(rebar.Side), Text(rebar.SourceHash ?? ""),
                Text(rebar.GeometryHash ?? ""), Text(Name(rebar.Layer)), Text(rebar.Mode));
        }
        internal static Guid AddLinkedRebar(Database database, Transaction tr, Guid groupId,
            Polyline entity, ObjectId sourceId, double side, string sourceHash, double cover, double diameter)
        {
            Sizes(cover, diameter);
            if (entity == null || entity.ObjectId.IsNull || sourceId.IsNull || Math.Abs(side) != 1 ||
                !Read(database, tr).Groups.Exists(g => g.Id == groupId))
                throw new InvalidOperationException("新钢筋缺少有效的轮廓或钢筋组。");
            Upgrade(database, tr);
            var rebar = new Rebar { Id = Guid.NewGuid(), GroupId = groupId, EntityId = entity.ObjectId,
                SourceId = sourceId, Side = side, SourceHash = sourceHash,
                GeometryHash = GeometryHash(entity), Layer = entity.Layer, Mode = "Contour",
                Cover = cover, Diameter = diameter };
            DBDictionary root = Root(database, tr, true);
            Xrecord record = new Xrecord { Data = RebarValues(rebar) };
            root.SetAt("R_" + rebar.Id.ToString("N"), record);
            tr.AddNewlyCreatedDBObject(record, true);
            return rebar.Id;
        }
        internal static void UpdateRebar(Database database, Transaction tr, Rebar rebar)
        {
            Sizes(rebar.Cover, rebar.Diameter);
            if (!Read(database, tr).Rebars.Exists(r => r.Id == rebar.Id))
                throw new InvalidOperationException("钢筋关联已变化，请刷新。");
            Upgrade(database, tr);
            DBDictionary root = Root(database, tr, true);
            Xrecord record = (Xrecord)tr.GetObject(root.GetAt("R_" + rebar.Id.ToString("N")), OpenMode.ForWrite);
            record.Data = RebarValues(rebar);
        }

        internal static Guid AddAnnotation(Database database, Transaction tr, Guid rebarId,
            DBText textEntity, Line leader, string label)
        {
            label = Name(label);
            Index index = Read(database, tr);
            if (!index.Rebars.Exists(r => r.Id == rebarId) ||
                index.Annotations.Exists(a => a.RebarId == rebarId) ||
                textEntity == null || leader == null || textEntity.ObjectId.IsNull || leader.ObjectId.IsNull)
                throw new InvalidOperationException("该钢筋不存在、已有标注或标注实体无效。");
            Upgrade(database, tr);
            Guid id = Guid.NewGuid();
            Add(Root(database, tr, true), "A_" + id.ToString("N"), tr,
                Text(id.ToString("D")), Text(rebarId.ToString("D")),
                new TypedValue(Code(DxfCode.SoftPointerId), textEntity.ObjectId),
                new TypedValue(Code(DxfCode.SoftPointerId), leader.ObjectId), Text(label));
            return id;
        }

        internal static void UpdateAnnotation(Database database, Transaction tr, Annotation annotation)
        {
            Index index = Read(database, tr);
            Annotation existing = index.Annotations.Find(a => a.Id == annotation.Id);
            if (existing == null || existing.RebarId != annotation.RebarId ||
                existing.TextId != annotation.TextId || existing.LeaderId != annotation.LeaderId)
                throw new InvalidOperationException("标注关联已变化，请刷新。");
            Upgrade(database, tr);
            DBDictionary root = Root(database, tr, true);
            Xrecord record = (Xrecord)tr.GetObject(root.GetAt("A_" + annotation.Id.ToString("N")), OpenMode.ForWrite);
            record.Data = new ResultBuffer(Text(annotation.Id.ToString("D")), Text(annotation.RebarId.ToString("D")),
                new TypedValue(Code(DxfCode.SoftPointerId), annotation.TextId),
                new TypedValue(Code(DxfCode.SoftPointerId), annotation.LeaderId), Text(Name(annotation.Label)));
        }

        internal static void UpdateDefaults(Database database, Transaction tr, Guid id, string name, double cover, double diameter)
        {
            name = Name(name); Sizes(cover, diameter);
            Index index = Read(database, tr);
            Node node = index.Nodes.Find(n => n.Id == id);
            Group group = index.Groups.Find(g => g.Id == id);
            if (node == null && group == null) throw new InvalidOperationException("所选节点或钢筋组不存在。");
            DBDictionary root = Root(database, tr, true);
            string key = (node == null ? "G_" : "N_") + id.ToString("N");
            Xrecord record = (Xrecord)tr.GetObject(root.GetAt(key), OpenMode.ForWrite);
            record.Data = node != null
                ? new ResultBuffer(Text(id.ToString("D")), Text(name), Real(cover), Real(diameter))
                : new ResultBuffer(Text(id.ToString("D")), Text(group.NodeId.ToString("D")), Text(name), Real(cover), Real(diameter));
        }
    }
}
