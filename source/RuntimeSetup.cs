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
        internal const string Proxy="https://gh-proxy.com/";
        internal const string YtBase="https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download/";
        internal const string FfmpegZip="https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
        internal const string FfmpegBuildZip="https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        internal const string FfmpegBuildChecksums="https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/checksums.sha256";
        internal static string[] MirrorCandidates(string url){return new[]{Proxy+url,url};}
        // 校验值不能走和"被校验文件"同一个通道：否则镜像可以同时替换校验值与文件，SHA-256 形同虚设。
        // 所以校验文件先直连官方源，只有直连失败才降级镜像，并在界面上明说（2026-10-24）。
        internal static string[] ChecksumCandidates(string url){return new[]{url,Proxy+url};}
        internal static string YtDlpHashFrom(string sums){var m=Regex.Match(sums??"",@"(?m)^([a-fA-F0-9]{64})\s+\*?yt-dlp\.exe\s*$");return m.Success?m.Groups[1].Value.ToLowerInvariant():null;}
        internal static string FfmpegBuildHashFrom(string sums){var m=Regex.Match(sums??"",@"(?im)^([a-f0-9]{64})\s+\*?ffmpeg-master-latest-win64-gpl\.zip\s*$");return m.Success?m.Groups[1].Value.ToLowerInvariant():null;}
        internal static string Root {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BiliGreenDownloader");}}
        internal static string Tools {get{return Path.Combine(Root,"tools");}}
        internal static ToolPaths Paths(string directory) {return new ToolPaths{YtDlp=Path.Combine(directory,"yt-dlp.exe"),Ffmpeg=Path.Combine(directory,"ffmpeg.exe"),Ffprobe=Path.Combine(directory,"ffprobe.exe")};}
        internal static bool IsPe64(string path) {
            try{using(var f=File.OpenRead(path))using(var r=new BinaryReader(f)){if(f.Length<512||r.ReadUInt16()!=0x5a4d)return false;f.Position=0x3c;int offset=r.ReadInt32();if(offset<0||offset>f.Length-6)return false;f.Position=offset;return r.ReadUInt32()==0x4550&&r.ReadUInt16()==0x8664;}}catch{return false;}
        }
        internal static ToolPaths Ensure(CancellationToken token,IProgress<Update> progress,bool update=false,Action<string> log=null) {
            // note：同时更新状态栏与日志，让"校验值降级走镜像"这类提醒不会一闪而过。
            Action<string> note=m=>{try{progress.Report(new Update(m));}catch{}if(log!=null)try{log(m);}catch{}};
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;Directory.CreateDirectory(Tools);
            var paths=Paths(Tools);
            if(update||!IsPe64(paths.YtDlp)){
                progress.Report(new Update("首次准备 / 更新下载引擎：连接官方发布站点…"));
                string sums=GetChecksumText(YtBase+"SHA2-256SUMS",token,note);
                string expected=YtDlpHashFrom(sums);
                if(expected==null)throw new IOException("没有取得下载引擎的完整性校验信息，请稍后重试。");
                string staged=Path.Combine(Tools,"yt-dlp-"+expected.Substring(0,12)+".new");
                FetchAny(YtBase+"yt-dlp.exe",staged,expected,token,progress,"准备下载引擎");
                if(!IsPe64(staged))throw new IOException("下载引擎不是有效的 Windows 64 位程序。");
                if(File.Exists(paths.YtDlp))File.Replace(staged,paths.YtDlp,null);else File.Move(staged,paths.YtDlp);
            }
            if(!IsPe64(paths.Ffmpeg)||!IsPe64(paths.Ffprobe)){
                progress.Report(new Update("首次准备音视频组件（约 190 MB，自动走国内加速线路），以后无需重复下载…"));
                string zip=FetchFfmpeg(token,progress,note);
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
        static string FetchFfmpeg(CancellationToken token,IProgress<Update> progress,Action<string> note) {
            Exception last=null;
            try{
                string hash=FfmpegBuildHashFrom(GetChecksumText(FfmpegBuildChecksums,token,note));
                if(hash!=null){
                    string z=Path.Combine(Tools,"ffmpeg-"+hash.Substring(0,12)+".zip");
                    FetchAny(FfmpegBuildZip,z,hash,token,progress,"准备音视频组件");
                    return z;
                }
            }catch(OperationCanceledException){throw;}
            catch(Exception ex){last=ex;progress.Report(new Update("加速线路不可用，改用官方源…"));}
            var g=Regex.Match(GetChecksumText(FfmpegZip+".sha256",token,note),@"(?i)\b[a-f0-9]{64}\b");
            if(!g.Success)throw new IOException("没有取得音视频组件校验信息，请稍后重试。",last);
            string expected=g.Value.ToLowerInvariant();string zip=Path.Combine(Tools,"ffmpeg-"+expected.Substring(0,12)+".zip");
            FetchAny(FfmpegZip,zip,expected,token,progress,"准备音视频组件");
            return zip;
        }
        internal static void ExtractTools(string zip,string stage,CancellationToken token) {
            using(var z=ZipFile.OpenRead(zip))foreach(string name in new[]{"ffmpeg.exe","ffprobe.exe"}){
                token.ThrowIfCancellationRequested();var candidates=z.Entries.Where(e=>e.FullName.Replace('\\','/').EndsWith("/bin/"+name,StringComparison.OrdinalIgnoreCase)||e.FullName.Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();
                if(candidates.Length!=1||candidates[0].Length>400L*1024*1024)throw new IOException("组件压缩包缺少唯一的 "+name);
                // Ignore archive paths entirely: only two known basenames are written.
                using(var input=candidates[0].Open())using(var output=File.Create(Path.Combine(stage,name))){byte[] b=new byte[131072];int n;while((n=input.Read(b,0,b.Length))>0){token.ThrowIfCancellationRequested();output.Write(b,0,n);}}
            }
        }
        internal static string GetTextAny(string url,CancellationToken token) {
            Exception last=null;
            foreach(string candidate in MirrorCandidates(url)){
                try{return GetText(candidate,token);}
                catch(OperationCanceledException){throw;}
                catch(Exception ex){last=ex;}
            }
            throw new IOException("组件校验信息下载失败，请检查网络后重试。",last);
        }
        // 校验文件专用：官方直连优先；只有直连失败才走镜像，并且明确提示"安全性降低"。
        internal static string GetChecksumText(string url,CancellationToken token,Action<string> note) {
            Exception last=null;
            foreach(string candidate in ChecksumCandidates(url)){
                try{
                    // 直连官方源给较短超时：国内直连 GitHub 常常挂死，不能让"多一次校验请求"变成几十秒等待。
                    string text=GetText(candidate,token,candidate==url?12000:25000);
                    if(candidate!=url&&note!=null)note("提示：这次没能直连官方源，组件校验值来自加速镜像（安全性低于官方直连）。");
                    return text;
                }
                catch(OperationCanceledException){throw;}
                catch(Exception ex){last=ex;}
            }
            throw new IOException("组件校验信息下载失败，请检查网络后重试。",last);
        }
        internal static void FetchAny(string url,string destination,string hash,CancellationToken token,IProgress<Update> progress,string label) {
            Exception last=null;
            foreach(string candidate in MirrorCandidates(url)){
                try{Fetch(candidate,destination,hash,token,progress,label);return;}
                catch(OperationCanceledException){throw;}
                catch(IOException ex){last=ex;}
            }
            throw new IOException("下载组件失败。请检查能否访问 GitHub / BtbN / gyan.dev（国内网络会自动走加速线路，已下载部分会自动续传），再点击重试；也可使用“导入已有组件”。",last);
        }
        static HttpWebRequest Request(string url,int timeoutMs=25000) {var r=(HttpWebRequest)WebRequest.Create(url);r.UserAgent="BiliGreenDownloader/1.0";r.Timeout=timeoutMs;r.ReadWriteTimeout=timeoutMs;r.AllowAutoRedirect=true;r.MaximumAutomaticRedirections=8;return r;}
        internal static string GetText(string url,CancellationToken token,int timeoutMs=25000) {
            token.ThrowIfCancellationRequested();var r=Request(url,timeoutMs);
            try{using(token.Register(r.Abort))using(var response=(HttpWebResponse)r.GetResponse())using(var input=response.GetResponseStream())using(var mem=new MemoryStream()){
                if(response.ResponseUri.Scheme!="https")throw new IOException("组件站点没有提供安全下载连接。");byte[] b=new byte[8192];int n;while((n=input.Read(b,0,b.Length))>0){token.ThrowIfCancellationRequested();mem.Write(b,0,n);if(mem.Length>2*1024*1024)throw new IOException("组件校验文件异常。");}return Encoding.UTF8.GetString(mem.ToArray());
            }}catch(WebException){token.ThrowIfCancellationRequested();throw;}
        }
        internal static void Fetch(string url,string destination,string hash,CancellationToken token,IProgress<Update> progress,string label) {
            if(File.Exists(destination)&&Core.Hash(destination)==hash)return;
            string partial=destination+".part";Exception last=null;
            for(int attempt=0;attempt<8;attempt++){
                token.ThrowIfCancellationRequested();long offset=File.Exists(partial)?new FileInfo(partial).Length:0;var request=Request(url);if(offset>0)request.AddRange(offset);
                try{
                    using(token.Register(request.Abort))using(var response=(HttpWebResponse)request.GetResponse()){
                        if(response.ResponseUri.Scheme!="https")throw new IOException("组件站点未提供 HTTPS 下载。");
                        bool append=response.StatusCode==HttpStatusCode.PartialContent&&offset>0;
                        if(append&&!String.Equals((response.Headers["Content-Range"]??"").Split(new[]{'-'},StringSplitOptions.None)[0],"bytes "+offset,StringComparison.OrdinalIgnoreCase))throw new IOException("下载站返回了错误的续传范围。");
                        if(!append)offset=0;
                        long total=response.ContentLength>0?offset+response.ContentLength:0,received=offset;if(total>500L*1024*1024)throw new IOException("组件文件大小异常。");
                        using(var input=response.GetResponseStream())using(var output=new FileStream(partial,append?FileMode.Append:FileMode.Create,FileAccess.Write,FileShare.Read)){
                            byte[] b=new byte[131072];int n;DateTime lastReport=DateTime.MinValue,lastData=DateTime.UtcNow;
                            using(var watchdog=new Timer(_=>{try{if((DateTime.UtcNow-lastData).TotalSeconds>30)request.Abort();}catch{}},null,3000,3000)){
                                while((n=input.Read(b,0,b.Length))>0){token.ThrowIfCancellationRequested();output.Write(b,0,n);received+=n;lastData=DateTime.UtcNow;if(received>500L*1024*1024)throw new IOException("组件文件过大。");if((DateTime.UtcNow-lastReport).TotalMilliseconds>200){progress.Report(new Update(label+"："+(received/1048576.0).ToString("0.0")+" MB",total>0?(int)Math.Min(99,100*received/total):-1));lastReport=DateTime.UtcNow;}}
                            }
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
            throw new IOException("下载组件失败。请检查能否访问 GitHub / BtbN / gyan.dev，再点击重试；也可使用“导入已有组件”。",last);
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
