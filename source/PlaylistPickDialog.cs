using System;
using System.Drawing;
using System.Windows.Forms;

namespace BiliGreenDownloader {
    /// <summary>
    /// 多 P 视频的选择窗口：列出「第几 P / 标题 / 时长」，默认选中第 1 P。
    /// 2026-11-14 新增：链接不带分 P 参数、又确实是多 P 视频时弹出来，
    /// 让使用者点选，不用自己拼 ?p=2（怕新手不会）。
    /// </summary>
    internal sealed class PlaylistPickDialog : Form {
        internal int SelectedPage {get;private set;}
        readonly ListView list=new ListView();

        public PlaylistPickDialog(PlaylistInfo info) {
            if(info==null||info.Pages.Count==0)throw new InvalidOperationException("没有可选择的 P。");
            Text="选择要下载的分 P";
            FormBorderStyle=FormBorderStyle.Sizable;
            StartPosition=FormStartPosition.CenterParent;
            ShowInTaskbar=false;
            MinimizeBox=false;
            MaximizeBox=false;
            ClientSize=new Size(760,460);
            MinimumSize=new Size(620,360);
            Font=new Font("Microsoft YaHei UI",9f);
            BackColor=Color.White;

            var hint=new Label();
            hint.Dock=DockStyle.Top;
            hint.Height=56;
            hint.Padding=new Padding(14,10,14,4);
            hint.Text="这个链接是一个多 P 视频（合集），一共 "+info.Pages.Count+" P。\r\n请选择要下载的那一 P：";
            hint.AutoEllipsis=true;

            list.Dock=DockStyle.Fill;
            list.View=View.Details;
            list.FullRowSelect=true;
            list.MultiSelect=false;
            list.HideSelection=false;
            list.GridLines=false;
            list.Columns.Add("P",56,HorizontalAlignment.Left);
            list.Columns.Add("标题",520,HorizontalAlignment.Left);
            list.Columns.Add("时长",90,HorizontalAlignment.Left);
            foreach(var page in info.Pages){
                var item=new ListViewItem(new[]{page.Index.ToString(),String.IsNullOrEmpty(page.Title)?("P"+page.Index):page.Title,DurationText(page.Duration)});
                item.Tag=page;
                list.Items.Add(item);
            }
            if(list.Items.Count>0)list.Items[0].Selected=true;
            list.DoubleClick+=delegate{if(list.SelectedItems.Count>0){SelectedPage=list.SelectedItems[0].Index+1;DialogResult=DialogResult.OK;}};
            Controls.Add(list);

            var buttons=new FlowLayoutPanel();
            buttons.Dock=DockStyle.Bottom;
            buttons.Height=52;
            buttons.FlowDirection=FlowDirection.RightToLeft;
            buttons.Padding=new Padding(0,10,14,0);
            var ok=new Button();
            ok.Text="下载这一 P";
            ok.Width=120;
            ok.Height=32;
            ok.Click+=delegate{AcceptSelection();};
            var cancel=new Button();
            cancel.Text="取消";
            cancel.Width=90;
            cancel.Height=32;
            cancel.DialogResult=DialogResult.Cancel;
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            Controls.Add(buttons);
            Controls.Add(hint);
            AcceptButton=ok;
            CancelButton=cancel;
        }

        static string DurationText(double seconds) {
            if(seconds<=0)return "";
            long total=(long)Math.Round(seconds);
            if(total>=3600)return (total/3600).ToString()+":"+(total%3600/60).ToString("00")+":"+(total%60).ToString("00");
            return (total/60).ToString()+":"+(total%60).ToString("00");
        }

        void AcceptSelection() {
            if(list.SelectedItems.Count==0){MessageBox.Show(this,"请先在列表里点选一个 P。","还没有选择",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            SelectedPage=list.SelectedItems[0].Index+1;
            DialogResult=DialogResult.OK;
        }
    }
}
