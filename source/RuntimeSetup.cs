using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace BiliGreenDownloader {
    internal static class RuntimeSetup {
        internal const string YtBase="https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download/";
        internal const string FfmpegZip="https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
        internal static string Root {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BiliGreenDownloader");}}
        internal static string Tools {get{return Path.Combine(Root,"tools");}}
        internal static ToolPaths Paths(string directory) {return new ToolPaths{YtDlp=Path.Combine(directory,"yt-dlp.exe"),Ffmpeg=Path.Combine(directory,"ffmpeg.exe"),Ffprobe=Path.Combine(directory,"ffprobe.exe")};}
        internal static bool IsPe64(string path) {
            try{using(var f=File.OpenRead(path))using(var r=new BinaryReader(f)){if(f.Length<512||r.ReadUInt16()!=0x5a4d)return false;f.Position=0x3c;int offset=r.ReadInt32();if(offset<0||offset>f.Length-6)return false;f.Position=offset;return r.ReadUInt32()==0x4550&&r.ReadUInt16()==0x8664;}}catch{return false;}
        }
        internal static ToolPaths Ensure(CancellationToken token,IProgress<Update> progress,bool update=false) {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;Directory.CreateDirectory(Tools);
            var paths=Paths(Tools);
            if(update||!IsPe64(paths.YtDlp)){
                progress.Report(new Update("首次准备 / 更新下载引擎：连接官方发布站点…"));
                string sums=GetText(YtBase+"SHA2-256SUMS",token);
                var m=Regex.Match(sums,@"(?m)^([a-fA-F0-9]{64})\s+\*?yt-dlp\.exe\s*$");
                if(!m.Success)throw new IOException("没有取得下载引擎的完整性校验信息，请稍后重试。");
                string expected=m.Groups[1].Value.ToLowerInvariant();string staged=Path.Combine(Tools,"yt-dlp-"+expected.Substring(0,12)+".new");
                Fetch(YtBase+"yt-dlp.exe",staged,expected,token,progress,"准备下载引擎");
                if(!IsPe64(staged))throw new IOException("下载引擎不是有效的 Windows 64 位程序。");
                if(File.Exists(paths.YtDlp))File.Replace(staged,paths.YtDlp,null);else File.Move(staged,paths.YtDlp);
            }
            if(!IsPe64(paths.Ffmpeg)||!IsPe64(paths.Ffprobe)){
                progress.Report(new Update("首次准备音视频组件（当前约 109 MB），以后无需重复下载…"));
                var m=Regex.Match(GetText(FfmpegZip+".sha256",token),@"(?i)\b[a-f0-9]{64}\b");
                if(!m.Success)throw new IOException("没有取得音视频组件校验信息，请稍后重试。");
                string zip=Path.Combine(Tools,"ffmpeg-"+m.Value.Substring(0,12)+".zip");
                Fetch(FfmpegZip,zip,m.Value.ToLowerInvariant(),token,progress,"准备音视频组件");
                string stage=Path.Combine(Tools,"unpack-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
                try{
                    ExtractTools(zip,stage,token);
                    if(!IsPe64(Path.Combine(stage,"ffmpeg.exe"))||!IsPe64(Path.Combine(stage,"ffprobe.exe")))throw new IOException("音视频组件解压不完整。");
                    token.ThrowIfCancellationRequested();
                    foreach(string name in new[]{"ffmpeg.exe","ffprobe.exe"}){string src=Path.Combine(stage,name),dst=Path.Combine(Tools,name);if(File.Exists(dst))File.Replace(src,dst,null);else File.Move(src,dst);}
                    File.Delete(zip);
                }finally{try{Directory.Delete(stage,true);}catch{}}
            }
            token.ThrowIfCancellationRequested();progress.Report(new Update("下载组件已就绪",100));return paths;
        }
        internal static void ExtractTools(string zip,string stage,CancellationToken token) {
            using(var z=ZipFile.OpenRead(zip))foreach(string name in new[]{"ffmpeg.exe","ffprobe.exe"}){
                token.ThrowIfCancellationRequested();var candidates=z.Entries.Where(e=>e.FullName.Replace('\\','/').EndsWith("/bin/"+name,StringComparison.OrdinalIgnoreCase)||e.FullName.Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();
                if(candidates.Length!=1||candidates[0].Length>400L*1024*1024)throw new IOException("组件压缩包缺少唯一的 "+name);
                // Ignore archive paths entirely: only two known basenames are written.
                using(var input=candidates[0].Open())using(var output=File.Create(Path.Combine(stage,name))){byte[] b=new byte[131072];int n;while((n=input.Read(b,0,b.Length))>0){token.ThrowIfCancellationRequested();output.Write(b,0,n);}}
            }
        }
        static HttpWebRequest Request(string url) {var r=(HttpWebRequest)WebRequest.Create(url);r.UserAgent="BiliGreenDownloader/1.0";r.Timeout=25000;r.ReadWriteTimeout=25000;r.AllowAutoRedirect=true;r.MaximumAutomaticRedirections=8;return r;}
        internal static string GetText(string url,CancellationToken token) {
            token.ThrowIfCancellationRequested();var r=Request(url);
            try{using(token.Register(r.Abort))using(var response=(HttpWebResponse)r.GetResponse())using(var input=response.GetResponseStream())using(var mem=new MemoryStream()){
                if(response.ResponseUri.Scheme!="https")throw new IOException("组件站点没有提供安全下载连接。");byte[] b=new byte[8192];int n;while((n=input.Read(b,0,b.Length))>0){token.ThrowIfCancellationRequested();mem.Write(b,0,n);if(mem.Length>2*1024*1024)throw new IOException("组件校验文件异常。");}return Encoding.UTF8.GetString(mem.ToArray());
            }}catch(WebException){token.ThrowIfCancellationRequested();throw;}
        }
        internal static void Fetch(string url,string destination,string hash,CancellationToken token,IProgress<Update> progress,string label) {
            if(File.Exists(destination)&&Core.Hash(destination)==hash)return;
            string partial=destination+".part";Exception last=null;
            for(int attempt=0;attempt<3;attempt++){
                token.ThrowIfCancellationRequested();long offset=File.Exists(partial)?new FileInfo(partial).Length:0;var request=Request(url);if(offset>0)request.AddRange(offset);
                try{
                    using(token.Register(request.Abort))using(var response=(HttpWebResponse)request.GetResponse()){
                        if(response.ResponseUri.Scheme!="https")throw new IOException("组件站点未提供 HTTPS 下载。");
                        bool append=response.StatusCode==HttpStatusCode.PartialContent&&offset>0;
                        if(append&&!String.Equals((response.Headers["Content-Range"]??"").Split(new[]{'-'},StringSplitOptions.None)[0],"bytes "+offset,StringComparison.OrdinalIgnoreCase))throw new IOException("下载站返回了错误的续传范围。");
                        if(!append)offset=0;
                        long total=response.ContentLength>0?offset+response.ContentLength:0,received=offset;if(total>500L*1024*1024)throw new IOException("组件文件大小异常。");
                        using(var input=response.GetResponseStream())using(var output=new FileStream(partial,append?FileMode.Append:FileMode.Create,FileAccess.Write,FileShare.Read)){
                            byte[] b=new byte[131072];int n;DateTime lastReport=DateTime.MinValue;
                            while((n=input.Read(b,0,b.Length))>0){token.ThrowIfCancellationRequested();output.Write(b,0,n);received+=n;if(received>500L*1024*1024)throw new IOException("组件文件过大。");if((DateTime.UtcNow-lastReport).TotalMilliseconds>200){progress.Report(new Update(label+"："+(received/1048576.0).ToString("0.0")+" MB",total>0?(int)Math.Min(99,100*received/total):-1));lastReport=DateTime.UtcNow;}}
                        }
                        if(total>0&&received!=total)throw new IOException("组件下载中断，将尝试续传。");
                    }
                    token.ThrowIfCancellationRequested();progress.Report(new Update(label+"：检查文件完整性…"));
                    if(Core.Hash(partial)!=hash){File.Delete(partial);throw new IOException("组件校验不一致，未运行下载到的文件。");}
                    if(File.Exists(destination))File.Delete(destination);File.Move(partial,destination);return;
                }catch(OperationCanceledException){throw;}
                catch(WebException ex){token.ThrowIfCancellationRequested();var response=ex.Response as HttpWebResponse;if(response!=null){if((int)response.StatusCode==416&&File.Exists(partial)){if(Core.Hash(partial)==hash){if(File.Exists(destination))File.Delete(destination);File.Move(partial,destination);response.Dispose();return;}File.Delete(partial);}response.Dispose();}last=ex;}
                catch(IOException ex){last=ex;}
                if(attempt<2&&token.WaitHandle.WaitOne(750))token.ThrowIfCancellationRequested();
            }
            throw new IOException("下载组件失败。请检查能否访问 GitHub 和 gyan.dev，再点击重试；也可使用“导入已有组件”。",last);
        }
        internal static ToolPaths Import(string folder) {
            string[] roots={folder,Path.Combine(folder,"bin"),Path.Combine(folder,"tools")};
            var found=new System.Collections.Generic.Dictionary<string,string>();
            foreach(string name in new[]{"yt-dlp.exe","ffmpeg.exe","ffprobe.exe"}){
                string path=roots.Select(d=>Path.Combine(d,name)).FirstOrDefault(IsPe64);
                if(path==null)throw new IOException("所选目录（或其 bin / tools 子目录）中没有 "+name+"，请把三个 64 位组件放到同一文件夹后再导入。");found[name]=path;
            }
            Directory.CreateDirectory(Tools);
            foreach(var item in found){string dst=Path.Combine(Tools,item.Key);if(Path.GetFullPath(dst).Equals(Path.GetFullPath(item.Value),StringComparison.OrdinalIgnoreCase))continue;string tmp=dst+".import";File.Copy(item.Value,tmp,true);if(File.Exists(dst))File.Replace(tmp,dst,null);else File.Move(tmp,dst);}
            return Paths(Tools);
        }
    }
}
