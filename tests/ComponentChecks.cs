using System;
using System.IO;
using System.Threading;
using BiliGreenDownloader;
internal static class ComponentChecks {
    static int Main(string[] args){
        try{
            string folder=Path.GetFullPath(args[0]),extract=Path.Combine(folder,"extracted");Directory.CreateDirectory(extract);
            RuntimeSetup.ExtractTools(Path.Combine(folder,"ffmpeg.zip"),extract,CancellationToken.None);
            foreach(string path in new[]{Path.Combine(folder,"yt-dlp.exe"),Path.Combine(extract,"ffmpeg.exe"),Path.Combine(extract,"ffprobe.exe")}){if(!RuntimeSetup.IsPe64(path))throw new Exception("Invalid x64 PE: "+Path.GetFileName(path));Console.WriteLine("PASS official x64 component: "+Path.GetFileName(path));}
            string hdr=Path.GetFullPath(args[1]),output=Path.Combine(Path.GetDirectoryName(hdr),"hdr-converted.mp4");var info=MediaInfo.Probe("/usr/bin/ffprobe",hdr,CancellationToken.None);if(!info.Hdr||info.CanCopy)throw new Exception("HDR not detected");
            var r=Runner.Run("/usr/bin/ffmpeg",MediaInfo.ConvertArgs(hdr,output,info),Path.GetDirectoryName(hdr),CancellationToken.None);if(r.ExitCode!=0)throw new Exception(r.Error);
            var result=MediaInfo.Probe("/usr/bin/ffprobe",output,CancellationToken.None);if(result.Video!="h264"||result.Pixel!="yuv420p"||result.Transfer!="bt709"||result.Hdr)throw new Exception("HDR conversion invalid");Console.WriteLine("PASS actual 10-bit PQ HDR to BT.709 H264 conversion");return 0;
        }catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}
