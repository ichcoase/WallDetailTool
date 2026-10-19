using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using TransientManager = Autodesk.AutoCAD.GraphicsInterface.TransientManager;
using TransientDrawingMode = Autodesk.AutoCAD.GraphicsInterface.TransientDrawingMode;

namespace WallDetailTool
{
    // First version: an orthogonal, planar beam / slab / upstand section.
    // Architectural entities are read only. WDTUPCONTOUR creates editable copies;
    // WDTUPDRAW reads those copies and commits the complete detail in one transaction.
    public partial class Commands
    {
        private const string ConcreteLayer = "WDT-CONCRETE";
        private const string LineLayer = "WDT-LINE-REBAR";
        private const string DotLayer = "WDT-DOT-REBAR";
        private const string LabelLayer = "WDT-REBAR-TEXT";
        private const string DimensionLayer = "WDT-DIMENSION";
        private const string ParamRecord = "WDT_UPSTAND_LAST";
        private const double Epsilon = 1e-6;
        private static readonly IntegerCollection PreviewViewports = new IntegerCollection();

        private sealed class Rect
        {
            internal double L, R, B, T, Z;
            internal double W { get { return R - L; } }
            internal double H { get { return T - B; } }
            internal bool Same(Rect b) { return Near(L,b.L) && Near(R,b.R) && Near(B,b.B) && Near(T,b.T) && Near(Z,b.Z); }
        }

        private sealed class DetailParams
        {
            internal double Cover = 25, LineDiameter = 8, LineSpacing = 150;
            internal double DotDiameter = 6, DotSpacing = 200;
            internal double LeftAnchor, RightAnchor;
        }

