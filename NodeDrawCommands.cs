using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using CadApplication=Autodesk.AutoCAD.ApplicationServices.Application;

namespace WallDetailTool
{
    public partial class Commands
    {
        [CommandMethod("WDTBEAMNODE",CommandFlags.Session)]
        public void ShowBeamNode() { ShowNodeForm(false,false); }
        [CommandMethod("WDTSLABNODE",CommandFlags.Session)]
        public void ShowSlabNode() { ShowNodeForm(true,false); }
        [CommandMethod("WDTUPFORM",CommandFlags.Session)]
        public void ShowUpstandDrawForm() { ShowNodeForm(false,false); }
        [CommandMethod("WDTBEAMHANGDRAW",CommandFlags.Session)]
        public void ShowBeamHangingForm() { ShowNodeForm(false,true); }
        [CommandMethod("WDTUPSLABDRAW",CommandFlags.Session)]
        public void ShowSlabDrawForm() { ShowNodeForm(true,false); }
        [CommandMethod("WDTSLABHANGDRAW",CommandFlags.Session)]
        public void ShowSlabHangingForm() { ShowNodeForm(true,true); }

        // Former queue helpers are safe aliases, never replaying pending state.
        [CommandMethod("WDTUPFORMDRAW",CommandFlags.Session)]
        public void DrawUpstandFromForm() { ShowNodeForm(false,false); }
        [CommandMethod("WDTUPSLABFORMDRAW",CommandFlags.Session)]
        public void DrawSlabFromForm() { ShowNodeForm(true,false); }
        private static string Number(double value) { return value>0?value.ToString("g",CultureInfo.CurrentCulture):""; }
        private static double[] AdvancedValues(UpstandPanelValues p)
        { return new[]{p.Cover,p.LineDisplayWidth,p.DotDisplayDiameter,p.Radius,p.TextHeight}; }

        private static async void ShowNodeForm(bool slab,bool hanging)
        {
            Document doc=CadApplication.DocumentManager.MdiActiveDocument;
            if(doc==null) return;
            try
            {
                // Read each direction once into this opening. No draft or request
                // survives cancellation; records are only written by the core.
                var saved=UpstandPanelValues.ReadUserDefaults();
                string[][] text=new string[2][];
                double[][] windowAdvanced={AdvancedValues(saved),AdvancedValues(saved)};
                for(int direction=0;direction<2;direction++)
                {
                    bool down=direction==1;
                    if(slab)
                    {
                        var p=new SlabUpstandRebarParameters { Cover=saved.Cover,LineDisplayWidth=saved.LineDisplayWidth };
                        { p.LineDiameter=saved.LineDiameter; p.LineSpacing=saved.LineSpacing;
                            p.DotDiameter=saved.DotDiameter; p.DotSpacing=saved.DotSpacing; }
                        double thickness;
                        ReadSlabLast(doc.Database,p,out thickness,down);
                        text[direction]=Array.ConvertAll(new[]{p.LineDiameter,p.LineSpacing,p.DotDiameter,p.DotSpacing,
                            thickness,p.EffectiveH,p.LeftLength,p.RightLength},Number);
                        if(Math.Abs(p.LeftLength-p.RightLength)<1e-9) text[direction][7]="";
                    }
                    else
                    {
                        var p=ReadBeamWindowValues(doc.Database,down);
                        windowAdvanced[direction]=AdvancedValues(p);
                        text[direction]=Array.ConvertAll(new[]{p.LineDiameter,p.LineSpacing,p.DotDiameter,p.DotSpacing,
                            p.LeftAnchor,p.RightAnchor},Number);
                        if(p.RightAnchor>0 && Math.Abs(p.LeftAnchor-p.RightAnchor)<1e-9) text[direction][5]="";
                    }
                }
                var state=new NodeDrawState(slab,hanging,text[0],text[1]);
                NodeDrawRequest request;
                using(var dialog=new NodeDrawForm(state,windowAdvanced[0],windowAdvanced[1],(int)doc.Database.Insunits))
                {
                    if(CadApplication.ShowModalDialog(dialog)!=DialogResult.OK) return;
                    request=dialog.Request;
                }
                if(request==null) return;
                // The closure owns the complete immutable request and target
                // document. No mutable global fields or command-string queue.
                await CadApplication.DocumentManager.ExecuteInCommandContextAsync(_=>
                {
                    if(RectangleInputGeometry.TargetMatches(doc,CadApplication.DocumentManager.MdiActiveDocument,request.UnitCode,(int)doc.Database.Insunits))
                        ExecuteNodeRequest(request);
                    else doc.Editor.WriteMessage("\n目标图纸已切换，本次绘制请求已放弃，请重新打开窗口。");
                    return Task.CompletedTask;
                },null);
            }
            catch(System.Exception ex)
            {
                if(ReferenceEquals(doc,CadApplication.DocumentManager.MdiActiveDocument))
                    doc.Editor.WriteMessage("\n节点绘制未启动："+ex.Message);
            }
        }

