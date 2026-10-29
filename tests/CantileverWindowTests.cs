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
    static void Fill(Form f) { for(int i=0;i<15;i++)if(i!=4)Find<TextBox>(f,"Parameter"+i).Text=Text[i]; }
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
            f.CreateRequest();Check(Find<TextBox>(f,"Parameter0").Text=="300");
            Check(f.Controls.Find("Parameter4",true).Length==0);Check(Find<Label>(f,"AutomaticF").Text.Contains("10 mm"));
            for(int i=0;i<15;i++) if(i!=4) Check(!String.IsNullOrWhiteSpace(Find<TextBox>(f,"Parameter"+i).Text));
            for(int i=0;i<4;i++) { Find<ComboBox>(f,"Mode").SelectedIndex=i;f.CreateRequest(); }
            Find<ComboBox>(f,"Mode").SelectedIndex=0;Fill(f);
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
            CantileverAnnotationGeometry.ApplyMeasuredLayout(annotation,annotation.Labels.Select(l=>new CantileverTextSize(200*scale,62.5*scale)).ToArray());
            Check(annotation.Labels.Count==(mode==CantileverSupportMode.InnerWithAdditional?4:3));
            foreach(var label in annotation.Labels)
            {
                var leaders=label.Layout.Leaders;var last=leaders.Last();
                Near(last.A.Y,last.B.Y);Check(label.Layout.TextAnchor.Y>last.A.Y);
                Check(leaders.Count==2);Check(Math.Abs(leaders[0].A.Y-leaders[0].B.Y)>scale);
                Near(leaders[0].A.X,leaders[0].B.X);Near(leaders[0].B.X,last.A.X);Near(leaders[0].B.Y,last.A.Y);
                if(label.Name=="面筋"||label.Name=="附加筋")Check(label.Layout.Rightward==(direction==CantileverDirection.Right));
                if(label.Name=="内跨伸出筋")Check(label.Layout.Rightward==(direction==CantileverDirection.Left));
            }
            var topLabel=annotation.Labels[0].Layout;var topPath=steel.Bars[0].Vertices;
            Near(topLabel.Leaders[0].A.Y,topPath[topPath.Count-3].Point.Y*scale);
            var dotLabel=annotation.Labels.Single(l=>l.Name=="上下两排点筋").Layout;
            Check(dots.Upper.Any(q=>Math.Abs(q.X*scale-dotLabel.Leaders[0].A.X)<1e-7&&Math.Abs(q.Y*scale-dotLabel.Leaders[0].A.Y)<1e-7));
            var pairedLower=dots.Lower.Single(q=>Math.Abs(q.X*scale-dotLabel.Leaders[0].A.X)<1e-7);
            Check(pairedLower.Y*scale<dotLabel.Leaders[0].A.Y&&pairedLower.Y*scale>dotLabel.Leaders[0].B.Y);
            Check(annotation.Labels.Count(l=>l.Name=="上下两排点筋")==1);Check(dotLabel.Text.Contains("6@200"));
            Near((steel.Bars[0].Vertices.Last().Point.Y-steel.Bars[1].Vertices[0].Point.Y)*scale,10*scale);
            foreach(var first in annotation.Labels) foreach(var second in annotation.Labels)
            {
                if(ReferenceEquals(first,second))continue;
                foreach(var a in first.Layout.Leaders)foreach(var b in second.Layout.Leaders)Check(!Crosses(a,b));
            }
            Near(Math.Abs(annotation.Dimensions.WidthA.X-annotation.Dimensions.WidthB.X),1500*scale);
            if(mode==CantileverSupportMode.InnerWithAdditional)
            { Near(annotation.Lin.Lin,450);Near(Math.Abs(annotation.Lin.BeamFace.X-annotation.Lin.Cutoff.X),450);Check(annotation.Labels.Any(l=>l.Layout.Text.Contains("16@100"))); }
            else Check(annotation.Lin==null);
            var encoded=CantileverDrawRecord.EncodeSnapshot(copy,frame,p,37);
            CantileverFrame restored;CantileverPathInputs restoredInputs;double elevation;
            var restoredRequest=CantileverDrawRecord.DecodeSnapshot(encoded,Layers,out restored,out restoredInputs,out elevation);
            Near(restored.SupportX,1500);Near(restored.TopY,-700);Near(restoredInputs.Length,1500);Near(elevation,37);Near(restoredRequest.Scale,scale);
            Check(encoded[0]==2&&encoded[7]==0);
            var legacy=(double[])encoded.Clone();legacy[0]=1;legacy[7]=987;
            var legacyCopy=(double[])legacy.Clone();var restoredLegacy=CantileverDrawRecord.DecodeSnapshot(legacy,Layers,out restored,out restoredInputs,out elevation);
            Near(restoredLegacy.Value(4),0);Near(restoredLegacy.Value(0),copy.Value(0));
            Near(CantileverRebarGeometry.Calculate(restored,restoredLegacy.Specifications,restoredInputs).EffectiveF,steel.EffectiveF);
            Check(legacy.SequenceEqual(legacyCopy)); // Reading never rewrites an old block snapshot.
            var narrow=copy.Inputs(150,200);var narrowSteel=CantileverRebarGeometry.Calculate(frame,copy.Specifications,narrow);
            var two=CantileverDots.Calculate(frame,narrowSteel,narrow,12,200);
            var narrowLabels=CantileverAnnotationGeometry.Calculate(frame,copy,narrow,narrowSteel,two,37);
            Check(two.Upper.Count+two.Lower.Count==2);
            var one=narrowLabels.Labels.Single(l=>l.Name=="上下两排点筋").Layout;
            Check(one.Text=="点筋 2φ6");Check(one.Leaders.Count==2);Check(!one.Text.Contains("@"));
            var fourInputs=copy.Inputs(300,200);var fourSteel=CantileverRebarGeometry.Calculate(frame,copy.Specifications,fourInputs);
            var fourDots=CantileverDots.Calculate(frame,fourSteel,fourInputs,12,200);
            Check(fourDots.Upper.Count==2&&fourDots.Lower.Count==2);
            var fourLabel=CantileverAnnotationGeometry.Calculate(frame,copy,fourInputs,fourSteel,fourDots,37).Labels.Single(l=>l.Name=="上下两排点筋");
            Check(fourLabel.Layout.Text=="点筋 φ6@200"); // Two per row is FOUR actual dots.
        }
        foreach(int i in new[]{0,1,2,3,7,23,24,25})
        { var values=CantileverDrawRecord.Encode(captured);values[i]=i==24?1.5:double.NaN;Reject(()=>CantileverDrawRecord.Decode(values,Layers)); }
        Reject(()=>CantileverDrawRecord.Decode(new double[25],Layers));
        var layers=(string[])Layers.Clone();var advanced=(double[])Advanced.Clone();
        var state=new CantileverDrawState(null,advanced,layers,new[]{12.0,150,6,200});advanced[0]=999;layers[0]="modified";
        var r=state.Create(Text,CantileverDirection.Right,CantileverSupportMode.NoInnerUp,1,4,false);Near(r.Advanced(0),25);Check(r.Layer(0)==Layers[0]);
        Check(CantileverDrawRecord.LastKey!="WDT_BEAM_SIDE_LAST"&&CantileverDrawRecord.LastKey!="WDT_UPSTAND_LAST");
        var changedText=(string[])Text.Clone();changedText[0]="420";changedText[7]="18";
        var saved=state.Create(changedText,CantileverDirection.Left,CantileverSupportMode.InnerWithAdditional,1,4,false);
        var reopened=new CantileverDrawState(saved,new[]{99.0,8,8,30,40},Layers,new[]{8.0,200,8,150});
        Near(double.Parse(reopened.Initial[0]),420);Near(double.Parse(reopened.Initial[7]),18);
        Near(reopened.Create(reopened.Initial,CantileverDirection.Left,CantileverSupportMode.InnerWithAdditional,1,4,false).Advanced(0),25);
        foreach(var dir in new[]{CantileverDirection.Left,CantileverDirection.Right})
        foreach(double e in new[]{30.0,600,10000})foreach(double lin in new[]{30.0,450,12000})
        {
            var text=(string[])Text.Clone();text[3]=e.ToString();text[6]=lin.ToString();
            var request=State().Create(text,dir,CantileverSupportMode.InnerWithAdditional,1,4,false);
            var frame=new CantileverFrame(dir,0,0,300);var p=request.Inputs(1500,200);
            var steel=CantileverRebarGeometry.Calculate(frame,request.Specifications,p);
            var dots=CantileverDots.Calculate(frame,steel,p,12,200);
            var labels=CantileverAnnotationGeometry.Calculate(frame,request,p,steel,dots,0);
            CantileverAnnotationGeometry.ApplyMeasuredLayout(labels,labels.Labels.Select(l=>new CantileverTextSize(200,62.5)).ToArray());
            foreach(var first in labels.Labels)foreach(var second in labels.Labels)
            {
                if(ReferenceEquals(first,second))continue;
                foreach(var a in first.Layout.Leaders)foreach(var b in second.Layout.Leaders)Check(!Crosses(a,b));
            }
        }
    }
    static bool Crosses(AnnotationSegment a,AnnotationSegment b)
    {
        Func<AnnotationPoint,AnnotationPoint,AnnotationPoint,double> cross=(p,q,r)=>(q.X-p.X)*(r.Y-p.Y)-(q.Y-p.Y)*(r.X-p.X);
        return cross(a.A,a.B,b.A)*cross(a.A,a.B,b.B)<-1e-12&&cross(b.A,b.B,a.A)*cross(b.A,b.B,a.B)<-1e-12;
    }
}
