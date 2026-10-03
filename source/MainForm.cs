using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BiliGreenDownloader {
    internal sealed class Job {
        public string Time="",Title="",Status="",Path="",Detail="";
        public Dictionary<string,object> Data(){return new Dictionary<string,object>{{"time",Time},{"title",Title},{"status",Status},{"path",Path},{"detail",Detail}};}
        public static Job Read(Dictionary<string,object> d){return new Job{Time=Json.Get(d,"time"),Title=Json.Get(d,"title"),Status=Json.Get(d,"status"),Path=Json.Get(d,"path"),Detail=Json.Get(d,"detail")};}
    }
    internal sealed class AppState {
        public string Folder="";public bool Limit1080,Compatible=true;public readonly List<Job> Jobs=new List<Job>();
        public static AppState Load(string file){
            var state=new AppState();if(!File.Exists(file))return state;
            var d=Json.Object(File.ReadAllText(file,Encoding.UTF8));state.Folder=Json.Get(d,"folder");state.Limit1080=Json.Get(d,"limit1080")=="True";state.Compatible=Json.Get(d,"compatible","True")!="False";
            object rows;if(d.TryGetValue("jobs",out rows)&&rows is List<object>)foreach(var item in ((List<object>)rows).Take(50)){var row=item as Dictionary<string,object>;if(row==null)continue;var job=Job.Read(row);if(job.Status=="处理中"){job.Status="已中断";job.Detail="软件上次关闭时尚未完成，可重新下载以复用片段。";}state.Jobs.Add(job);}return state;
        }
        public void Save(string file){Core.AtomicText(file,Json.Write(new Dictionary<string,object>{{"folder",Folder},{"limit1080",Limit1080},{"compatible",Compatible},{"jobs",Jobs.Take(50).Select(j=>j.Data()).ToList()}}));}
    }
    internal sealed class MainForm:Form {
        readonly TextBox link=new TextBox(),folder=new TextBox(),logBox=new TextBox();
        readonly Button start=new Button(),cancel=new Button(),choose=new Button(),cookies=new Button(),clearCookie=new Button(),update=new Button(),import=new Button();
        readonly ComboBox quality=new ComboBox();readonly CheckBox compatible=new CheckBox();
        readonly Label status=new Label(),cookieLabel=new Label();readonly ProgressBar bar=new ProgressBar();
        readonly ListView history=new ListView();readonly TabControl tabs=new TabControl();readonly ToolTip tips=new ToolTip();
        readonly string stateFile=Path.Combine(RuntimeSetup.Root,"settings.json"),logFile=Path.Combine(RuntimeSetup.Root,"latest.log");
        readonly AppState state;CancellationTokenSource cancellation;bool busy,closeAfter;string cookiePath="";
        internal MainForm(){
            Text="视频素材下载器  "+Core.Version;Font=new Font("Microsoft YaHei UI",9F);AutoScaleMode=AutoScaleMode.Dpi;
            ClientSize=new Size(940,790);MinimumSize=new Size(880,750);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(245,247,250);
            try{state=AppState.Load(stateFile);}catch{state=new AppState();}
            Build();LoadSettings();RenderHistory();FormClosing+=HandleClosing;
            AppendLog("视频素材下载器 "+Core.Version+" · "+(Environment.Is64BitOperatingSystem?"64 位系统":"32 位系统"));
            status.Text="粘贴链接即可开始。首次使用会自动准备下载组件。";
        }
        static Button Button(string text,EventHandler click,int width=110){var b=new Button{Text=text,Width=width,Height=32,Margin=new Padding(0,0,8,0),UseVisualStyleBackColor=true};if(click!=null)b.Click+=click;return b;}
        static Button Button(string text,int width,EventHandler click){return Button(text,click,width);}
        static Label Label(string text){return new Label{Text=text,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(0,3,8,3)};}
        static void Configure(Button b,string text,int width,EventHandler click){b.Text=text;b.Width=width;b.Height=32;b.Margin=new Padding(0,0,8,0);b.UseVisualStyleBackColor=true;b.Click+=click;}
        void Build(){
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22,17,22,15),ColumnCount=1,RowCount=8};Controls.Add(root);
            foreach(int height in new[]{34,30,212,46,55,34})root.RowStyles.Add(new RowStyle(SizeType.Absolute,height));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
            var title=Label("视频素材下载器");title.Font=new Font(Font.FontFamily,19,FontStyle.Bold);title.ForeColor=Color.FromArgb(27,39,54);root.Controls.Add(title,0,0);
            var subtitle=Label("B 站 / YouTube 素材 · 默认输出 H.264 / AAC 的 MP4，便于导入剪辑软件");subtitle.ForeColor=Color.FromArgb(84,97,115);root.Controls.Add(subtitle,0,1);
            var box=new TableLayoutPanel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(12),ColumnCount=3,RowCount=5,Margin=new Padding(0,7,0,10)};
            box.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,78));box.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));box.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,108));
            foreach(int h in new[]{36,36,36,36,22})box.RowStyles.Add(new RowStyle(SizeType.Absolute,h));root.Controls.Add(box,0,2);
            box.Controls.Add(Label("视频链接"),0,0);link.Dock=DockStyle.Fill;link.Margin=new Padding(0,4,9,3);link.Name="VideoUrl";box.Controls.Add(link,1,0);
            box.Controls.Add(Button("粘贴",(s,e)=>{try{if(Clipboard.ContainsText())link.Text=Clipboard.GetText();}catch(Exception ex){ShowError(ex.Message);}},98),2,0);
            box.Controls.Add(Label("保存位置"),0,1);folder.Dock=DockStyle.Fill;folder.Margin=new Padding(0,4,9,3);box.Controls.Add(folder,1,1);Configure(choose,"选择文件夹",98,ChooseFolder);box.Controls.Add(choose,2,1);
            box.Controls.Add(Label("清晰度"),0,2);var choices=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0)};box.Controls.Add(choices,1,2);box.SetColumnSpan(choices,2);
            quality.DropDownStyle=ComboBoxStyle.DropDownList;quality.Width=210;quality.Items.AddRange(new object[]{"当前可用的最高画质","优先 1080P 及以下"});quality.Margin=new Padding(0,3,18,0);choices.Controls.Add(quality);
            compatible.Text="PR 兼容 MP4";compatible.AutoSize=true;compatible.Margin=new Padding(0,7,0,0);choices.Controls.Add(compatible);
            tips.SetToolTip(compatible,"已兼容的编码直接封装；不兼容的编码转为 H.264 + AAC。转码会使用 CPU，且有少量画质损失。");
            box.Controls.Add(Label("登录信息"),0,3);var auth=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0)};box.Controls.Add(auth,1,3);box.SetColumnSpan(auth,2);
            Configure(cookies,"选择 cookies.txt",149,ChooseCookies);auth.Controls.Add(cookies);Configure(clearCookie,"清除",64,(s,e)=>{cookiePath="";cookieLabel.Text="未导入 · 可先直接下载";});auth.Controls.Add(clearCookie);cookieLabel.Text="未导入 · 可先直接下载";cookieLabel.AutoSize=true;cookieLabel.Margin=new Padding(4,7,0,0);auth.Controls.Add(cookieLabel);
            var hint=Label("支持 B 站（BV 号 / 视频链接 / b23.tv 短链）与 YouTube（watch、youtu.be、shorts）；B 站分 P 链接下载指定的一 P。");hint.Font=new Font(Font.FontFamily,8.5F);hint.ForeColor=Color.FromArgb(93,105,119);box.Controls.Add(hint,0,4);box.SetColumnSpan(hint,3);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0,0,0,8)};root.Controls.Add(actions,0,3);
            Configure(start,"开始下载",133,StartDownload);start.BackColor=Color.FromArgb(36,110,224);start.ForeColor=Color.White;start.FlatStyle=FlatStyle.Flat;start.FlatAppearance.BorderSize=0;actions.Controls.Add(start);
            Configure(cancel,"取消任务",105,(s,e)=>{if(cancellation!=null){cancel.Enabled=false;status.Text="正在停止，请稍等…";cancellation.Cancel();}});cancel.Enabled=false;actions.Controls.Add(cancel);
            actions.Controls.Add(Button("打开保存位置",124,(s,e)=>OpenFolder(folder.Text)));
            Configure(update,"更新下载组件",124,UpdateTools);actions.Controls.Add(update);Configure(import,"导入已有组件",124,ImportTools);actions.Controls.Add(import);
            actions.Controls.Add(Button("使用说明",100,(s,e)=>ShowHelp()));
            var progress=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=new Padding(0)};progress.RowStyles.Add(new RowStyle(SizeType.Absolute,29));progress.RowStyles.Add(new RowStyle(SizeType.Absolute,16));root.Controls.Add(progress,0,4);
            status.AutoSize=false;status.Dock=DockStyle.Fill;status.AutoEllipsis=true;status.TextAlign=ContentAlignment.MiddleLeft;status.ForeColor=Color.FromArgb(41,72,113);status.Margin=new Padding(0);progress.Controls.Add(status,0,0);bar.Dock=DockStyle.Fill;bar.Margin=new Padding(0);progress.Controls.Add(bar,0,1);
            var section=Label("任务记录与日志");section.Font=new Font(Font.FontFamily,10,FontStyle.Bold);root.Controls.Add(section,0,5);
            tabs.Dock=DockStyle.Fill;tabs.Margin=new Padding(0);root.Controls.Add(tabs,0,6);var records=new TabPage("下载记录"){Padding=new Padding(7)};var logs=new TabPage("运行日志"){Padding=new Padding(7)};tabs.TabPages.AddRange(new[]{records,logs});
            var recordLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};recordLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));recordLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,37));records.Controls.Add(recordLayout);
            history.Name="DownloadHistory";history.View=View.Details;history.FullRowSelect=true;history.MultiSelect=false;history.HideSelection=false;history.Dock=DockStyle.Fill;history.BorderStyle=BorderStyle.FixedSingle;history.Columns.Add("时间",126);history.Columns.Add("状态",76);history.Columns.Add("视频 / 任务",300);history.Columns.Add("说明",308);history.DoubleClick+=(s,e)=>OpenSelected();recordLayout.Controls.Add(history,0,0);
            var recordButtons=new FlowLayoutPanel{Dock=DockStyle.Fill,Margin=new Padding(0,5,0,0)};recordButtons.Controls.Add(Button("查看文件",104,(s,e)=>OpenSelected()));recordButtons.Controls.Add(Button("查看任务详情",130,(s,e)=>{if(history.SelectedItems.Count>0){var job=(Job)history.SelectedItems[0].Tag;MessageBox.Show(this,job.Title+"\n\n"+job.Status+"："+job.Detail+(job.Path!=""?"\n\n"+job.Path:""),"任务详情",MessageBoxButtons.OK,MessageBoxIcon.Information);}}));recordLayout.Controls.Add(recordButtons,0,1);
            var logLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};logLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));logLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,37));logs.Controls.Add(logLayout);logBox.Multiline=true;logBox.ReadOnly=true;logBox.ScrollBars=ScrollBars.Vertical;logBox.Dock=DockStyle.Fill;logBox.BackColor=Color.White;logBox.WordWrap=true;logLayout.Controls.Add(logBox,0,0);
            var logButtons=new FlowLayoutPanel{Dock=DockStyle.Fill,Margin=new Padding(0,5,0,0)};logButtons.Controls.Add(Button("复制日志",104,(s,e)=>{try{if(logBox.Text.Length>0)Clipboard.SetText(logBox.Text);}catch(Exception ex){ShowError(ex.Message);}}));logButtons.Controls.Add(Button("导出日志",104,ExportLog));logLayout.Controls.Add(logButtons,0,1);
            var foot=Label("失败和取消也会留下记录。重复下载同一链接时会尽量续传；同名素材不会覆盖。");foot.Font=new Font(Font.FontFamily,8.5F);foot.ForeColor=Color.FromArgb(103,113,126);root.Controls.Add(foot,0,7);
        }
        void LoadSettings(){string defaultFolder=Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);if(String.IsNullOrWhiteSpace(defaultFolder))defaultFolder=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);folder.Text=String.IsNullOrWhiteSpace(state.Folder)?Path.Combine(defaultFolder,"视频素材"):state.Folder;quality.SelectedIndex=state.Limit1080?1:0;compatible.Checked=state.Compatible;}
        void SaveState(){try{state.Folder=folder.Text;state.Limit1080=quality.SelectedIndex==1;state.Compatible=compatible.Checked;state.Save(stateFile);}catch(Exception ex){AppendLog("保存下载记录 / 设置失败："+ex.Message);}}
        void RenderHistory(){history.BeginUpdate();history.Items.Clear();foreach(var j in state.Jobs.Take(50)){var item=new ListViewItem(new[]{j.Time,j.Status,j.Title,j.Detail}){Tag=j};if(j.Status=="成功")item.ForeColor=Color.FromArgb(20,123,67);else if(j.Status=="失败")item.ForeColor=Color.FromArgb(178,52,41);history.Items.Add(item);}history.EndUpdate();}
        Job NewJob(string title){var job=new Job{Time=DateTime.Now.ToString("MM-dd HH:mm:ss"),Title=title,Status="处理中",Detail="准备组件 / 解析视频"};state.Jobs.Insert(0,job);if(state.Jobs.Count>50)state.Jobs.RemoveRange(50,state.Jobs.Count-50);RenderHistory();SaveState();return job;}
        void SetBusy(bool value){busy=value;start.Enabled=choose.Enabled=link.Enabled=folder.Enabled=quality.Enabled=compatible.Enabled=cookies.Enabled=clearCookie.Enabled=update.Enabled=import.Enabled=!value;cancel.Enabled=value;}
        IProgress<Update> Progress(){return new Progress<Update>(u=>{if(IsDisposed||!busy)return;status.Text=u.Message;bar.Style=u.Percent<0?ProgressBarStyle.Marquee:ProgressBarStyle.Continuous;if(u.Percent>=0)bar.Value=Math.Max(0,Math.Min(100,u.Percent));});}
        void AppendLog(string text){
            if(IsDisposed)return;if(InvokeRequired){try{BeginInvoke(new Action<string>(AppendLog),text);}catch(InvalidOperationException){}return;}
            string line=DateTime.Now.ToString("HH:mm:ss")+"  "+Core.Redact(text)+Environment.NewLine;if(logBox.TextLength>160000)logBox.Text=logBox.Text.Substring(logBox.TextLength-80000);logBox.AppendText(line);
            try{Directory.CreateDirectory(RuntimeSetup.Root);if(File.Exists(logFile)&&new FileInfo(logFile).Length>1024*1024){string old=logFile+".1";if(File.Exists(old))File.Delete(old);File.Move(logFile,old);}File.AppendAllText(logFile,line,new UTF8Encoding(false));}catch{}
        }
        async void StartDownload(object sender,EventArgs e){
            if(busy)return;DownloadOptions options;
            try{options=new DownloadOptions{Url=Core.NormalizeUrl(link.Text),Folder=Path.GetFullPath(folder.Text),Limit1080=quality.SelectedIndex==1,Compatible=compatible.Checked,Cookies=cookiePath};}catch(Exception ex){ShowError(ex.Message);return;}
            var job=NewJob(options.Url);SetBusy(true);cancellation=new CancellationTokenSource();var token=cancellation.Token;var progress=Progress();tabs.SelectedIndex=0;AppendLog("开始下载任务；"+(options.Compatible?"输出 PR 兼容 MP4":"保留原始编码"));
            try{
                var result=await Task.Run(()=>{var tools=RuntimeSetup.Ensure(token,progress);return Downloader.Download(options,tools,token,progress,AppendLog);});
                job.Title=result.Title;job.Path=result.Path;job.Status="成功";job.Detail=result.Description;status.Text="已保存："+Path.GetFileName(result.Path);bar.Style=ProgressBarStyle.Continuous;bar.Value=100;AppendLog("下载完成："+Path.GetFileName(result.Path));
            }catch(OperationCanceledException){job.Status="取消";job.Detail="已停止，可再次下载同一链接以复用片段。";status.Text="任务已取消。";AppendLog(job.Detail);}
            catch(Exception ex){if(token.IsCancellationRequested){job.Status="取消";job.Detail="任务已停止，可重试。";}else{job.Status="失败";job.Detail=Core.Redact(ex.Message);AppendLog("失败："+ex.Message);if(ex.InnerException!=null)AppendLog(ex.InnerException.Message);}status.Text=job.Detail;tabs.SelectedIndex=1;}
            finally{RenderHistory();SaveState();FinishOperation();}
        }
        async void UpdateTools(object sender,EventArgs e){
            if(busy)return;SetBusy(true);cancellation=new CancellationTokenSource();var progress=Progress();AppendLog("检查并更新下载组件…");
            try{await Task.Run(()=>RuntimeSetup.Ensure(cancellation.Token,progress,true));status.Text="下载组件已就绪。";AppendLog(status.Text);}catch(OperationCanceledException){status.Text="组件更新已取消。";}catch(Exception ex){status.Text="组件更新失败，详见日志。";AppendLog(ex.Message);if(ex.InnerException!=null)AppendLog(ex.InnerException.Message);tabs.SelectedIndex=1;}finally{FinishOperation();}
        }
        async void ImportTools(object sender,EventArgs e){
            if(busy)return;string path;using(var dialog=new FolderBrowserDialog{Description="选择包含 yt-dlp.exe、ffmpeg.exe、ffprobe.exe 的文件夹"}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;path=dialog.SelectedPath;}
            SetBusy(true);cancel.Enabled=false;status.Text="正在复制已有组件…";
            try{await Task.Run(()=>RuntimeSetup.Import(path));status.Text="组件已导入，可以下载。";AppendLog(status.Text);}catch(Exception ex){ShowError(ex.Message);status.Text="组件导入失败。";AppendLog(ex.Message);}finally{FinishOperation();}
        }
        void FinishOperation(){bar.Style=ProgressBarStyle.Continuous;if(cancellation!=null){cancellation.Dispose();cancellation=null;}SetBusy(false);if(closeAfter)Close();}
        void ChooseFolder(object sender,EventArgs e){using(var dialog=new FolderBrowserDialog{Description="选择素材保存文件夹",SelectedPath=folder.Text}){if(dialog.ShowDialog(this)==DialogResult.OK){folder.Text=dialog.SelectedPath;SaveState();}}}
        void ChooseCookies(object sender,EventArgs e){using(var dialog=new OpenFileDialog{Title="选择自己导出的 B 站 / YouTube Netscape cookies.txt",Filter="Cookies 文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*"}){if(dialog.ShowDialog(this)==DialogResult.OK){try{Core.FilterCookies(dialog.FileName);cookiePath=dialog.FileName;cookieLabel.Text="已选择 · 仅本次打开有效";}catch(Exception ex){ShowError(ex.Message);}}}}
        void OpenFolder(string path){try{path=Path.GetFullPath(path);Directory.CreateDirectory(path);Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception ex){ShowError(ex.Message);}}
        void OpenSelected(){if(history.SelectedItems.Count==0)return;var job=(Job)history.SelectedItems[0].Tag;if(job.Path==""||!File.Exists(job.Path)){ShowError("该任务没有已保存的文件，或文件已被移动。可在任务详情中查看原因。");return;}try{if(Environment.OSVersion.Platform==PlatformID.Win32NT)Process.Start(new ProcessStartInfo("explorer.exe","/select,"+Core.Quote(job.Path)){UseShellExecute=false});else OpenFolder(Path.GetDirectoryName(job.Path));}catch(Exception ex){ShowError(ex.Message);}}
        void ExportLog(object sender,EventArgs e){using(var dialog=new SaveFileDialog{Title="导出当前日志",Filter="日志文本 (*.txt)|*.txt",FileName="视频下载日志_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".txt"}){if(dialog.ShowDialog(this)==DialogResult.OK)try{File.WriteAllText(dialog.FileName,logBox.Text,new UTF8Encoding(true));}catch(Exception ex){ShowError(ex.Message);}}}
        void ShowError(string message){MessageBox.Show(this,Core.Redact(message),"视频素材下载器",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        void ShowHelp(){MessageBox.Show(this,"1. 粘贴 B 站（BV 号 / 视频链接 / b23.tv 短链）或 YouTube 链接，选保存位置，点“开始下载”。\n2. 首次使用自动下载官方 yt-dlp 与 FFmpeg 组件，共约 130 MB，之后重复使用。若连接失败，可点“导入已有组件”。\n3. 默认输出 H.264 / AAC 的 MP4；必要时用 CPU 转码，不需要 CUDA。\n4. 清晰度受原片与账号权限限制。“最高画质”指当前能取得的最高画质。需要登录时可选择自己导出的 B 站或 YouTube cookies.txt；软件不会读取浏览器密码。\n5. B 站分 P 链接下载指定的一 P；不带 p 参数下载第一 P。暂不支持直播、番剧和空间批量下载。YouTube 只下载链接对应的单个视频，不下载整个播放列表。\n6. 下载 YouTube 需要网络能访问该站点；若提示要求验证身份，请先开启代理后重试。\n7. 下载记录和日志会保留。未完成的片段留在保存位置的 .BiliGreen-work 中，重试时尽量复用。", "使用说明",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        void HandleClosing(object sender,FormClosingEventArgs e){
            if(busy){e.Cancel=true;if(cancellation==null){MessageBox.Show(this,"正在复制组件，请完成后关闭。","请稍等");return;}if(!closeAfter&&MessageBox.Show(this,"当前任务尚未完成，停止任务并退出？","退出",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes){closeAfter=true;cancel.Enabled=false;status.Text="正在停止任务…";cancellation.Cancel();}return;}SaveState();
        }
        protected override void Dispose(bool disposing){if(disposing)tips.Dispose();base.Dispose(disposing);}
    }
    internal static class Program {
        [STAThread]static void Main(){
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);bool first;
            using(var mutex=new Mutex(true,Environment.OSVersion.Platform==PlatformID.Win32NT?@"Local\BiliGreenDownloader.Windows.v1":"BiliGreenDownloader.Windows.v1",out first)){
                if(!first){MessageBox.Show("下载器已经打开，请使用现有窗口。","视频素材下载器");return;}
                try{if(!Environment.Is64BitOperatingSystem)throw new InvalidOperationException("请在 Windows 10 / 11 的 64 位系统上使用。");Application.Run(new MainForm());}catch(Exception ex){MessageBox.Show("程序无法启动："+Core.Redact(ex.Message),"视频素材下载器");}
            }
        }
    }
}
