using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BiliGreenDownloader {
    internal sealed class Update { public string Message;public int Percent=-1; public Update(string message,int percent=-1){Message=message;Percent=percent;} }
    internal sealed class ToolPaths {public string YtDlp,Ffmpeg,Ffprobe;}
    internal sealed class DownloadOptions {public string Url,Folder,Cookies="";public bool Limit1080,Compatible=true;}
    internal sealed class DownloadResult {public string Path,Title,Description;}
    internal static class Core {
        internal const string Version="1.1.1";
        internal static readonly string[] BilibiliHosts=new[]{"www.bilibili.com","bilibili.com","m.bilibili.com"};
        internal static readonly string[] YouTubeHosts=new[]{"www.youtube.com","youtube.com","m.youtube.com","music.youtube.com","youtu.be","www.youtu.be"};
        internal static bool IsYouTubeHost(string host){return YouTubeHosts.Contains((host??"").ToLowerInvariant());}
        internal static string NormalizeUrl(string input) {
            input=(input??"").Trim();
            if(Regex.IsMatch(input,@"^BV[0-9A-Za-z]{10}$"))return "https://www.bilibili.com/video/"+input;
            if(Regex.IsMatch(input,@"^av[0-9]+$",RegexOptions.IgnoreCase))return "https://www.bilibili.com/video/"+input.ToLowerInvariant();
            var found=Regex.Match(input,@"https?://[^\s<>""，。；）】]+",RegexOptions.IgnoreCase);
            if(found.Success)input=found.Value;
            if(input.StartsWith("www.bilibili.com/",StringComparison.OrdinalIgnoreCase)||input.StartsWith("b23.tv/",StringComparison.OrdinalIgnoreCase)
               ||input.StartsWith("www.youtube.com/",StringComparison.OrdinalIgnoreCase)||input.StartsWith("youtube.com/",StringComparison.OrdinalIgnoreCase)
               ||input.StartsWith("m.youtube.com/",StringComparison.OrdinalIgnoreCase)||input.StartsWith("youtu.be/",StringComparison.OrdinalIgnoreCase))input="https://"+input;
            Uri uri;if(!Uri.TryCreate(input,UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https")||uri.UserInfo.Length!=0||!uri.IsDefaultPort)throw new InvalidOperationException("请粘贴 B 站视频链接 / BV 号 / b23.tv 短链，或 YouTube 视频链接。");
            string host=uri.Host.ToLowerInvariant();
            if(YouTubeHosts.Contains(host))return NormalizeYouTube(uri);
            if(uri.Host.Equals("b23.tv",StringComparison.OrdinalIgnoreCase)&&Regex.IsMatch(uri.AbsolutePath,@"^/[A-Za-z0-9]+/?$"))return "https://b23.tv"+uri.AbsolutePath.TrimEnd(new[]{'/'});
            if(!BilibiliHosts.Contains(host))throw new InvalidOperationException("这个工具只接收 B 站或 YouTube 视频链接。");
            var id=Regex.Match(uri.AbsolutePath,@"^/video/(BV[0-9A-Za-z]{10}|av[0-9]+)/?$",RegexOptions.IgnoreCase);
            if(!id.Success)throw new InvalidOperationException("请打开具体投稿视频，复制地址栏链接；暂不支持个人空间、直播或番剧页面。");
            string url="https://www.bilibili.com/video/"+id.Groups[1].Value;
            var page=Regex.Match(uri.Query,@"(?:^|[?&])p=(\d+)(?:&|$)");
            if(page.Success){int n;if(!Int32.TryParse(page.Groups[1].Value,out n)||n<1)throw new InvalidOperationException("分 P 序号无效。");url+="?p="+n;}
            return url;
        }
        internal static string NormalizeYouTube(Uri uri) {
            string host=uri.Host.ToLowerInvariant();string id=null;
            if(host=="youtu.be"||host=="www.youtu.be"){
                var shortMatch=Regex.Match(uri.AbsolutePath,@"^/([A-Za-z0-9_-]{11})(?:/|$)");
                if(shortMatch.Success)id=shortMatch.Groups[1].Value;
            }else{
                var watch=Regex.Match(uri.Query,@"(?:^|[?&])v=([A-Za-z0-9_-]{11})(?:&|$)");
                if(watch.Success)id=watch.Groups[1].Value;
                else{var path=Regex.Match(uri.AbsolutePath,@"^/(?:shorts|embed|live|v)/([A-Za-z0-9_-]{11})(?:/|$)");if(path.Success)id=path.Groups[1].Value;}
            }
            if(String.IsNullOrEmpty(id))throw new InvalidOperationException("没有识别出 YouTube 视频编号。请复制单个视频的链接（watch?v=… 或 youtu.be/…），不要使用频道、播放列表或首页链接。");
            return "https://www.youtube.com/watch?v="+id;
        }
        internal static string ResolveShort(string url,CancellationToken token) {
            if(new Uri(url).Host!="b23.tv")return url;
            for(int i=0;i<5;i++){
                token.ThrowIfCancellationRequested();var request=(HttpWebRequest)WebRequest.Create(url);request.AllowAutoRedirect=false;request.UserAgent="Mozilla/5.0 BiliGreenDownloader/1.0";request.Timeout=20000;request.ReadWriteTimeout=20000;
                using(token.Register(request.Abort))using(var response=(HttpWebResponse)request.GetResponse()){
                    int status=(int)response.StatusCode;string next=response.Headers["Location"];
                    if(status>=300&&status<400&&!String.IsNullOrEmpty(next)){url=NormalizeUrl(new Uri(new Uri(url),next).AbsoluteUri);if(new Uri(url).Host!="b23.tv")return url;continue;}
                    throw new InvalidOperationException("分享短链没有返回投稿视频地址，请打开视频后复制完整的 bilibili.com/video 链接。");
                }
            }throw new InvalidOperationException("分享短链跳转过多，请使用完整视频链接。");
        }
        internal static string Quote(string value) {
            var b=new StringBuilder("\"");int slashes=0;
            foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"'){b.Append('\\',slashes*2+1);b.Append('"');slashes=0;}else{b.Append('\\',slashes);slashes=0;b.Append(c);}}
            b.Append('\\',slashes*2);return b.Append('"').ToString();
        }
        internal static string Arguments(IEnumerable<string> values){return String.Join(" ",values.Select(Quote));}
        internal static string Hash(string path){using(var h=SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
        internal static string TextHash(string text){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
        internal static string SafeName(string title) {
            string name=Regex.Replace(title??"视频素材",@"[<>:""/\\|?*\x00-\x1f]","_").Trim().TrimEnd('.',' ');
            if(name.Length>90)name=name.Substring(0,90).TrimEnd();if(name.Length>0&&Char.IsHighSurrogate(name[name.Length-1]))name=name.Substring(0,name.Length-1);
            if(String.IsNullOrWhiteSpace(name))name="视频素材";if(Regex.IsMatch(name,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",RegexOptions.IgnoreCase))name="_"+name;return name;
        }
        internal static string Redact(string text) {
            text=Regex.Replace(text??"",@"\x1B\[[0-?]*[ -/]*[@-~]","");
            text=Regex.Replace(text,@"(?i)(SESSDATA|bili_jct|DedeUserID|Cookie|Authorization)\s*[:=]\s*[^\s;]+","$1=[已隐藏]");
            text=Regex.Replace(text,@"https?://[^\s]+","[链接]");return text.Length>1200?text.Substring(0,1200):text;
        }
        internal static string FilterCookies(string path) {
            if(String.IsNullOrWhiteSpace(path))return "";
            if(new FileInfo(path).Length>4*1024*1024)throw new InvalidOperationException("登录文件过大，请只导出 B 站或 YouTube 站点的 cookies.txt。");
            var rows=new List<string>{"# Netscape HTTP Cookie File"};
            foreach(string raw in File.ReadAllLines(path,Encoding.UTF8)){
                string line=raw.TrimStart(new[]{'\uFEFF'});if(line.StartsWith("#")&&!line.StartsWith("#HttpOnly_"))continue;
                var fields=line.Split(new[]{'\t'},StringSplitOptions.None);if(fields.Length!=7)continue;string domain=fields[0].Replace("#HttpOnly_","").TrimStart(new[]{'.'}).ToLowerInvariant();
                if(IsBilibiliDomain(domain)||IsYouTubeDomain(domain))rows.Add(line);
            }
            if(rows.Count==1)throw new InvalidOperationException("没有读到 B 站或 YouTube 登录信息。需要 Netscape 格式的 cookies.txt 文件。");
            return String.Join("\n",rows)+"\n";
        }
        internal static bool IsBilibiliDomain(string domain){return domain=="bilibili.com"||domain.EndsWith(".bilibili.com",StringComparison.Ordinal);}
        internal static bool IsYouTubeDomain(string domain){return domain=="youtube.com"||domain.EndsWith(".youtube.com",StringComparison.Ordinal)||domain=="google.com"||domain.EndsWith(".google.com",StringComparison.Ordinal)||domain=="youtu.be"||domain.EndsWith(".youtu.be",StringComparison.Ordinal);}
        internal static List<string> DownloadArgs(string url,string stage,string cookie,ToolPaths tools,bool limit1080) {
            var a=new List<string>{"--ignore-config","--no-plugin-dirs","--no-playlist","--playlist-items","1","--no-simulate","--newline","--progress","--no-colors","--encoding","utf-8","--windows-filenames","--continue","--retries","3","--fragment-retries","3","--abort-on-unavailable-fragments","--socket-timeout","25","--concurrent-fragments","2","--ffmpeg-location",Path.GetDirectoryName(tools.Ffmpeg),"-f","bv*+ba/b/bv*","-S",limit1080?"res:1080,vcodec:h264,acodec:aac":"res,vcodec:h264,acodec:aac","--merge-output-format","mkv","-P",stage,"-o","source.%(ext)s","--print","after_move:__BILI_RESULT__{\"path\":%(filepath)j,\"title\":%(title)j,\"id\":%(id)j}"};
            if(!String.IsNullOrEmpty(cookie))a.AddRange(new[]{"--cookies",cookie});a.Add("--");a.Add(url);return a;
        }
        internal static void AtomicText(string path,string value) {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));string tmp=path+".writing-"+Guid.NewGuid().ToString("N");
            try{File.WriteAllText(tmp,value,new UTF8Encoding(false));if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}finally{if(File.Exists(tmp))File.Delete(tmp);}
        }
        internal static bool Inside(string file,string directory){string root=Path.GetFullPath(directory).TrimEnd(new[]{Path.DirectorySeparatorChar})+Path.DirectorySeparatorChar;return Path.GetFullPath(file).StartsWith(root,Environment.OSVersion.Platform==PlatformID.Win32NT?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);}
        internal static string FailureHint(string text) {
            string s=(text??"").ToLowerInvariant();
            // 分类判断保持不变，但每条都把「引擎实际说了什么」带上 ——
            // 只说"下载没有完成"，使用者没法提供线索，开发者也定位不了。
            string what;string[] steps;
            if(s.Contains("412")||s.Contains("429")){
                what="站点暂时限制了请求（返回 412 / 429）。";
                steps=new[]{"等几分钟再试 —— 短时间内反复重试会被限制得更久；","导入自己的 cookies 登录文件后重试，成功率通常更高；","换一个网络（比如切到手机热点）再试。"};
            }else if(s.Contains("not a bot")||s.Contains("sign in to confirm")||s.Contains("confirm you")){
                what="YouTube 要求验证身份（通常是把当前网络判定成了数据中心）。";
                steps=new[]{"开启代理后再重试；","或导入自己的 YouTube 登录文件（cookies）；","确认系统时间准确 —— 偏差过大会被要求验证。"};
            }else if(s.Contains("login")||s.Contains("cookies")||s.Contains("premium")||s.Contains("403")){
                what="这个视频或这个清晰度需要登录权限，或者请求被站点拒绝了。";
                steps=new[]{"先在浏览器里确认这个视频当前能正常播放；","点「导入 cookies」选自己导出的登录文件后重试；","只需要较低清晰度时，把「限制 1080p」关掉再试。"};
            }else if(s.Contains("404")||s.Contains("deleted")||s.Contains("not found")){
                what="视频不存在、已删除，或者当前账号访问不到。";
                steps=new[]{"把同一个链接粘到浏览器里，确认能正常播放；","确认链接没有多复制或少复制字符；","会员限定或已删除的内容无法下载。"};
            }else if(s.Contains("timed out")||s.Contains("resolve")||s.Contains("connection")){
                what="连接站点失败或者超时了。";
                steps=new[]{"检查网络；需要代理的站点请先开启代理；","稍后重试 —— 已经下好的片段会尽量复用，不会从头再来。"};
            }else{
                what="下载没有完成。";
                steps=new[]{"先点「更新下载组件」把 yt-dlp 更新到最新，再重试；","换一个输出目录再试；","下面这几行引擎输出是定位问题的关键，别删掉。"};
            }
            return Diag.Diagnose(what,new[]{"引擎最后几行："+Diag.Tail(text,8)},steps);
        }
    }
    internal sealed class ProcessResult {public int ExitCode;public string Output,Error;}
    internal static class Runner {
        internal static ProcessResult Run(string exe,IEnumerable<string> args,string cwd,CancellationToken token,Action<string,bool> line=null) {
            token.ThrowIfCancellationRequested();var output=new StringBuilder();var errors=new StringBuilder();object gate=new object();
            var psi=new ProcessStartInfo(exe,Core.Arguments(args)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,WorkingDirectory=cwd,StandardOutputEncoding=new UTF8Encoding(false),StandardErrorEncoding=new UTF8Encoding(false)};
            psi.EnvironmentVariables["PYTHONUTF8"]="1";psi.EnvironmentVariables["PYTHONIOENCODING"]="utf-8";
            using(var p=new Process{StartInfo=psi}){
                DataReceivedEventHandler read=(sender,e)=>{if(e.Data==null)return;lock(gate){if(output.Length<8*1024*1024)output.AppendLine(e.Data);}if(line!=null){try{line(e.Data,false);}catch{}}};
                DataReceivedEventHandler err=(sender,e)=>{if(e.Data==null)return;lock(gate){if(errors.Length>50000)errors.Remove(0,25000);errors.AppendLine(e.Data);}if(line!=null){try{line(e.Data,true);}catch{}}};
                p.OutputDataReceived+=read;p.ErrorDataReceived+=err;p.Start();p.StandardInput.Close();p.BeginOutputReadLine();p.BeginErrorReadLine();
                using(token.Register(()=>KillTree(p))){p.WaitForExit();}token.ThrowIfCancellationRequested();
                return new ProcessResult{ExitCode=p.ExitCode,Output=output.ToString(),Error=errors.ToString()};
            }
        }
        static void KillTree(Process p){
            try{if(p.HasExited)return;if(Environment.OSVersion.Platform==PlatformID.Win32NT){string taskkill=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"taskkill.exe");using(var killer=Process.Start(new ProcessStartInfo(taskkill,"/PID "+p.Id+" /T /F"){UseShellExecute=false,CreateNoWindow=true})){if(killer!=null)killer.WaitForExit(5000);}}if(!p.HasExited)p.Kill();}catch{}
        }
    }
    internal sealed class MediaInfo {
        public string Video="",Audio="",Pixel="",Transfer="",Rate="",Average="";public int Width,Height;public double Duration;
        public bool HasAudio{get{return Audio!="";}}public bool Hdr{get{return Transfer=="smpte2084"||Transfer=="arib-std-b67";}}
        public bool CanCopy {get{return Video=="h264"&&Pixel=="yuv420p"&&!Hdr&&(!HasAudio||Audio=="aac")&&(String.IsNullOrEmpty(Rate)||String.IsNullOrEmpty(Average)||Rate==Average);}}
        internal static MediaInfo Parse(string json){
            var root=Json.Object(json);var result=new MediaInfo();object value;
            if(root.TryGetValue("format",out value)){var format=value as Dictionary<string,object>;Double.TryParse(Json.Get(format,"duration"),NumberStyles.Float,CultureInfo.InvariantCulture,out result.Duration);}
            if(root.TryGetValue("streams",out value))foreach(var row in (List<object>)value){var stream=row as Dictionary<string,object>;string type=Json.Get(stream,"codec_type");if(type=="video"&&result.Video==""){result.Video=Json.Get(stream,"codec_name");result.Pixel=Json.Get(stream,"pix_fmt");result.Transfer=Json.Get(stream,"color_transfer");result.Rate=Json.Get(stream,"r_frame_rate");result.Average=Json.Get(stream,"avg_frame_rate");Int32.TryParse(Json.Get(stream,"width"),out result.Width);Int32.TryParse(Json.Get(stream,"height"),out result.Height);}else if(type=="audio"&&result.Audio=="")result.Audio=Json.Get(stream,"codec_name");}
            if(result.Video=="")throw new InvalidOperationException("文件中没有可读取的视频轨道。");return result;
        }
        internal static MediaInfo Probe(string exe,string file,CancellationToken token) {
            var r=Runner.Run(exe,new[]{"-v","error","-show_streams","-show_format","-of","json",file},Path.GetDirectoryName(file),token);
            if(r.ExitCode!=0)throw new InvalidOperationException("无法确认视频完整性，未将它标记为成功。 "+Core.Redact(r.Error));return Parse(r.Output);
        }
        internal static List<string> ConvertArgs(string input,string output,MediaInfo info,bool forceTranscode=false) {
            var args=new List<string>{"-hide_banner","-nostdin","-y","-threads","2","-filter_threads","2","-i",input,"-map","0:v:0","-map","0:a:0?","-sn","-dn"};
            if(info.CanCopy&&!forceTranscode)args.AddRange(new[]{"-c:v","copy","-c:a","copy"});
            else{
                string filter="pad=ceil(iw/2)*2:ceil(ih/2)*2,format=yuv420p";
                if(info.Hdr)filter="zscale=t=linear:npl=100,format=gbrpf32le,tonemap=tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=tv,"+filter;
                args.AddRange(new[]{"-vf",filter,"-c:v","libx264","-preset","fast","-crf","18","-threads","4","-pix_fmt","yuv420p","-fps_mode","cfr","-c:a","aac","-b:a","192k","-ar","48000"});
                if(info.Hdr)args.AddRange(new[]{"-color_primaries","bt709","-color_trc","bt709","-colorspace","bt709"});
            }
            args.AddRange(new[]{"-movflags","+faststart","-progress","pipe:1","-nostats",output});return args;
        }
    }
}