        private static bool Near(double a, double b) { return Math.Abs(a-b) <= Epsilon; }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        [CommandMethod("WDTUPDOTSIZE", CommandFlags.Modal)]
        public void SetUpstandDotSize()
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                var option = new PromptDoubleOptions("\n圆点筋图形显示直径（mm）: ") { AllowNegative = false, AllowZero = false,
                    DefaultValue = PluginSettings.ReadDotDiameterMillimetres(), UseDefaultValue = true };
                PromptDoubleResult result = doc.Editor.GetDouble(option);
                if (result.Status == PromptStatus.OK) PluginSettings.SaveDotDiameterMillimetres(result.Value);
            }
            catch (System.Exception ex) { doc.Editor.WriteMessage("\n未保存设置：" + ex.Message); }
        }

        [CommandMethod("WDTUPCONTOUR", CommandFlags.Modal)]
        public void ExtractUpstandContour()
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            try
            {
                ed.WriteMessage("\n框选典型梁上反坎节点；支持闭合矩形多段线及四条直线组成的矩形。原图元不修改。");
                PromptSelectionResult selected = ed.GetSelection();
                if (selected.Status != PromptStatus.OK) return;
                var rectangles = new List<Rect>();
                var lines = new List<Line>();
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject item in selected.Value)
                    {
                        if (item == null) continue;
                        Entity entity = tr.GetObject(item.ObjectId, OpenMode.ForRead) as Entity;
                        Rect r = ReadRect(entity as Polyline);
                        if (r != null) AddUnique(rectangles, r);
                        Line line = entity as Line;
                        if (line != null) lines.Add(line);
                    }
                    CollectLineRectangles(lines, rectangles);
                }
                Rect beam, slab, upstand;
                if (!TryClassify(rectangles, out beam, out slab, out upstand))
                {
                    if (rectangles.Count < 3)
                        throw new InvalidOperationException("选区内只找到 " + rectangles.Count +
                            " 个完整矩形；需要梁、板、反坎各一个。闭合矩形多段线及四条直线闭环均可识别。");
                    ed.WriteMessage("\n自动判断梁板反坎关系不唯一或轮廓有错位，请按提示手动指定三个矩形。");
                    beam = PickRect(doc, "梁矩形"); if (beam == null) return;
                    slab = PickRect(doc, "板矩形"); if (slab == null) return;
                    upstand = PickRect(doc, "反坎矩形"); if (upstand == null) return;
                    CheckRelations(beam, slab, upstand);
                }
                // This transaction is intentionally after all checks. A failed extraction leaves no work contours.
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    EnsureLayer(doc.Database, tr, "WDT-WORK-BEAM", 1);
                    EnsureLayer(doc.Database, tr, "WDT-WORK-SLAB", 3);
                    EnsureLayer(doc.Database, tr, "WDT-WORK-UPSTAND", 5);
                    BlockTableRecord space = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);
                    Append(space, tr, Rectangle(beam, "WDT-WORK-BEAM"));
                    Append(space, tr, Rectangle(slab, "WDT-WORK-SLAB"));
                    Append(space, tr, Rectangle(upstand, "WDT-WORK-UPSTAND"));
                    tr.Commit();
                }
                ed.WriteMessage("\n已生成红色梁、绿色板、蓝色反坎工作轮廓。可用 CAD 夹点/MOVE/STRETCH 修正，再运行 WDTUPDRAW。");
            }
            catch (System.Exception ex) { ed.WriteMessage("\n未提取轮廓：" + ex.Message); }
        }

        [CommandMethod("WDTUPDRAW", CommandFlags.Modal)]
        public void DrawUpstand()
        {
            Document doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            try
            {
                Rect beam = PickRect(doc, "梁工作轮廓"); if (beam == null) return;
                Rect slab = PickRect(doc, "板工作轮廓"); if (slab == null) return;
                Rect upstand = PickRect(doc, "反坎工作轮廓"); if (upstand == null) return;
                CheckRelations(beam, slab, upstand);
                double scale = MillimetresToDrawingUnits(doc.Database);
                if (doc.Database.Insunits == UnitsValue.Undefined)
                    ed.WriteMessage("\n无单位图纸按 1 图形单位 = 1 mm。所有输入均为 mm。");
                DetailParams values = ReadLast(doc.Database);
                while (true)
                {
                    if (!ReadParameters(ed, values)) return;
                    List<Entity> preview = BuildDetail(doc.Database, beam, slab, upstand, values, scale);
                    try
                    {
                        ShowPreview(preview);
                        var choice = new PromptKeywordOptions("\n检查红色预览 [确认(C)/修改(M)/取消(X)] <修改>: ");
                        choice.Keywords.Add("C"); choice.Keywords.Add("M"); choice.Keywords.Add("X");
                        choice.Keywords.Default = "M";
                        PromptResult result = ed.GetKeywords(choice);
                        if (result.Status != PromptStatus.OK || result.StringResult == "X") return;
                        if (result.StringResult == "M") continue;
                        PromptPointResult insertion = ed.GetPoint("\n指定新大样的梁左下角插入位置: ");
                        if (insertion.Status != PromptStatus.OK) return;
                        Vector3d shift = insertion.Value - new Point3d(beam.L, beam.B, beam.Z);
                        using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                        {
                            EnsureLayer(doc.Database, tr, ConcreteLayer, 7);
                            EnsureLayer(doc.Database, tr, LineLayer, 1);
                            EnsureLayer(doc.Database, tr, DotLayer, 2);
                            EnsureLayer(doc.Database, tr, LabelLayer, 4);
                            EnsureLayer(doc.Database, tr, DimensionLayer, 6);
                            BlockTableRecord space = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);
                            // Regenerate as database entities; transient clones are never adopted by the DB.
                            foreach (Entity entity in BuildDetail(doc.Database, beam, slab, upstand, values, scale))
                            {
                                entity.TransformBy(Matrix3d.Displacement(shift));
                                Append(space, tr, entity);
                            }
                            WriteLast(doc.Database, tr, values);
                            tr.Commit();
                        }
                        ed.WriteMessage("\n完整大样已生成。锚固长度为您输入的设计值，请审核构造与尺寸。UNDO 可撤销本次生成。");
                        return;
                    }
                    finally { HidePreview(preview); foreach (Entity entity in preview) entity.Dispose(); }
                }
            }
            catch (System.Exception ex) { ed.WriteMessage("\n未生成梁上反坎大样：" + ex.Message); }
        }

        private static Rect PickRect(Document doc, string name)
        {
            var options = new PromptEntityOptions("\n选择" + name + "（闭合矩形多段线）: ");
            options.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult result = doc.Editor.GetEntity(options);
            if (result.Status != PromptStatus.OK) return null;
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                Rect rect = ReadRect(tr.GetObject(result.ObjectId, OpenMode.ForRead) as Polyline);
                Check(rect != null, name + "必须是轴对齐的闭合矩形二维多段线，可先用原生 PEDIT/JOIN 修正。");
                return rect;
            }
        }

        private static Rect ReadRect(Polyline p)
        {
            if (p == null || !p.Closed || p.NumberOfVertices != 4 ||
                Math.Abs(p.Normal.X) > Epsilon || Math.Abs(p.Normal.Y) > Epsilon || p.Normal.Z < 1-Epsilon) return null;
            var r = new Rect { L = double.PositiveInfinity, B = double.PositiveInfinity,
                R = double.NegativeInfinity, T = double.NegativeInfinity, Z = p.Elevation };
            for (int i = 0; i < 4; i++)
            {
                if (!Near(p.GetBulgeAt(i), 0)) return null;
                Point3d a = p.GetPoint3dAt(i), b = p.GetPoint3dAt((i+1)%4);
                if (!Near(a.Z,r.Z) || !Near(a.Z,b.Z) || (Near(a.X,b.X) == Near(a.Y,b.Y))) return null;
                r.L = Math.Min(r.L,a.X); r.R = Math.Max(r.R,a.X);
                r.B = Math.Min(r.B,a.Y); r.T = Math.Max(r.T,a.Y);
            }
            if (r.W <= Epsilon || r.H <= Epsilon) return null;
            for (int i = 0; i < 4; i++)
            {
                Point3d v = p.GetPoint3dAt(i);
                if (!(Near(v.X,r.L) || Near(v.X,r.R)) || !(Near(v.Y,r.B) || Near(v.Y,r.T))) return null;
            }
            return r;
        }

        private static void AddUnique(List<Rect> result, Rect value)
        {
            foreach (Rect r in result) if (r.Same(value)) return;
            result.Add(value);
        }

        private static void CollectLineRectangles(List<Line> lines, List<Rect> result)
        {
            // Only complete four-edge loops qualify; independent architectural lines remain untouched.
            foreach (Line bottom in lines) foreach (Line top in lines)
            {
                if (bottom == top || !Near(bottom.StartPoint.Y,bottom.EndPoint.Y) ||
                    !Near(top.StartPoint.Y,top.EndPoint.Y) || !Near(bottom.StartPoint.Z,top.StartPoint.Z)) continue;
                double b = bottom.StartPoint.Y, t = top.StartPoint.Y;
                if (t <= b + Epsilon) continue;
                double l = Math.Min(bottom.StartPoint.X,bottom.EndPoint.X), r = Math.Max(bottom.StartPoint.X,bottom.EndPoint.X);
                if (r-l <= Epsilon || !Near(l,Math.Min(top.StartPoint.X,top.EndPoint.X)) ||
                    !Near(r,Math.Max(top.StartPoint.X,top.EndPoint.X))) continue;
                bool left = false, right = false;
                foreach (Line side in lines)
                {
                    if (!Near(side.StartPoint.X,side.EndPoint.X) || !Near(side.StartPoint.Z,bottom.StartPoint.Z)) continue;
                    double low = Math.Min(side.StartPoint.Y,side.EndPoint.Y), high = Math.Max(side.StartPoint.Y,side.EndPoint.Y);
                    if (!Near(low,b) || !Near(high,t)) continue;
                    if (Near(side.StartPoint.X,l)) left = true;
                    if (Near(side.StartPoint.X,r)) right = true;
                }
                if (left && right) AddUnique(result,new Rect { L=l,R=r,B=b,T=t,Z=bottom.StartPoint.Z });
            }
        }

        private static bool TryClassify(List<Rect> all, out Rect beam, out Rect slab, out Rect upstand)
        {
            beam = slab = upstand = null;
            int count = 0;
            foreach (Rect b in all) foreach (Rect s in all) foreach (Rect u in all)
            {
                if (b == s || b == u || s == u) continue;
                if (!Relations(b,s,u)) continue;
                beam=b; slab=s; upstand=u; count++;
            }
            return count == 1;
        }

        private static bool Relations(Rect b, Rect s, Rect u)
        {
            double align = Math.Max(1e-3, Math.Min(Math.Min(b.H,u.H), Math.Max(s.H,b.H)) * .05);
            double side = Math.Max(1e-3, b.W * .02);
            bool samePlane = Math.Abs(b.Z-s.Z) <= align && Math.Abs(b.Z-u.Z) <= align;
            bool upstandAboveBeam = Math.Abs(u.B-b.T) <= align && u.T > b.T+align
                && u.L >= b.L-side && u.R <= b.R+side && u.W < b.W+side;
            bool slabTouchesBeam = Math.Abs(s.T-b.T) <= align || Math.Abs(s.B-b.T) <= align
                || Math.Abs(s.T-b.B) <= align || Math.Abs(s.B-b.B) <= align;
            bool slabNearBeam = s.R >= b.L-side && s.L <= b.R+side;
            return samePlane && b.W > b.H && u.W > Epsilon && u.H > Epsilon
                && s.W > s.H && upstandAboveBeam && slabTouchesBeam && slabNearBeam;
        }
        private static void CheckRelations(Rect b, Rect s, Rect u)
        {
            Check(Relations(b,s,u), "轮廓关系仍不符合典型梁上反坎：反坎应位于梁顶附近且不宽于梁，板应在梁边附近相接。请检查是否选错轮廓或修正错位。");
        }

        private static Polyline Rectangle(Rect r, string layer)
        {
            var p = new Polyline(4) { Closed=true, Elevation=r.Z, Layer=layer };
            p.AddVertexAt(0,new Point2d(r.L,r.B),0,0,0); p.AddVertexAt(1,new Point2d(r.R,r.B),0,0,0);
            p.AddVertexAt(2,new Point2d(r.R,r.T),0,0,0); p.AddVertexAt(3,new Point2d(r.L,r.T),0,0,0);
            return p;
        }

        private static bool Ask(Editor ed, string name, ref double value, bool zero=false)
        {
            var options = new PromptDoubleOptions("\n"+name+"（mm） <"+value.ToString("g")+">: ")
            { AllowNegative=false, AllowZero=zero, DefaultValue=value, UseDefaultValue=true };
            PromptDoubleResult result = ed.GetDouble(options);
            if (result.Status != PromptStatus.OK) return false;
            Check(Finite(result.Value) && (zero ? result.Value>=0 : result.Value>0), name+"无效。");
            value=result.Value; return true;
        }
        private static bool ReadParameters(Editor ed, DetailParams p)
        {
            ed.WriteMessage("\n初始模板：U筋%%c8@150、点筋%%c6@200。两侧锚固长度须输入经设计确认的值，不自动推算La/LaE。");
            if (!Ask(ed,"保护层",ref p.Cover,true) || !Ask(ed,"U筋规格直径",ref p.LineDiameter)
                || !Ask(ed,"U筋标注间距",ref p.LineSpacing) || !Ask(ed,"点筋规格直径",ref p.DotDiameter)
                || !Ask(ed,"点筋最大间距",ref p.DotSpacing)) return false;
            if (!AskAnchor(ed,"左侧伸入梁内长度（设计确认）",ref p.LeftAnchor)
                || !AskAnchor(ed,"右侧伸入梁内长度（设计确认）",ref p.RightAnchor)) return false;
            return true;
        }

        private static bool AskAnchor(Editor ed, string name, ref double value)
        {
            if (value > 0) return Ask(ed,name,ref value);
            var options = new PromptDoubleOptions("\n"+name+"（mm；须明确输入）: ")
                { AllowNegative=false, AllowZero=false, UseDefaultValue=false };
            PromptDoubleResult result = ed.GetDouble(options);
            if (result.Status != PromptStatus.OK) return false;
            Check(Finite(result.Value) && result.Value>0, name+"无效。");
            value=result.Value; return true;
        }

        private static List<Entity> BuildDetail(Database db, Rect beam, Rect slab, Rect up, DetailParams p, double scale)
        {
            var entities = new List<Entity>();
            try
            {
                double width = PluginSettings.ReadLineWidthMillimetres()*scale;
                double dot = PluginSettings.ReadDotDiameterMillimetres()*scale;
                double inset = p.Cover*scale + width/2; // This detail's approved display-based offset.
                double left=up.L+inset, right=up.R-inset, top=up.T-inset;
                double beamTop=beam.T;
                Check(right-left > width+2*dot+Epsilon, "反坎过窄，U筋与两列圆点筋无法放下。");
                Check(top > beamTop+dot+width+50*scale, "反坎高度不足，最下排圆点筋无法满足距梁顶50～150 mm。");
                Check(p.LeftAnchor*scale <= beam.H+Epsilon && p.RightAnchor*scale <= beam.H+Epsilon,
                    "输入的直锚长度超过梁高；没有已审核的弯折规则，停止生成。");
                entities.Add(Rectangle(beam,ConcreteLayer)); entities.Add(Rectangle(slab,ConcreteLayer));
                entities.Add(Rectangle(up,ConcreteLayer));
                var u = new Polyline(4) { Elevation=beam.Z, Layer=LineLayer };
                u.AddVertexAt(0,new Point2d(left,beamTop-p.LeftAnchor*scale),0,width,width);
                u.AddVertexAt(1,new Point2d(left,top),0,width,width);
                u.AddVertexAt(2,new Point2d(right,top),0,width,width);
                u.AddVertexAt(3,new Point2d(right,beamTop-p.RightAnchor*scale),0,width,width);
                u.ConstantWidth=width; entities.Add(u);
                double yFirst=top-width/2-dot/2;
                double bottomMin=beamTop+50*scale+dot/2, bottomMax=beamTop+150*scale+dot/2;
                Check(yFirst >= bottomMin-Epsilon, "反坎高度不足，无法布置第一排圆点筋。");
                double yLast=Math.Min(yFirst,bottomMax);
                double maximum=p.DotSpacing*scale;
                int rows = yFirst-yLast <= Epsilon ? 1 : (int)Math.Ceiling((yFirst-yLast)/maximum)+1;
                Check(rows > 0 && rows <= 10000, "圆点筋排数超出可处理范围。");
                double actual=rows == 1 ? 0 : (yFirst-yLast)/(rows-1);
                double xLeft=left+width/2+dot/2, xRight=right-width/2-dot/2;
                Check(xRight-xLeft >= dot-Epsilon, "两列点筋相交，几何空间不足。");
                for (int i=0;i<rows;i++)
                {
                    double y=yFirst-i*actual;
                    entities.Add(Dot(xLeft,y,beam.Z,dot)); entities.Add(Dot(xRight,y,beam.Z,dot));
                }
                double labelHeight= Math.Max(2.5*scale, Math.Min(20*scale, up.W/16));
                entities.Add(Text(db,"%%c"+p.LineDiameter.ToString("g")+"@"+p.LineSpacing.ToString("g"),
                    new Point3d(up.R+20*scale,up.T,beam.Z),labelHeight,LabelLayer));
                entities.Add(Text(db,"%%c"+p.DotDiameter.ToString("g")+"@"+p.DotSpacing.ToString("g"),
                    new Point3d(up.R+20*scale,up.T-3*labelHeight,beam.Z),labelHeight,LabelLayer));
                entities.Add(Text(db,"L="+p.LeftAnchor.ToString("g")+" / R="+p.RightAnchor.ToString("g")+" mm (审核)",
                    new Point3d(up.R+20*scale,up.T-6*labelHeight,beam.Z),labelHeight,LabelLayer));
                entities.Add(Dimension(db,new Point3d(up.L,up.T,beam.Z),new Point3d(up.R,up.T,beam.Z),
                    new Point3d((up.L+up.R)/2,up.T+30*scale,beam.Z),DimensionLayer));
                entities.Add(Dimension(db,new Point3d(up.L,beamTop,beam.Z),new Point3d(up.L,up.T,beam.Z),
                    new Point3d(up.L-30*scale,(beamTop+up.T)/2,beam.Z),DimensionLayer));
                entities.Add(Dimension(db,new Point3d(beam.L,beam.B,beam.Z),new Point3d(beam.R,beam.B,beam.Z),
                    new Point3d((beam.L+beam.R)/2,beam.B-30*scale,beam.Z),DimensionLayer));
                return entities;
            }
            catch { foreach (Entity e in entities) e.Dispose(); throw; }
        }

        private static Polyline Dot(double x,double y,double z,double diameter)
        {
            double r=diameter/2, bulge=Math.Tan(Math.PI/8);
            var p=new Polyline(4) { Closed=true, Elevation=z, Layer=DotLayer };
            p.AddVertexAt(0,new Point2d(x+r,y),bulge,0,0);
            p.AddVertexAt(1,new Point2d(x,y+r),bulge,0,0);
            p.AddVertexAt(2,new Point2d(x-r,y),bulge,0,0);
            p.AddVertexAt(3,new Point2d(x,y-r),bulge,0,0);
            return p;
        }
        private static DBText Text(Database db,string value,Point3d position,double height,string layer)
        {
            var t=new DBText(); t.SetDatabaseDefaults(db);
            t.TextString=value; t.Position=position; t.Height=height; t.Layer=layer;
            return t;
        }
        private static RotatedDimension Dimension(Database db,Point3d a,Point3d b,Point3d line,string layer)
        {
            double angle=Math.Abs(a.Y-b.Y) > Math.Abs(a.X-b.X) ? Math.PI/2 : 0;
            var d=new RotatedDimension(angle,a,b,line,"",db.Dimstyle);
            d.SetDatabaseDefaults(db); d.Layer=layer; return d;
        }
        private static void ShowPreview(List<Entity> entities)
        {
            int displayed=0;
            try
            {
                foreach (Entity e in entities)
                {
                    e.ColorIndex=1;
                    Check(TransientManager.CurrentTransientManager.AddTransient(e,TransientDrawingMode.DirectShortTerm,128,PreviewViewports),
                        "无法显示完整预览。");
                    displayed++;
                }
            }
            catch
            {
                for (int i=0;i<displayed;i++) TransientManager.CurrentTransientManager.EraseTransient(entities[i],PreviewViewports);
                throw;
            }
        }
        private static void HidePreview(List<Entity> entities)
        {
            foreach (Entity e in entities)
                try { TransientManager.CurrentTransientManager.EraseTransient(e,PreviewViewports); }
                catch (Autodesk.AutoCAD.Runtime.Exception) { }
        }
        private static void Append(BlockTableRecord space,Transaction tr,Entity entity)
        {
            space.AppendEntity(entity); tr.AddNewlyCreatedDBObject(entity,true);
        }
        private static void EnsureLayer(Database db,Transaction tr,string name,short color)
        {
            LayerTable table=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
            if (table.Has(name)) return;
            table.UpgradeOpen();
            var layer=new LayerTableRecord { Name=name, Color=Color.FromColorIndex(ColorMethod.ByAci,color) };
            table.Add(layer); tr.AddNewlyCreatedDBObject(layer,true);
        }
        private static DetailParams ReadLast(Database db)
        {
            var p=new DetailParams();
            using (Transaction tr=db.TransactionManager.StartTransaction())
            {
                DBDictionary nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForRead);
                if (!nod.Contains(ParamRecord)) return p;
                Xrecord record=tr.GetObject(nod.GetAt(ParamRecord),OpenMode.ForRead) as Xrecord;
                if (record == null || record.Data == null) return p;
                TypedValue[] v=record.Data.AsArray();
                if (v.Length != 7) return p;
                try
                {
                    p.Cover=Convert.ToDouble(v[0].Value); p.LineDiameter=Convert.ToDouble(v[1].Value);
                    p.LineSpacing=Convert.ToDouble(v[2].Value); p.DotDiameter=Convert.ToDouble(v[3].Value);
                    p.DotSpacing=Convert.ToDouble(v[4].Value); p.LeftAnchor=Convert.ToDouble(v[5].Value);
                    p.RightAnchor=Convert.ToDouble(v[6].Value);
                }
                catch { return new DetailParams(); }
                return p;
            }
        }
        private static void WriteLast(Database db,Transaction tr,DetailParams p)
        {
            DBDictionary nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForWrite);
            Xrecord record;
            if (nod.Contains(ParamRecord)) record=(Xrecord)tr.GetObject(nod.GetAt(ParamRecord),OpenMode.ForWrite);
            else { record=new Xrecord(); nod.SetAt(ParamRecord,record); tr.AddNewlyCreatedDBObject(record,true); }
            record.Data=new ResultBuffer(new TypedValue((int)DxfCode.Real,p.Cover),new TypedValue((int)DxfCode.Real,p.LineDiameter),
                new TypedValue((int)DxfCode.Real,p.LineSpacing),new TypedValue((int)DxfCode.Real,p.DotDiameter),
                new TypedValue((int)DxfCode.Real,p.DotSpacing),new TypedValue((int)DxfCode.Real,p.LeftAnchor),
                new TypedValue((int)DxfCode.Real,p.RightAnchor));
        }
    }
}
