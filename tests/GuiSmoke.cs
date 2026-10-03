using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using BiliGreenDownloader;

internal static class GuiSmoke {
    [STAThread] static int Main(string[] args){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        using(var f=new MainForm()){
            f.Show();Application.DoEvents();
            if(args.Length>1&&args[1]=="populated"){
                var field=typeof(MainForm).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic);var state=(AppState)field.GetValue(f);state.Jobs.Clear();
                state.Jobs.Add(new Job{Time="09-29 20:15:12",Status="成功",Title="绿幕素材示例（界面测试）",Detail="1920 × 1080 · MP4"});
                state.Jobs.Add(new Job{Time="09-29 20:12:09",Status="取消",Title="绿幕素材示例（界面测试）",Detail="已停止，可再次下载同一链接以复用片段。"});
                typeof(MainForm).GetMethod("RenderHistory",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,null);
            }
            Application.DoEvents();using(var bitmap=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bitmap,new Rectangle(0,0,f.Width,f.Height));bitmap.Save(args[0]);}
            var controls=Walk(f).ToArray();Console.WriteLine("GUI shown; controls="+controls.Length+"; buttons="+controls.OfType<Button>().Count());
            if(!controls.Any(c=>c.Text=="开始下载")||!controls.Any(c=>c.Name=="DownloadHistory"))return 1;
            foreach(var c in controls.Where(c=>c.Visible&&c is Button)){if(c.Width<50||c.Height<25)throw new Exception("button clipped: "+c.Text);}
            // Avoid saving fixture records through the normal close handler.
            f.Dispose();return 0;
        }
    }
    static System.Collections.Generic.IEnumerable<Control> Walk(Control c){foreach(Control child in c.Controls){yield return child;foreach(var nested in Walk(child))yield return nested;}}
}
