using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace BiliGreenDownloader {
    internal static class Downloader {
        internal static DownloadResult Download(DownloadOptions options,ToolPaths tools,CancellationToken token,IProgress<Update> progress,Action<string> log) {
            token.ThrowIfCancellationRequested();string url=Core.ResolveShort(Core.NormalizeUrl(options.Url),token);
            string outputFolder=Path.GetFullPath(options.Folder);Directory.CreateDirectory(outputFolder);
            string cookies=Core.FilterCookies(options.Cookies);
            string workRoot=Path.Combine(outputFolder,".BiliGreen-work");Directory.CreateDirectory(workRoot);
            if((File.GetAttributes(workRoot)&FileAttributes.ReparsePoint)!=0)throw new IOException("临时工作目录不能是目录链接，请选择另一个输出文件夹。");
            if(Environment.OSVersion.Platform==PlatformID.Win32NT)File.SetAttributes(workRoot,File.GetAttributes(workRoot)|FileAttributes.Hidden);
            string stage=Path.Combine(workRoot,Core.TextHash(url+"\n"+options.Limit1080+"\n"+Core.TextHash(cookies)).Substring(0,20));Directory.CreateDirectory(stage);
            if((File.GetAttributes(stage)&FileAttributes.ReparsePoint)!=0)throw new IOException("临时目录不是普通文件夹。");
            string marker=Path.Combine(stage,"BiliGreen.work");
            if(Directory.GetFileSystemEntries(stage).Length>0&&!File.Exists(marker))throw new IOException("临时目录中存在未知文件，请更换输出目录。");
            File.WriteAllText(marker,"BiliGreenDownloader/1",Encoding.UTF8);
            string cookiePath=String.IsNullOrEmpty(cookies)?"":Path.Combine(stage,"cookies.txt");
            bool completed=false;
            try {
                if(cookiePath!="")File.WriteAllText(cookiePath,cookies,new UTF8Encoding(false));
                progress.Report(new Update("解析视频并下载音视频…"));bool previewOnly=false;object eventGate=new object();
                var raw=Runner.Run(tools.YtDlp,Core.DownloadArgs(url,stage,cookiePath,tools,options.Limit1080),stage,token,(line,error)=>{
                    if(line.StartsWith("__BILI_RESULT__"))return;
                    lock(eventGate){
                        if(line.IndexOf("only the preview",StringComparison.OrdinalIgnoreCase)>=0||line.Contains("试看"))previewOnly=true;
                        var m=Regex.Match(line,@"\[download\]\s+(\d+(?:\.\d+)?)%");double percent;
                        if(m.Success&&Double.TryParse(m.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out percent))progress.Report(new Update("下载音视频："+percent.ToString("0.0",CultureInfo.InvariantCulture)+"%",Math.Min(99,(int)percent)));
                        else if(line.Contains("[Merger]")||line.Contains("[Fixup"))progress.Report(new Update("合并音视频轨道…"));
                        if(!m.Success)log(Core.Redact(line));
                    }
                });
                token.ThrowIfCancellationRequested();
                if(raw.ExitCode!=0)throw new InvalidOperationException(Core.FailureHint(raw.Error+raw.Output));
                if(previewOnly)throw new InvalidOperationException("站点只提供了试看片段，未将它标记为完整素材。请确认账号具有完整观看权限。");
                var lines=raw.Output.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Where(x=>x.StartsWith("__BILI_RESULT__")).ToArray();
                if(lines.Length!=1)throw new InvalidOperationException("没有取得唯一的已下载视频文件。暂时请使用普通投稿视频或明确的分 P 链接。");
                var result=Json.Object(lines[0].Substring("__BILI_RESULT__".Length));string source=Json.Get(result,"path"),title=Json.Get(result,"title","视频素材");
                if(!Path.IsPathRooted(source))source=Path.Combine(stage,source);
                if(!Core.Inside(source,stage)||!File.Exists(source)||new FileInfo(source).Length==0)throw new IOException("下载引擎没有生成完整文件，未记录为成功。");
                string extension=Path.GetExtension(source).ToLowerInvariant();
                if(!new[]{".mp4",".mkv",".webm",".flv",".mov",".m4v",".avi",".ts"}.Contains(extension))throw new InvalidOperationException("下载结果不是支持的视频文件。");
                progress.Report(new Update("检查下载结果和编码…"));var info=MediaInfo.Probe(tools.Ffprobe,source,token);
                log("实际素材："+info.Width+" × "+info.Height+"，视频 "+info.Video+"，"+(info.HasAudio?"音频 "+info.Audio:"无音轨"));
                if(info.Height<720&&String.IsNullOrEmpty(cookies))log("目前取得的清晰度较低；可能是原片或站点权限所限。需要时可导入 cookies 登录文件再试。");
                string finished=source;
                if(options.Compatible){
                    extension=".mp4";finished=Path.Combine(stage,"PR-ready.partial.mp4");
                    progress.Report(new Update(info.CanCopy?"封装为 MP4（保留原编码）…":"转换为 PR 兼容 MP4…"));
                    Action<string,bool> report=(line,error)=>{
                        if(error){log(Core.Redact(line));return;}
                        if(line.StartsWith("out_time_us=")){double us;if(info.Duration>0&&Double.TryParse(line.Substring(12),NumberStyles.Float,CultureInfo.InvariantCulture,out us))progress.Report(new Update("处理 MP4："+(us/1000000.0).ToString("0.0")+" / "+info.Duration.ToString("0.0")+" 秒",(int)Math.Max(0,Math.Min(99,us/1000000.0/info.Duration*100))));}
                    };
                    var convert=Runner.Run(tools.Ffmpeg,MediaInfo.ConvertArgs(source,finished,info),stage,token,report);
                    if(convert.ExitCode!=0&&info.CanCopy){log("直接封装没有完成，尝试兼容转码。 ");convert=Runner.Run(tools.Ffmpeg,MediaInfo.ConvertArgs(source,finished,info,true),stage,token,report);}
                    if(convert.ExitCode!=0||!File.Exists(finished))throw new InvalidOperationException("MP4 处理失败，原始下载内容已保留。可取消“PR 兼容 MP4”后重试，保存原始编码文件。 ");
                    progress.Report(new Update("验证最终 MP4…"));var check=MediaInfo.Probe(tools.Ffprobe,finished,token);
                    if(check.Video!="h264"||check.Pixel!="yuv420p"||(info.HasAudio&&check.Audio!="aac")||(info.Duration>1&&check.Duration<info.Duration-Math.Max(1,info.Duration*.02)))throw new InvalidOperationException("最终 MP4 未通过编码或时长检查，未标记为成功。");
                }
                token.ThrowIfCancellationRequested();string name=Core.SafeName(title)+(options.Compatible?"_PR":"_原始")+extension;
                string destination=Path.Combine(outputFolder,name);int suffix=2;
                while(File.Exists(destination)){destination=Path.Combine(outputFolder,Path.GetFileNameWithoutExtension(name)+"_"+(suffix++)+extension);}
                File.Move(finished,destination);completed=true;
                progress.Report(new Update("下载完成",100));
                return new DownloadResult{Path=destination,Title=title,Description=info.Width+" × "+info.Height+(options.Compatible?" · MP4":" · 原始编码")};
            } finally {
                if(cookiePath!=""){try{File.Delete(cookiePath);}catch{log("临时登录文件未能清理，请关闭软件后删除输出目录中的 .BiliGreen-work 临时文件夹。");}}
                if(completed){try{if(File.ReadAllText(marker,Encoding.UTF8)=="BiliGreenDownloader/1")Directory.Delete(stage,true);if(!Directory.EnumerateFileSystemEntries(workRoot).Any())Directory.Delete(workRoot);}catch{log("素材已保存；少量临时文件未能清理。");}}
            }
        }
    }
}
