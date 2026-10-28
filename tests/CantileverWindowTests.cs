using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WallDetailTool;

internal static class CantileverWindowTests
{
    static int checks;
    static readonly string[] Layers={"WDT-REBAR-LINE","WDT-REBAR-DOT","S-TEXT","s-floor_de_dim"};
    static readonly double[] Advanced={25,12,12,20,62.5};
    static readonly string[] Text={"300","180","250","600","100","200","450","12","150","8","200","16","100","6","200"};
    static void Check(bool v) { checks++;if(!v)throw new Exception("Check "+checks); }
    static void Near(double a,double b) { Check(Math.Abs(a-b)<1e-7); }
    static void Reject(Action a) { bool fail=false;try{a();}catch(ArgumentException){fail=true;}Check(fail); }
    static T Find<T>(Form f,string name) where T:Control { return (T)f.Controls.Find(name,true).Single(); }
    static CantileverDrawState State(CantileverDrawRequest saved=null) { return new CantileverDrawState(saved,Advanced,Layers,new[]{12.0,150,6,200}); }
    static void Fill(Form f) { for(int i=0;i<15;i++)Find<TextBox>(f,"Parameter"+i).Text=Text[i]; }
    [STAThread] static void Main()
    {
        try { Run();Console.WriteLine("CANTILEVER_WINDOW_RECORD_ANNOTATION_PASS checks="+checks); }
        catch(Exception ex){Console.WriteLine(ex);Environment.ExitCode=1;}
    }
    static void Run()
    {
        Application.EnableVisualStyles();CantileverDrawRequest captured=null;
        using(var f=new CantileverDrawForm(State(),"毫米",4,1,false))
        {
            Reject(()=>f.CreateRequest());Check(Find<TextBox>(f,"Parameter0").Text=="");
            Find<ComboBox>(f,"Mode").SelectedIndex=0;Reject(()=>f.CreateRequest());Fill(f);
            captured=f.CreateRequest();
            Find<TextBox>(f,"Parameter1").Text="invalid draft";
            Find<ComboBox>(f,"Mode").SelectedIndex=3;Check(!Find<TextBox>(f,"Parameter1").Enabled);f.CreateRequest();
            Find<ComboBox>(f,"Direction").SelectedIndex=1;Find<ComboBox>(f,"Mode").SelectedIndex=0;
            Check(Find<TextBox>(f,"Parameter1").Text=="invalid draft");Reject(()=>f.CreateRequest());
            Fill(f);Near(captured.Value(1),180);
            f.Show();Application.DoEvents();f.PerformLayout();
            using(var bitmap=new Bitmap(f.Width,f.Height))
            {
                f.DrawToBitmap(bitmap,new Rectangle(0,0,f.Width,f.Height));int coloured=0;
                for(int y=0;y<bitmap.Height;y+=3) for(int x=0;x<bitmap.Width;x+=3)
                { var pixel=bitmap.GetPixel(x,y);if(pixel.B>pixel.R*1.2&&pixel.B>80)coloured++; }
                Check(coloured>10);bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cantilever-window.png"));
            }
            f.Close();Check(f.Request==null);
        }
        using(var f=new CantileverDrawForm(State(captured),"毫米",4,1,false))
        { Near(f.CreateRequest().Value(1),180);f.Show();Application.DoEvents();Find<Button>(f,"Cancel").PerformClick();Check(f.Request==null&&f.DialogResult==DialogResult.Cancel); }
        using(var f=new CantileverDrawForm(State(),"米",6,.001,true))
        {
            Fill(f);Find<ComboBox>(f,"Mode").SelectedIndex=0;Reject(()=>f.CreateRequest());
            Find<ComboBox>(f,"Units").SelectedIndex=0;Near(f.CreateRequest().Scale,.001);
            Find<ComboBox>(f,"Units").SelectedIndex=1;Check(f.CreateRequest().OverrideMillimetres);Near(f.CreateRequest().Scale,1);
        }
        using(var f=new CantileverDrawForm(State(),"未知",9,0,true))
        { Fill(f);Find<ComboBox>(f,"Mode").SelectedIndex=0;Find<ComboBox>(f,"Units").SelectedIndex=0;Check(f.CreateRequest().OverrideMillimetres); }
        var target=new object();Check(CantileverDrawRequest.TargetMatches(target,target,4,4));
        Check(!CantileverDrawRequest.TargetMatches(target,new object(),4,4));Check(!CantileverDrawRequest.TargetMatches(target,target,4,6));
        foreach(var direction in new[]{CantileverDirection.Left,CantileverDirection.Right})
        foreach(var mode in new[]{CantileverSupportMode.NoInnerUp,CantileverSupportMode.NoInnerDown,CantileverSupportMode.InnerContinuous,CantileverSupportMode.InnerWithAdditional})
        foreach(double scale in new[]{1.0,.001,1/25.4})
        {
            var request=State().Create(Text,direction,mode,scale,4,false);var values=CantileverDrawRecord.Encode(request);
            var copy=CantileverDrawRecord.Decode(values,Layers);values[3]=999;Near(copy.Value(0),300);
            Check(copy.Mode==mode&&copy.Direction==direction);Near(copy.Value(9),8);Near(copy.Value(7),12);
            if(mode==CantileverSupportMode.InnerWithAdditional) { Near(copy.Value(11),16);Near(copy.Value(12),100); }
            var frame=new CantileverFrame(direction,1500,-700,300);var p=copy.Inputs(1500,200);
            var steel=CantileverRebarGeometry.Calculate(frame,copy.Specifications,p);
            var dots=CantileverDots.Calculate(frame,steel,p,12,200);
            var annotation=CantileverAnnotationGeometry.Calculate(frame,copy,p,steel,dots,37);
            Check(annotation.Labels.Count==(mode==CantileverSupportMode.InnerWithAdditional?4:3));
            foreach(var label in annotation.Labels)
            {
                Near(label.Layout.Leaders[0].A.Y,label.Layout.Leaders[0].B.Y);
                Check(label.Layout.TextAnchor.Y>label.Layout.Leaders[0].A.Y);
                if(label.Name=="面筋"||label.Name=="内跨伸出筋"||label.Name=="底筋")Check(label.Layout.Rightward==(direction==CantileverDirection.Right));
            }
            Near(Math.Abs(annotation.Dimensions.WidthA.X-annotation.Dimensions.WidthB.X),1500*scale);
            if(mode==CantileverSupportMode.InnerWithAdditional)
            { Near(annotation.Lin.Lin,450);Near(Math.Abs(annotation.Lin.BeamFace.X-annotation.Lin.Cutoff.X),450);Check(annotation.Labels.Any(l=>l.Layout.Text.Contains("16@100"))); }
            else Check(annotation.Lin==null);
            var encoded=CantileverDrawRecord.EncodeSnapshot(copy,frame,p,37);
            CantileverFrame restored;CantileverPathInputs restoredInputs;double elevation;
            var restoredRequest=CantileverDrawRecord.DecodeSnapshot(encoded,Layers,out restored,out restoredInputs,out elevation);
            Near(restored.SupportX,1500);Near(restored.TopY,-700);Near(restoredInputs.Length,1500);Near(elevation,37);Near(restoredRequest.Scale,scale);
        }
        foreach(int i in new[]{0,1,2,3,7,23,24,25})
        { var values=CantileverDrawRecord.Encode(captured);values[i]=i==24?1.5:double.NaN;Reject(()=>CantileverDrawRecord.Decode(values,Layers)); }
        Reject(()=>CantileverDrawRecord.Decode(new double[25],Layers));
        var layers=(string[])Layers.Clone();var advanced=(double[])Advanced.Clone();
        var state=new CantileverDrawState(null,advanced,layers,new[]{12.0,150,6,200});advanced[0]=999;layers[0]="modified";
        var r=state.Create(Text,CantileverDirection.Right,CantileverSupportMode.NoInnerUp,1,4,false);Near(r.Advanced(0),25);Check(r.Layer(0)==Layers[0]);
        Check(CantileverDrawRecord.LastKey!="WDT_BEAM_SIDE_LAST"&&CantileverDrawRecord.LastKey!="WDT_UPSTAND_LAST");
    }
}
