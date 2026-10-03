using System;
using System.IO;
using System.Threading;
using BiliGreenDownloader;
internal static class LiveSmoke {
    sealed class Progress:IProgress<Update>{int last=-1;string text="";public void Report(Update u){int bucket=u.Percent<0?-1:u.Percent/25;if(bucket!=last||u.Percent<0&&u.Message!=text){Console.WriteLine("STAGE "+u.Message);last=bucket;text=u.Message;}}}
    static int Main(string[] args){
        try{using(var token=new CancellationTokenSource(TimeSpan.FromMinutes(4))){var result=Downloader.Download(new DownloadOptions{Url=args[0],Folder=Path.GetFullPath(args[1]),Compatible=true},new ToolPaths{YtDlp=Path.GetFullPath(args[2]),Ffmpeg="/usr/bin/ffmpeg",Ffprobe="/usr/bin/ffprobe"},token.Token,new Progress(),s=>Console.WriteLine(Core.Redact(s)));Console.WriteLine("VERIFIED "+result.Title+" | "+result.Description+" | "+new FileInfo(result.Path).Length+" bytes");}return 0;}catch(Exception ex){Console.WriteLine("LIVE CHECK FAILED: "+Core.Redact(ex.Message));return 1;}
    }
}
