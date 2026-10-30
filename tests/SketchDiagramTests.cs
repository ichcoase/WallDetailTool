using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WallDetailTool;

internal static class SketchDiagramTests
{
    static int checks;
    static void Check(bool value,string message) { checks++;if(!value)throw new Exception(message); }
    static void Equal(PointF a,PointF b,string message) { Check(Math.Abs(a.X-b.X)<.01&&Math.Abs(a.Y-b.Y)<.01,message); }
    static bool Orange(Color c) { return c.R>240&&c.G>100&&c.G<180&&c.B<20; }
    static readonly double[] Advanced={25,12,12,20,62.5};
    static readonly string[] Layers={"L","D","T","M"};
    static T Find<T>(Control f,string name) where T:Control { return (T)f.Controls.Find(name,true).Single(); }
    static void NodeGeometry()
    {
        foreach(int width in new[]{240,350,480})foreach(bool slab in new[]{false,true})foreach(bool hanging in new[]{false,true})
        using(var sketch=new NodeSketch { Size=new Size(width,240),IsSlab=slab,Hanging=hanging })
        {
            var d=sketch.Diagram;
            Check(d.Support.Left<d.Steel.Min(p=>p.X)&&d.Support.Right>d.Steel.Max(p=>p.X),"support contains both anchorage ends");
            Check(d.AnchorY>d.Support.Top&&d.AnchorY<d.Support.Bottom,"anchorage stays inside illustrative support");
            Check(hanging?d.NodeEnd>d.Face:d.NodeEnd<d.Face,"node on correct support side");
            foreach(var p in d.Steel)Check(p.X>0&&p.X<width&&p.Y>0&&p.Y<210,"steel remains in drawing area");
            using(var image=new Bitmap(width,240))
            {
                sketch.DrawToBitmap(image,new Rectangle(0,0,width,240));
                Check(image.GetPixel((int)d.Center,(int)(d.NodeEnd+(hanging?-18:18))).ToArgb()==Color.FromArgb(220,225,230).ToArgb(),"protrusion has same concrete fill");
                Check(image.GetPixel((int)d.Center,(int)d.Face).ToArgb()==Color.FromArgb(220,225,230).ToArgb(),"no artificial face across concrete joint");
            }
            if(slab)
            {
                var h=d.ParameterSegments(4)[0];var H=d.ParameterSegments(5)[0];
                Check(h[0].Y==d.Face&&h[1].Y==d.OtherFace,"h spans slab faces");
                Check(H[0].Y==d.OtherFace&&H[1].Y==d.AnchorY,"H starts at slab bottom/top, ends at steel level");
                Equal(d.ParameterSegments(6)[0][0],d.Steel[1],"left L starts at horizontal bend end");
                Equal(d.ParameterSegments(6)[0][1],d.Steel[0],"left L reaches actual end");
                Equal(d.ParameterSegments(7)[0][0],d.Steel[4],"right L starts at bend");
                Equal(d.ParameterSegments(7)[0][1],d.Steel[5],"right L reaches end");
            }
            else foreach(int i in new[]{4,5})
            {
                var path=d.ParameterSegments(i)[0];var end=d.Steel[i==4?0:3];
                Check(path[0].Y==d.Face,"anchor starts at connection face");Equal(path[1],end,"anchor ends on own steel end");
            }
            var opposite=new NodeSketchDiagram(width,240,slab,!hanging);
            for(int i=0;i<d.Steel.Length;i++)Check(Math.Abs(d.Steel[i].Y+opposite.Steel[i].Y-234)<.01,"up/down diagram mirrors about common plot center");
        }
        Check(new NodeSketchDiagram(350,240,false,false).Support.Height>new NodeSketchDiagram(350,240,true,false).Support.Height,"beam visually deeper than slab");
    }
    static void CantileverGeometry()
    {
        foreach(int width in new[]{240,350,480})foreach(bool left in new[]{false,true})foreach(int mode in new[]{1,2,3,4})
        {
            var d=new CantileverSketchDiagram(width,240,left,mode);
            var mirror=new CantileverSketchDiagram(width,240,!left,mode);
            for(int index=0;index<15;index++)
            {
                var paths=d.ParameterSegments(index);bool relevant=index==0||index==5||(index==1||index==2)&&mode<3||index==3&&mode>=3||index==6&&mode==4;
                Check((paths.Length!=0)==relevant,"only active dimensions have highlight");
                if(paths.Length==0)continue;
                var segment=paths[0];var other=mirror.ParameterSegments(index)[0];
                Check(segment[0].X==segment[1].X||segment[0].Y==segment[1].Y,"projection is axis aligned");
                for(int j=0;j<2;j++)Check(Math.Abs(segment[j].X+other[j].X-width)<.01&&Math.Abs(segment[j].Y-other[j].Y)<.01,"same targets mirror X only");
            }
            var b=d.ParameterSegments(0)[0];Check(Math.Abs(b[0].X-b[1].X)==d.Beam.Width&&b[0].Y==d.Beam.Top,"b spans actual beam faces");
            var B=d.ParameterSegments(5)[0];Equal(B[1],d.Bottom[0],"B ends at bottom steel support end");
            Check(B[0].Y==d.Bottom[0].Y&&B[0].X==d.SupportFace.X,"B starts at cantilever face on bottom path");
            if(mode<3)
            {
                var A=d.ParameterSegments(1)[0];Equal(A[0],d.SupportFace,"A starts at cantilever face");Equal(A[1],d.UpperSupport,"A reaches upright steel");
                var V=d.ParameterSegments(2)[0];Equal(V[0],d.UpperSupport,"V starts at horizontal steel level");Equal(V[1],d.SupportEnd,"V reaches support end");
                Check(mode==1?V[1].Y<V[0].Y:V[1].Y>V[0].Y,"support up/down direction");
            }
            else { var E=d.ParameterSegments(3)[0];Equal(E[0],d.InnerFace,"E starts at inner beam face");Equal(E[1],d.InnerEnd,"E ends at actual extending bar end"); }
            if(mode==4)
            {
                var Lin=d.ParameterSegments(6)[0];Equal(Lin[0],d.InnerFace,"Lin excludes beam width");Equal(Lin[1],d.AdditionalEnd,"Lin ends at symbol origin, excludes symbol tail");
                Check(d.Symbol[1].Y>d.Symbol[0].Y,"symbol down in both mirror directions");
            }
        }
    }
    static void FocusIntegration()
    {
        using(var form=new CantileverDrawForm(new CantileverDrawState(null,Advanced,Layers,new[]{8.0,150,6,200}),"mm",4,1,false))
        {
            form.Show();Application.DoEvents();Find<ComboBox>(form,"Mode").SelectedIndex=3;
            Find<TextBox>(form,"Parameter5").Focus();Application.DoEvents();var sketch=Find<CantileverSketch>(form,"Sketch");
            using(var image=new Bitmap(sketch.Width,sketch.Height))
            {
                sketch.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));var B=sketch.ParameterSegments(5)[0];
                Check(Orange(image.GetPixel((int)((B[0].X+B[1].X)/2),(int)B[0].Y)),"form B highlight touches bottom path");
            }
            Find<ComboBox>(form,"Mode").SelectedIndex=0;Find<TextBox>(form,"Parameter2").Focus();Application.DoEvents();
            Find<ComboBox>(form,"Mode").Focus();Find<ComboBox>(form,"Mode").SelectedIndex=2;Application.DoEvents();
            using(var image=new Bitmap(sketch.Width,sketch.Height))
            {
                sketch.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));bool orange=false;
                for(int x=0;x<image.Width&&!orange;x++)for(int y=0;y<image.Height;y++)if(Orange(image.GetPixel(x,y))) { orange=true;break; }
                Check(!orange,"hidden V does not leave stale highlight or caption");
            }
            form.Close();Check(form.Request==null,"sketch inspection creates no generation request");
        }
    }
    static void Panel(Graphics g,Control sketch,int parameter,string title,int x,int y)
    {
        using(sketch)using(var image=new Bitmap(350,240))
        {
            sketch.Size=image.Size;
            sketch.Paint+=(s,e)=> { foreach(var path in ((IParameterSketch)sketch).ParameterSegments(parameter))using(var pen=new Pen(Color.DarkOrange,3))e.Graphics.DrawLines(pen,path); };
            sketch.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
            g.DrawString(title,SystemFonts.MessageBoxFont,Brushes.Black,x+8,y+4);g.DrawImageUnscaled(image,x,y+26);
        }
    }
    static void Pictures()
    {
        string dir=AppDomain.CurrentDomain.BaseDirectory;
        using(var image=new Bitmap(720,550))using(var g=Graphics.FromImage(image))
        {
            g.Clear(Color.White);
            for(int i=0;i<4;i++) { bool slab=i>=2,down=i%2==1;Panel(g,new NodeSketch { IsSlab=slab,Hanging=down },slab?5:4,(slab?"板":"梁")+(down?"下下挂":"上反坎")+(slab?" · H":" · 左锚固"),i%2*360,i/2*275); }
            image.Save(System.IO.Path.Combine(dir,"sketch-beam-slab.png"));
        }
        foreach(bool left in new[]{false,true})using(var image=new Bitmap(720,825))using(var g=Graphics.FromImage(image))
        {
            g.Clear(Color.White);var modes=new[]{1,1,2,3,4,4};var parameters=new[]{0,1,2,3,5,6};var names=new[]{"b","A","V","E","B","Lin"};
            for(int i=0;i<6;i++)Panel(g,new CantileverSketch { Leftward=left,Mode=modes[i] },parameters[i],"模式"+modes[i]+" · "+names[i]+(left?" · 向左":" · 向右"),i%2*360,i/2*275);
            image.Save(System.IO.Path.Combine(dir,left?"sketch-cantilever-left.png":"sketch-cantilever-right.png"));
        }
    }
    [STAThread]static void Main()
    {
        try { NodeGeometry();CantileverGeometry();FocusIntegration();Pictures();Console.WriteLine("SKETCH_DIAGRAM_PASS checks="+checks); }
        catch(Exception ex) { Console.WriteLine(ex);Environment.ExitCode=1; }
    }
}
