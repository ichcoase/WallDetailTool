using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WallDetailTool;
internal static class UsabilityTests
{
 static int checks;static void Check(bool v,string m) { checks++;if(!v)throw new Exception(m); }
 static T Find<T>(Form f,string n)where T:Control { var found=f.Controls.Find(n,true);if(found.Length!=1)throw new Exception("Missing control: "+n+" in "+f.Name);return (T)found[0]; }
 static readonly double[] A={25,12,12,20,62.5};static readonly string[] L={"L","D","T","M"};
 static UnifiedNodeUi Ui(Form f) { return (UnifiedNodeUi)f.GetType().GetField("unified",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(f); }
 static void FocusVisible(Form f,int parameter)
 {
  f.Show();Find<TextBox>(f,"Parameter"+parameter).Focus();Application.DoEvents();
  var sketch=f.Controls.Find("Sketch",true).Single();
  using(var bmp=new Bitmap(sketch.Width,sketch.Height)) {
   sketch.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bool orange=false;
   for(int x=0;x<bmp.Width&&!orange;x++)for(int y=0;y<bmp.Height-20;y++)if(bmp.GetPixel(x,y).ToArgb()==Color.DarkOrange.ToArgb()) { orange=true;break; }
   Check(orange,"selected dimension survives native sketch painting");
  }
 }
 static void Layout(Form f,string name)
 {
  f.Show();Application.DoEvents();
  bool helped=false;using(var timer=new Timer { Interval=100 }) {
   timer.Tick+=(s,e)=> { foreach(var w in Application.OpenForms.Cast<Form>().Where(w=>w!=f&&w.Text.Contains("本地帮助")).ToArray()) {
    Check(w.Controls.OfType<PictureBox>().Any(p=>p.Image!=null),"offline help includes current native figure");
    Check(w.Controls.OfType<TextBox>().Any(t=>t.Text.Contains("试用")),"offline help includes example and limits");helped=true;w.Close();timer.Stop();
   } };
   timer.Start();Find<Button>(f,"Help").PerformClick();timer.Stop();Check(helped,"help opens from current node without network");
  }
  foreach(float scale in new[]{1f,1.25f,1.5f})
  {
   f.Font=new Font(f.Font.FontFamily,9*scale);
   f.ClientSize=new Size((int)(800*scale),(int)(560*scale));f.PerformLayout();Application.DoEvents();
   foreach(string n in new[]{"Generate","Cancel","RestorePresets","InputMethod"})
   { var c=f.Controls.Find(n,true).Single();Check(f.ClientRectangle.Contains(f.RectangleToClient(c.RectangleToScreen(c.ClientRectangle))),name+" reachable "+n); }
  }
  f.ClientSize=new Size(930,680);Application.DoEvents();
  using(var bmp=new Bitmap(f.Width,f.Height)) { f.DrawToBitmap(bmp,new Rectangle(0,0,f.Width,f.Height));bmp.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name+".png")); }
  foreach(var box in f.Controls.Find("Parameter0",true).Cast<TextBox>())Check(box.Visible&&box.Width>30&&box.Height>10,"numeric editor visible");
  f.Hide();
 }
 [STAThread]static void Main()
 {
  try {
   foreach(bool slab in new[]{false,true})
   {
    var seed=Enumerable.Repeat("",slab?8:6).ToArray();
    using(var f=new NodeDrawForm(new NodeDrawState(slab,false,seed,seed),A,A,4))
    {
     var before=f.CreateRequest();Check(before.Value(0)==8&&before.Value(slab?6:4)==300,"first defaults");
     Find<TextBox>(f,"Parameter"+(slab?6:4)).Text="777";
     Find<ComboBox>(f,"Direction").SelectedIndex=1;Find<TextBox>(f,"Parameter"+(slab?6:4)).Text="888";
     f.Show();Application.DoEvents();Find<Button>(f,"RestorePresets").PerformClick();Check(f.CreateRequest().Value(slab?6:4)==300,"restore active direction");
     Find<ComboBox>(f,"Direction").SelectedIndex=0;Check(f.CreateRequest().Value(slab?6:4)==777,"other draft unchanged");
     Check(f.CreateRequest().Advanced(0)==25,"advanced unchanged");
     FocusVisible(f,slab?5:4);
     var sketch=Find<NodeSketch>(f,"Sketch");var segment=Ui(f).FocusSegments(slab?5:4)[0];
     Check(Math.Abs(segment[0].Y-(slab?sketch.Diagram.OtherFace:sketch.Diagram.Face))<.1,"dimension starts at actual support face in upward sketch");
     Find<ComboBox>(f,"Direction").SelectedIndex=1;segment=Ui(f).FocusSegments(slab?5:5)[0];
     Check(Math.Abs(segment[0].Y-(slab?sketch.Diagram.OtherFace:sketch.Diagram.Face))<.1,"downward sketch follows support face");
     Find<ComboBox>(f,"Direction").SelectedIndex=0;
     Find<TextBox>(f,"Parameter0").Text="bad";Find<Button>(f,"Generate").PerformClick();Check(f.Request==null&&Find<TextBox>(f,"Parameter0").Text=="bad","inline error keeps draft without accepting");Find<TextBox>(f,"Parameter0").Text="8";
     Find<TextBox>(f,"Parameter0").Focus();Application.DoEvents();Check(Find<Label>(f,"ParameterHelp").Text.Contains("实际直径"),"focus explanation");
     Layout(f,slab?"unified-slab":"unified-beam");f.Close();Check(f.Request==null,"cancel close no request");
    }
   }
   var saved=new BeamSideDrawRequest(BeamSideDirection.Left,false,new BeamSideRebarParameters(510,120,cover:35),1,4,false,L);
   using(var f=new BeamSideDrawForm(new BeamSideDrawState(saved,A,L,new[]{10.0,125,8,180}),"mm",4,1,false))
   {
    Check(f.CreateRequest().Parameters.Length==510&&f.CreateRequest().Parameters.Cover==35,"saved values before shared");
    FocusVisible(f,4);Check(Ui(f).FocusSegments(4)[0].Length==3&&Ui(f).FocusSegments(5)[0].Length==2,"side L includes end leg, V only straight leg");
    Find<TextBox>(f,"Parameter4").Text="777";Find<ComboBox>(f,"Mode").SelectedIndex=1;
    Find<Button>(f,"RestorePresets").PerformClick();Check(f.CreateRequest().Parameters.Length==400,"restore full mode");
    Find<ComboBox>(f,"Mode").SelectedIndex=0;Check(f.CreateRequest().Parameters.Length==777,"side other mode draft retained");
    Layout(f,"unified-side");
   }
   using(var f=new CantileverDrawForm(new CantileverDrawState(null,A,L,new[]{8.0,150,6,200}),"mm",4,1,false))
   {
    f.Show();Application.DoEvents();Find<TextBox>(f,"Parameter1").Text="bad A";
    Find<ComboBox>(f,"Mode").SelectedIndex=3;Check(f.CreateRequest().Mode==CantileverSupportMode.InnerWithAdditional,"hidden invalid draft not blocking");
    Check(!Find<TextBox>(f,"Parameter1").Visible&&Find<TextBox>(f,"Parameter6").Visible,"unrelated parameters hidden");
    Find<TextBox>(f,"Parameter6").Text="999";Find<Button>(f,"RestorePresets").PerformClick();Check(f.CreateRequest().Value(6)==450,"active mode reset");
    Find<ComboBox>(f,"Mode").SelectedIndex=0;Check(Find<TextBox>(f,"Parameter1").Text=="bad A","mode raw draft kept");
    Find<Button>(f,"RestorePresets").PerformClick();Check(f.CreateRequest().Value(1)==180,"restore fixes only current");
    FocusVisible(f,2);
    var right=Ui(f).FocusSegments(2)[0];Find<ComboBox>(f,"Direction").SelectedIndex=1;var left=Ui(f).FocusSegments(2)[0];
    Check(right[0].Y==left[0].Y&&right[1].Y==left[1].Y&&Math.Abs(right[0].X+left[0].X-f.Controls.Find("Sketch",true)[0].Width)<.1,"cantilever V mirrors X without reversing vertical direction");
    for(int m=0;m<4;m++) { Find<ComboBox>(f,"Mode").SelectedIndex=m;Check(f.CreateRequest().Value(0)==300,"four modes have required first defaults"); }
    Layout(f,"unified-cantilever");
   }
   Check(UnifiedNodeUi.Explain("side",5,"").Contains("不含R"),"beam V semantics");
   Check(UnifiedNodeUi.Explain("cantilever",6,"").Contains("不含梁宽"),"Lin semantics");
   Check(UnifiedNodeUi.Explain("slab",6,"").Contains("不含圆弧"),"slab L semantics");
   var shared=new[]{35.0,10,125,8,180,300,300,30,50,16,14};
   var record=new[]{25.0,12,150,6,200,450,500};var merged=UnifiedNodeUi.MergeDefaults(shared,record);
   Check(merged[5]==450&&merged[7]==30&&merged[9]==16,"old missing advanced fields inherit shared values");
   merged[1]=999;Check(shared[1]==10&&record[1]==12,"merging never mutates shared or record");
   Console.WriteLine("USABILITY_PASS checks="+checks);
  }catch(Exception e) {Console.WriteLine(e);Environment.ExitCode=1;}
 }
}
