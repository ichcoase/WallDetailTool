using System;
using System.Collections.Generic;
using System.Linq;
namespace WallDetailTool
{
    internal static class TopSlabTests
    {
        static int checks;
        static void Check(bool ok,string message) { checks++;if(!ok)throw new Exception(message); }
        static void Reject(Action action) { bool rejected=false;try { action(); }catch(ArgumentException) { rejected=true; }catch(InvalidOperationException) { rejected=true; }Check(rejected,"invalid input accepted"); }
        static List<ContourSegment> Source(int direction,double wall,double slab,int offset,bool reverse,bool subdivide)
        {
            var v=new[]{new ContourPoint(0,0),new ContourPoint(wall,0),new ContourPoint(wall,800-slab),new ContourPoint(wall+900,800-slab),new ContourPoint(wall+900,800),new ContourPoint(0,800)};
            var points=new List<ContourPoint>();
            for(int i=0;i<6;i++) { int index=(offset+(reverse?-i:i)+12)%6;points.Add(new ContourPoint(1000+direction*v[index].X,200+v[index].Y)); }
            var edges=new List<ContourSegment>();
            for(int i=0;i<6;i++)
            {
                var a=points[i];var b=points[(i+1)%6];
                if(subdivide) { var m=new ContourPoint((a.X+b.X)/2,(a.Y+b.Y)/2);edges.Add(new ContourSegment(a,m));edges.Add(new ContourSegment(m,b)); }
                else edges.Add(new ContourSegment(a,b));
            }
            var random=new Random(offset*37+direction+(reverse?100:200)+(subdivide?300:0));
            for(int i=edges.Count-1;i>0;i--) { int j=random.Next(i+1);var e=edges[i];edges[i]=edges[j];edges[j]=e; }
            for(int i=0;i<edges.Count;i+=2) { var e=edges[i];edges[i]=new ContourSegment(e.B,e.A); }
            return edges;
        }
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                if(args.Contains("--open-only")) { TopSlabOpenTests.Run();return 0; }
                if(args.Contains("--collision-only")) { TopSlabCollisionTests.Run();return 0; }
                if(args.Contains("--slab-window-only"))
                {
                    var preset=TopSlabForm.TrialValues(TopSlabBase.Slab,8,150,6,200,25,8);
                    Check(preset[11]==60&&preset[12]==150&&preset[13]==150&&preset[14]==120&&preset[15]==29,"confirmed slab defaults and existing effective H");
                    Check(TopSlabForm.TrialValues(TopSlabBase.Slab,8,150,6,200,25,12)[15]==31,"factory H31");
                    var slabInitial=new[]{TopSlabForm.TrialValues(TopSlabBase.Beam,8,150,6,200,25,8).Select(v=>v.ToString()).ToArray(),preset.Select(v=>v.ToString()).ToArray()};
                    using(var form=new TopSlabForm(slabInitial,4))
                    {
                        form.Show();System.Windows.Forms.Application.DoEvents();form.SetBase(TopSlabBase.Slab);
                        var outer=(System.Windows.Forms.TextBox)form.Controls.Find("Parameter12",true)[0];
                        var inner=(System.Windows.Forms.TextBox)form.Controls.Find("Parameter13",true)[0];
                        var h=(System.Windows.Forms.TextBox)form.Controls.Find("Parameter14",true)[0];
                        var H=(System.Windows.Forms.TextBox)form.Controls.Find("Parameter15",true)[0];
                        var grid=(System.Windows.Forms.TableLayoutPanel)h.Parent;
                        Check(grid.GetRow(h)==12&&grid.GetRow(H)==13&&grid.GetRow(outer)==14&&grid.GetRow(inner)==15,"slab order h H Louter Linner");
                        Check(((System.Windows.Forms.Label)form.Controls.Find("Caption13",true)[0]).Text.Contains("留空同外侧"),"explicit optional inner length");
                        inner.Text="";outer.Text="420";H.Text="200";
                        var generate=(System.Windows.Forms.Button)form.Controls.Find("Generate",true)[0];generate.PerformClick();
                        Check(!form.Selecting&&form.Values==null,"invalid H blocked before selecting");
                        H.Text="29";form.SetBase(TopSlabBase.Beam);
                        Check(outer.Text=="300"&&inner.Text=="300"&&!h.Visible&&!H.Visible,"beam draft isolated and slab-only fields hidden");
                        form.SetBase(TopSlabBase.Slab);Check(outer.Text=="420"&&inner.Text==""&&H.Text=="29","raw slab draft restored");
                        generate.PerformClick();Check(form.Selecting&&form.Values[12]==420&&form.Values[13]==420&&form.Values[14]==120&&form.Values[15]==29,"blank inner resolves into complete immutable request values");
                    }
                    Check(slabInitial[1][12]=="150"&&slabInitial[1][13]=="150","editing does not overwrite supplied values");
                    Console.WriteLine("TOP_SLAB_SLAB_WINDOW_PASS checks="+checks);return 0;
                }
                if(args.Contains("--auto-window-only"))
                {
                    var autoWindowInitial=new[]{Enumerable.Repeat("40",16).ToArray(),Enumerable.Repeat("60",16).ToArray()};
                    using(var form=new TopSlabForm(autoWindowInitial,4))
                    {
                        var automatic=(System.Windows.Forms.TextBox)form.Controls.Find("Parameter10",true)[0];
                        var b=(System.Windows.Forms.TextBox)form.Controls.Find("Parameter11",true)[0];
                        Check(!automatic.Enabled&&automatic.ReadOnly&&automatic.Text=="自动","A automatic read-only");
                        b.Text="invalid beam B";form.SetBase(TopSlabBase.Slab);
                        Check(!automatic.Enabled&&automatic.Text=="自动","A stays automatic after type switch");
                        b.Text="";form.SetBase(TopSlabBase.Beam);Check(b.Text=="invalid beam B","B raw beam draft retained");
                        form.SetBase(TopSlabBase.Slab);Check(b.Text=="","B raw slab draft retained");
                    }
                    Check(autoWindowInitial[0][10]=="40"&&autoWindowInitial[1][10]=="60","legacy input arrays preserved");
                    var workflowInitial=new[]{Enumerable.Repeat("150",16).ToArray(),Enumerable.Repeat("250",16).ToArray()};
                    workflowInitial[0][12]="300";workflowInitial[0][13]="300";
                    using(var form=new TopSlabForm(workflowInitial,4))
                    {
                        var outer=(System.Windows.Forms.TextBox)form.Controls.Find("Parameter12",true)[0];
                        var inner=(System.Windows.Forms.TextBox)form.Controls.Find("Parameter13",true)[0];
                        var b=(System.Windows.Forms.TextBox)form.Controls.Find("Parameter11",true)[0];
                        var button=(System.Windows.Forms.Button)form.Controls.Find("Generate",true)[0];
                        Check(button.Text=="选择轮廓并生成","single workflow action");
                        outer.Text="420";Check(inner.Text=="420","beam default follows outer");
                        inner.Text="350";outer.Text="430";Check(inner.Text=="350","manual inner override preserved");
                        form.SetBase(TopSlabBase.Slab);Check(inner.Text=="250","slab draft isolated");
                        form.SetBase(TopSlabBase.Beam);Check(inner.Text=="350"&&outer.Text=="430","beam draft restored");
                        form.Show();System.Windows.Forms.Application.DoEvents();b.Text="";button.PerformClick();
                        Check(!form.Selecting&&form.Values==null,"invalid parameters do not select");
                        b.Text="60";button.PerformClick();
                        Check(form.Selecting&&form.DialogResult==System.Windows.Forms.DialogResult.Retry&&form.Values[12]==430&&form.Values[13]==350,"valid parameters select without existing geometry");
                    }
                    Console.WriteLine("TOP_SLAB_AUTO_WINDOW_PASS checks="+checks);return 0;
                }
                foreach(int d in new[]{-1,1})foreach(double wall in new[]{250.0,300.0})foreach(double slab in new[]{200.0,250.0})
                foreach(int start in Enumerable.Range(0,6))foreach(bool reverse in new[]{false,true})foreach(bool split in new[]{false,true})
                {
                    var source=Source(d,wall,slab,start,reverse,split);var g=TopSlabRecognizer.Recognize(source,7);
                    Check(g.Direction==d&&g.WallThickness==wall&&g.SlabThickness==slab&&g.Projection==900&&g.Height==800&&g.Elevation==7,"recognition");
                    foreach(TopSlabBase type in new[]{TopSlabBase.Beam,TopSlabBase.Slab})
                    {
                        object target=new object();double[] values={12,150,16,150,10,150,6,200,8,200,40,60,300,350,200,31};
                        double[] advanced={25,12,12,20,30};string[] layers={"a","b","c","d"};
                        var r=new TopSlabRequest(target,g,type,4,values,advanced,layers);values[0]=999;advanced[0]=999;layers[0]="changed";
                        Check(r.V(0)==12&&r.A(0)==25&&r.Layer(0)=="a"&&r.Matches(target,4)&&!r.Matches(new object(),4)&&!r.Matches(target,0),"immutable target");
                        var layout=TopSlabRebar.Calculate(r);Check(layout.Bars.Count==3,"three bars");
                        Check(Math.Abs(layout.Bars[0][0].Y-(type==TopSlabBase.Beam?200-300:200-200+31))<1e-8,"outer bottom definition");
                        Check(Math.Abs(layout.Bars[0].Last().Y-layout.Bars[2][0].Y-10)<1e-8,"existing cantilever free end");
                        bool crossing=false;
                        var innerBar=layout.Bars[1];var slabBar=layout.Bars[2];
                        for(int i=0;i<innerBar.Count-1;i++)for(int j=0;j<slabBar.Count-1;j++)
                        {
                            var a=innerBar[i];var b=innerBar[i+1];var c1=slabBar[j];var e=slabBar[j+1];
                            crossing|=BeamSideCurve.Intersections(new BeamSideCurve(new ContourPoint(a.X,a.Y),new ContourPoint(b.X,b.Y),a.Bulge),
                                new BeamSideCurve(new ContourPoint(c1.X,c1.Y),new ContourPoint(e.X,e.Y),c1.Bulge)).Count>0;
                        }
                        Check(crossing,"independent inner/slab crossing");
                        Check(layout.WallDots.Count>0&&layout.SlabDots.Count>0,"dot rows");
                        double outer=25+6,inner=wall-25-8,off=12;
                        foreach(var p in layout.WallLeftDots.Skip(1).Concat(layout.WallRightDots.Skip(1))) { double x=d*(p.X-1000);Check(Math.Abs(x-(outer+off))<1e-8||Math.Abs(x-(inner-off))<1e-8,"wall tangent"); }
                        double top=800-25-6,bottom=800-slab+25+5;
                        foreach(var p in layout.SlabDots.Skip(1))Check(Math.Abs(p.Y-200-(top-off))<1e-8||Math.Abs(p.Y-200-(bottom+off))<1e-8,"slab straight tangent");
                        foreach(var p in layout.SlabDots)Check(d*(p.X-g.InnerX)-r.A(2)/2>=-1e-8,"no slab dots in wall domain");
                        foreach(var row in new[]{layout.WallLeftDots,layout.WallRightDots}) { var sorted=row.OrderBy(p=>p.Y).ToArray();for(int i=1;i<sorted.Length;i++)Check(sorted[i].Y-sorted[i-1].Y<=r.V(7)+1e-8,"wall spacing"); }
                        foreach(var row in layout.SlabDots.GroupBy(p=>p.Y)) { var sorted=row.OrderBy(p=>p.X).ToArray();for(int i=1;i<sorted.Length;i++)Check(sorted[i].X-sorted[i-1].X<=r.V(9)+1e-8,"slab spacing"); }
                        Check(TopSlabRequest.LastKey(type)!=TopSlabRequest.LastKey(type==TopSlabBase.Beam?TopSlabBase.Slab:TopSlabBase.Beam),"type isolation");
                        Check(r.Record().Length==23,"record length");
                    }
                }
                var s=Source(1,300,200,0,false,false);s.RemoveAll(e=>e.A.X==2200&&e.B.X==2200);Reject(()=>TopSlabRecognizer.Recognize(s,0));
                s=Source(1,300,200,0,false,false);s.Add(s[0]);Reject(()=>TopSlabRecognizer.Recognize(s,0));
                s=Source(1,300,200,0,false,false);s[0]=new ContourSegment(s[0].A,new ContourPoint(s[0].B.X+3,s[0].B.Y+3));Reject(()=>TopSlabRecognizer.Recognize(s,0));
                s=Source(1,300,200,0,false,false);s.Add(new ContourSegment(new ContourPoint(0,0),new ContourPoint(10,0)));Reject(()=>TopSlabRecognizer.Recognize(s,0));
                Reject(()=>TopSlabRecognizer.Recognize(Source(1,300,800,0,false,false),0));
                Reject(()=>new TopSlabGeometry(0,0,800,300,0,900,0,1));
                var g0=TopSlabRecognizer.Recognize(Source(1,300,200,0,false,false),0);
                var v0=new double[]{12,150,16,150,10,150,6,200,8,200,40,60,300,350,200,31};
                var a0=new double[]{25,12,12,20,30};var l0=new[]{"a","b","c","d"};
                var baseline=new TopSlabRequest(new object(),g0,TopSlabBase.Beam,4,v0,a0,l0);
                var drawn=TopSlabRebar.Calculate(baseline);
                var svg=new System.Text.StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='1300' height='1100' viewBox='850 -1150 1450 1300'><g transform='scale(1,-1)'>");
                svg.Append("<path d='M1000 200H1300V800H2200V1000H1000Z' fill='none' stroke='gray' stroke-width='2'/>");
                foreach(var b in drawn.Bars)for(int i=0;i<b.Count-1;i++)
                {
                    var curve=new BeamSideCurve(new ContourPoint(b[i].X,b[i].Y),new ContourPoint(b[i+1].X,b[i+1].Y),b[i].Bulge);
                    for(int j=0;j<24;j++) { var p=curve.At(j/24.0);var q=curve.At((j+1)/24.0);svg.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"<path d='M{0} {1}L{2} {3}' stroke='{4}' stroke-width='12'/>",p.X,p.Y,q.X,q.Y,new[]{"red","orange","purple"}[drawn.Bars.IndexOf(b)]); }
                }
                foreach(var p in drawn.WallDots.Concat(drawn.SlabDots))svg.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"<circle cx='{0}' cy='{1}' r='6' fill='teal'/>",p.X,p.Y);
                svg.Append("</g></svg>");System.IO.File.WriteAllText("tmp/top-slab-auto-20261104/calculated.svg",svg.ToString());
                TopSlabRevisionTests.Run();
                TopSlabOpenTests.Run();
                var invalid=(double[])v0.Clone();invalid[10]=1000;Check(TopSlabRebar.Calculate(new TopSlabRequest(new object(),g0,TopSlabBase.Beam,4,invalid,a0,l0)).EffectiveA==drawn.EffectiveA,"old manual A ignored");
                Reject(()=>new TopSlabRequest(new object(),g0,TopSlabBase.Beam,6,v0,a0,l0));
                invalid=(double[])v0.Clone();invalid[11]=double.NaN;Reject(()=>new TopSlabRequest(new object(),g0,TopSlabBase.Beam,4,invalid,a0,l0));
                var initial=new[]{v0.Select(v=>v.ToString()).ToArray(),v0.Select(v=>v.ToString()).ToArray()};
                using(var form=new TopSlabForm(initial,4))
                {
                    var field=(System.Windows.Forms.TextBox)form.Controls.Find("Parameter11",true)[0];field.Text="invalid draft";
                    form.SetBase(TopSlabBase.Slab);Check(field.Text=="60","type initial");field.Text="";
                    form.SetBase(TopSlabBase.Beam);Check(field.Text=="invalid draft","raw beam draft");
                    form.SetBase(TopSlabBase.Slab);Check(field.Text=="","raw slab draft");
                    var copy=form.Drafts();copy[1][11]="changed";Check(form.Drafts()[1][11]=="","draft defensive copy");
                    form.Geometry=g0;form.RefreshGeometry();
                    form.Show();System.Windows.Forms.Application.DoEvents();
                    using(var image=new System.Drawing.Bitmap(form.Width,form.Height)) { form.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,image.Width,image.Height));image.Save("tmp/top-slab-auto-20261104/window.png"); }
                    form.Close();
                }
                Check(initial[0][10]=="40","window does not mutate input");
                Console.WriteLine("TopSlabTests PASS: "+checks);return 0;
            }
            catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
        }
    }
}
