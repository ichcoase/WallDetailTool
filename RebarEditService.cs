using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace WallDetailTool
{
    internal static class RebarEditService
    {
        // Caller owns one AutoCAD transaction so the entity, layer and metadata undo together.
        internal static void Apply(Database database, Transaction tr, DrawingData.Rebar data,
            Polyline current, Polyline proposed, ObjectId sourceId, double side, string sourceHash,
            double cover, double diameter, string layer, bool replacePath, bool manualPath)
        {
            if (data == null || current == null || proposed == null || data.EntityId != current.ObjectId)
                throw new InvalidOperationException("钢筋关联与实体不一致。");
            LayerTools.Ensure(database, tr, layer);
            Polyline saved;
            if (replacePath)
            {
                if (sourceId.IsNull || Math.Abs(side) != 1)
                    throw new InvalidOperationException("重新生成需要已确认的轮廓和方向。");
                saved = (Polyline)proposed.Clone();
                saved.SetPropertiesFrom(current);
                saved.Layer = layer;
                Commands.SetRebarWidth(saved, diameter * Commands.MillimetresToDrawingUnits(database));
                BlockTableRecord space = (BlockTableRecord)tr.GetObject(current.OwnerId, OpenMode.ForWrite);
                space.AppendEntity(saved);
                tr.AddNewlyCreatedDBObject(saved, true);
                current.UpgradeOpen();
                current.Erase();
                data.EntityId = saved.ObjectId;
                data.SourceId = sourceId;
                data.Side = side;
                data.SourceHash = sourceHash;
                data.Mode = "Contour";
            }
            else
            {
                current.UpgradeOpen();
                current.Layer = layer;
                saved = current;
                data.Mode = manualPath || data.Mode == "Legacy" ? "Free" : data.Mode;
            }
            data.Cover = cover;
            data.Diameter = diameter;
            data.Layer = layer;
            data.GeometryHash = DrawingData.GeometryHash(saved);
            DrawingData.UpdateRebar(database, tr, data);
        }
    }
}
