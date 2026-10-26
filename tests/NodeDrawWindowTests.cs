using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WallDetailTool;

internal static class NodeDrawWindowTests
{
    static int checks;
    static void Ok(bool value,string name) { checks++; if(!value) throw new Exception(name); }
    static void Near(double a,double b,string name) { Ok(Math.Abs(a-b)<1e-8,name); }
    static void Reject(Action action,string name)
    { try { action(); } catch(InvalidOperationException) { checks++; return; } throw new Exception(name); }
    static T Find<T>(Form f,string name) where T:Control { return (T)f.Controls.Find(name,true)[0]; }
    static string[] Up(bool slab)
    { return slab?new[]{"8","150","6","200","180","31","250",""}:new[]{"8","150","6","200","350",""}; }
    static string[] Down(bool slab)
    { return slab?new[]{"10","125","8","180","220","40","420","460"}:new[]{"10","125","8","180","470","490"}; }
    static void Render(Form f,string name)
    {
        f.Show(); Application.DoEvents(); f.PerformLayout();
        using(var bitmap=new Bitmap(f.Width,f.Height))
        {
            f.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
            int red=0;
            for(int y=0;y<bitmap.Height;y+=2) for(int x=0;x<bitmap.Width;x+=2)
            { var c=bitmap.GetPixel(x,y); if(c.R>100 && c.G<100 && c.B<100) red++; }
            Ok(red>15,"native sketch actually painted");
            bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name+".png"));
        }
        f.Hide();
    }
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        foreach(bool slab in new[]{false,true})
        foreach(bool initialDown in new[]{false,true})
        {
            var up=Up(slab); var down=Down(slab);
            var state=new NodeDrawState(slab,initialDown,up,down);
            double[] a={25,12,12,20,62.5},b={30,8,10,25,50};
            using(var form=new NodeDrawForm(state,a,b))
            {
                var selector=Find<ComboBox>(form,"Direction");
                Ok(selector.SelectedIndex==(initialDown?1:0),"forced initial direction");
                // Mutating caller-owned arrays cannot change seeds or advanced settings.
                up[0]="999"; down[0]="999"; a[0]=999; b[0]=999;
                for(int direction=0;direction<2;direction++)
                {
                    selector.SelectedIndex=direction;
                    bool hanging=direction==1;
                    string node=(slab?"板":"梁")+(hanging?"下下挂":"上反坎");
                    Ok(form.Text.Contains(node),"title agrees");
                    Ok(Find<Button>(form,"Generate").Text=="生成"+node,"button agrees");
                    Ok(Find<NodeSketch>(form,"Sketch").Hanging==hanging,"sketch agrees");
                    Ok(Find<Label>(form,"Description").Text.Contains(hanging?"向上":"向下"),"entry direction explanation");
                    if(slab) Ok(Find<Label>(form,"HLabel").Text.Contains(hanging?"板顶至水平筋中心线距离 H":"板底至水平筋中心线距离 H"),"H meaning caption");
                    var request=form.CreateRequest();
                    Ok(request.IsSlab==slab && request.Hanging==hanging,"complete request family and direction");
                    Near(request.Value(0),hanging?10:8,"independent saved specs");
                    Near(request.Advanced(0),hanging?30:25,"advanced snapshot");
                    if(direction==0) Near(request.Value(slab?7:5),request.Value(slab?6:4),"blank right follows left");
                    Render(form,(slab?"slab":"beam")+(hanging?"_down":"_up"));
                }
                selector.SelectedIndex=0;
                Find<TextBox>(form,"Parameter0").Text="editing-invalid";
                Find<TextBox>(form,"Parameter"+(slab?7:5)).Text="";
                selector.SelectedIndex=1;
                Find<TextBox>(form,"Parameter0").Text="14";
                var accepted=form.CreateRequest();
                selector.SelectedIndex=0;
                Ok(Find<TextBox>(form,"Parameter0").Text=="editing-invalid","invalid upward draft survives switch");
                Ok(Find<TextBox>(form,"Parameter"+(slab?7:5)).Text=="","blank draft survives switch");
                Reject(()=>form.CreateRequest(),"invalid spec cannot create request");
                Find<TextBox>(form,"Parameter0").Text="NaN"; Reject(()=>form.CreateRequest(),"NaN rejected");
                Find<TextBox>(form,"Parameter0").Text="Infinity"; Reject(()=>form.CreateRequest(),"Infinity rejected");
                Find<TextBox>(form,"Parameter0").Text="-1"; Reject(()=>form.CreateRequest(),"negative rejected");
                Find<TextBox>(form,"Parameter0").Text="0"; Reject(()=>form.CreateRequest(),"zero rejected");
                selector.SelectedIndex=1;
                Ok(Find<TextBox>(form,"Parameter0").Text=="14","downward draft survives switch");
                Find<TextBox>(form,"Parameter0").Text="16";
                Near(accepted.Value(0),14,"accepted request immutable after further edits");
                Ok(accepted.Hanging,"accepted direction not changed by selector");
                form.Close(); Ok(form.Request==null,"closing without acceptance has no submitted request");
            }
            // A fresh opening uses only successful records, not cancelled drafts.
            using(var reopened=new NodeDrawForm(new NodeDrawState(slab,false,Up(slab),Down(slab)),a,b))
            {
                Near(reopened.CreateRequest().Value(0),8,"cancel/reopen discards drafts");
                Find<ComboBox>(reopened,"Direction").SelectedIndex=1;
                Near(reopened.CreateRequest().Value(0),10,"other cancelled draft discarded");
                if(slab)
                {
                    Find<TextBox>(reopened,"Parameter4").Text="";
                    Near(reopened.CreateRequest().Value(4),0,"full-section thickness optional");
                }
            }
            using(var cancelled=new NodeDrawForm(new NodeDrawState(slab,initialDown,Up(slab),Down(slab)),a,b))
            {
                cancelled.Show(); Application.DoEvents();
                Find<Button>(cancelled,"Cancel").PerformClick();
                Ok(cancelled.DialogResult==DialogResult.Cancel && cancelled.Request==null,"cancel button submits nothing");
            }
            using(var confirmed=new NodeDrawForm(new NodeDrawState(slab,initialDown,Up(slab),Down(slab)),a,b))
            {
                confirmed.Show(); Application.DoEvents();
                Find<Button>(confirmed,"Generate").PerformClick();
                Ok(confirmed.DialogResult==DialogResult.OK && confirmed.Request!=null,"generate button submits request");
                Ok(confirmed.Request.Hanging==initialDown && confirmed.Request.IsSlab==slab,"button submits selected direction and family");
            }
        }
        // Exact persistent payload compatibility: old upward records, current
        // downward records, new complete upward records; no partially read specs.
        var old3=SlabDrawRecord.Decode(new[]{31.0,250,300},false); Near(old3[3],0,"legacy 3 fields");
        var old4=SlabDrawRecord.Decode(new[]{40.0,260,320,180},false); Near(old4[3],180,"legacy 4 fields");
        var memory=new[]{40.0,250,420,200,10,125,8,180};
        foreach(bool down in new[]{false,true})
        {
            var decoded=SlabDrawRecord.Decode(memory,down); memory[4]=12;
            Near(decoded[0],40,"saved H"); Near(decoded[2],420,"saved independent right");
            Near(decoded[7],180,"saved dot spacing");
            Ok(decoded.Length==8,"full specification record");
            Reject(()=>SlabDrawRecord.Decode(new[]{40.0,250,420,200,10,125,8,double.NaN},down),"invalid saved spec rejected");
        }
        Reject(()=>SlabDrawRecord.Decode(new[]{31.0,250,300},true),"hanging never reads legacy upward format");
        Reject(()=>SlabDrawRecord.Decode(new[]{31.0,250},false),"truncated record rejected");
        var values=new[]{8.0,150,6,200,350,470}; var advanced=new[]{25.0,12,12,20,62.5};
        var immutable=new NodeDrawRequest(false,false,values,advanced); values[0]=999; advanced[0]=999;
        Near(immutable.Value(0),8,"request owns input snapshot"); Near(immutable.Advanced(0),25,"request owns advanced snapshot");
        Console.WriteLine("NODE_DRAW_WINDOW_PASS checks="+checks);
    }
}
