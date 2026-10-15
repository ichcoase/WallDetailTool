using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace WallDetailTool
{
    internal static class LayerTools
    {
        internal static string CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Length > 120 ||
                name.IndexOfAny(new[] { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=' }) >= 0)
                throw new InvalidOperationException("图层名称为空、过长或含有不允许的字符。");
            return name;
        }
        internal static List<string> Names(Database db, Transaction tr)
        {
            var names = new List<string>();
            var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (ObjectId id in layers)
                names.Add(((LayerTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name);
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }
        internal static bool Exists(Database db, Transaction tr, string name)
        {
            var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return layers.Has(CheckName(name));
        }
        internal static bool ConfirmNew(Database db, Editor editor, string name)
        {
            name = CheckName(name);
            using (Transaction tr = db.TransactionManager.StartTransaction())
                if (Exists(db, tr, name)) return true;
            var options = new PromptKeywordOptions("\n图层“" + name + "”不存在，是否创建？ [是(Y)/否(N)] <否>: ");
            options.Keywords.Add("Y");
            options.Keywords.Add("N");
            options.Keywords.Default = "N";
            PromptResult result = editor.GetKeywords(options);
            return result.Status == PromptStatus.OK && result.StringResult == "Y";
        }
        internal static void Ensure(Database db, Transaction tr, string name)
        {
            name = CheckName(name);
            var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (layers.Has(name)) return;
            layers.UpgradeOpen();
            var layer = new LayerTableRecord { Name = name };
            layers.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
        }
    }
}