        // Window-only compatibility: valid historic fields override shared values;
        // missing/invalid fields fall back individually, without writing records.
        private static UpstandPanelValues ReadBeamWindowValues(Autodesk.AutoCAD.DatabaseServices.Database db,bool hanging)
        {
            var p=UpstandPanelValues.ReadUserDefaults();
            var v=new[]{p.Cover,p.LineDiameter,p.LineSpacing,p.DotDiameter,p.DotSpacing,p.LeftAnchor,p.RightAnchor,p.Radius,p.TextHeight,p.LineDisplayWidth,p.DotDisplayDiameter};
            using(var tr=db.TransactionManager.StartTransaction()) {
                var nod=(Autodesk.AutoCAD.DatabaseServices.DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                string name=hanging?"WDT_BEAM_HANGING_LAST":ParamRecord;
                if(nod.Contains(name)) {
                    var record=tr.GetObject(nod.GetAt(name),Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Xrecord;
                    if(record!=null)using(var buffer=record.Data)if(buffer!=null) {
                        var a=buffer.AsArray();
                        if(a.Length==7||a.Length==8||a.Length==9||a.Length==11) {
                            var recordValues=new double[a.Length];
                            for(int i=0;i<a.Length;i++) { recordValues[i]=double.NaN;try { recordValues[i]=Convert.ToDouble(a[i].Value); }catch(FormatException) { }catch(InvalidCastException) { }catch(OverflowException) { } }
                            v=UnifiedNodeUi.MergeDefaults(v,recordValues);
                        }
                    }
                }
            }
            return new UpstandPanelValues { Cover=v[0],LineDiameter=v[1],LineSpacing=v[2],DotDiameter=v[3],DotSpacing=v[4],LeftAnchor=v[5],RightAnchor=v[6],Radius=v[7],TextHeight=v[8],LineDisplayWidth=v[9],DotDisplayDiameter=v[10] };
        }

        private static void ExecuteNodeRequest(NodeDrawRequest r)
        {
            if(r.IsSlab)
                DrawSlabCore(new SlabDrawValues { LineDiameter=r.Value(0),LineSpacing=r.Value(1),
                    DotDiameter=r.Value(2),DotSpacing=r.Value(3),LocalThickness=r.Value(4),H=r.Value(5),
                    LeftLength=r.Value(6),RightLength=r.Value(7),Cover=r.Advanced(0),LineDisplayWidth=r.Advanced(1),
                    DotDisplayDiameter=r.Advanced(2),Radius=r.Advanced(3),TextHeight=r.Advanced(4) },r.Hanging,r.TwoPointRectangle,r.UnitCode);
            else
                DrawUpstandCore(new UpstandPanelValues { LineDiameter=r.Value(0),LineSpacing=r.Value(1),
                    DotDiameter=r.Value(2),DotSpacing=r.Value(3),LeftAnchor=r.Value(4),RightAnchor=r.Value(5),
                    Cover=r.Advanced(0),LineDisplayWidth=r.Advanced(1),DotDisplayDiameter=r.Advanced(2),
                    Radius=r.Advanced(3),TextHeight=r.Advanced(4) },r.Hanging,r.TwoPointRectangle,r.UnitCode);
        }
    }
}
