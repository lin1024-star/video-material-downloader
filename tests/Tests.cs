using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using BiliGreenDownloader;

internal static class Tests {
    static int passed,failed;static string root;static ToolPaths tools;
    sealed class Sink:IProgress<Update>{public readonly List<Update> Items=new List<Update>();public void Report(Update u){lock(Items)Items.Add(u);}}
    static void Check(bool value,string message="assertion failed"){if(!value)throw new Exception(message);}
    static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+name+": "+ex);}}
    static void Throws(Action action){bool thrown=false;try{action();}catch{thrown=true;}Check(thrown,"expected rejection");}
    static DownloadOptions Options(string name,bool compatible=true){return new DownloadOptions{Url="BV13x41117TL",Folder=Path.Combine(root,name),Compatible=compatible};}
    static DownloadResult Download(DownloadOptions options,Sink sink=null,Action<string> log=null,CancellationToken? token=null){return Downloader.Download(options,tools,token??CancellationToken.None,sink??new Sink(),log??(s=>{}));}
    static string[] Parts(string dir){return Directory.Exists(dir)?Directory.GetFiles(dir,"*.part",SearchOption.AllDirectories):new string[0];}
    public static int Main(string[] args){
        root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);tools=new ToolPaths{YtDlp=Path.GetFullPath(args[1]),Ffmpeg=args[2],Ffprobe=args[3]};string fixtureDir=Path.GetFullPath(args[4]);
        Test("bare BV and av IDs",()=>{Check(Core.NormalizeUrl("BV13x41117TL")=="https://www.bilibili.com/video/BV13x41117TL");Check(Core.NormalizeUrl("AV8903802").EndsWith("av8903802"));});
        Test("share text and exact part retained",()=>Check(Core.NormalizeUrl("分享： https://www.bilibili.com/video/BV13x41117TL/?p=2&share_source=copy 网页")=="https://www.bilibili.com/video/BV13x41117TL?p=2"));
        Test("short link normalization",()=>Check(Core.NormalizeUrl("http://b23.tv/AbC123/")=="https://b23.tv/AbC123"));
        Test("YouTube links normalize to a single watch URL",()=>{
            string want="https://www.youtube.com/watch?v=dQw4w9WgXcQ";
            foreach(string s in new[]{"https://www.youtube.com/watch?v=dQw4w9WgXcQ","https://youtu.be/dQw4w9WgXcQ","https://youtu.be/dQw4w9WgXcQ?t=30","https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf&index=2","https://m.youtube.com/watch?v=dQw4w9WgXcQ","https://www.youtube.com/shorts/dQw4w9WgXcQ","https://www.youtube.com/embed/dQw4w9WgXcQ","https://www.youtube.com/live/dQw4w9WgXcQ","https://music.youtube.com/watch?v=dQw4w9WgXcQ","www.youtube.com/watch?v=dQw4w9WgXcQ","youtu.be/dQw4w9WgXcQ"})Check(Core.NormalizeUrl(s)==want,s+" -> "+Core.NormalizeUrl(s));
            Check(Core.NormalizeUrl("看看这个 https://youtu.be/dQw4w9WgXcQ 很好")==want,"share text around a YouTube link");
        });
        Test("reject other hosts / login tricks / shell input",()=>{foreach(string s in new[]{"https://evil.com/video/BV13x41117TL","https://bilibili.com.evil.com/video/BV13x41117TL","https://user@www.bilibili.com/video/BV13x41117TL","https://www.bilibili.com:444/video/BV13x41117TL","file:///etc/passwd","--exec calc.exe","https://www.bilibili.com/bangumi/play/ep1","https://space.bilibili.com/1","https://www.bilibili.com/video/BV13x41117TL?p=0","https://youtube.com.evil.com/watch?v=dQw4w9WgXcQ","https://www.youtube.com.evil.com/watch?v=dQw4w9WgXcQ","https://fakeyoutube.com/watch?v=dQw4w9WgXcQ","https://evil-youtu.be/dQw4w9WgXcQ","dQw4w9WgXcQ","https://www.youtube.com/playlist?list=PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf","https://www.youtube.com/@SomeChannel","https://www.youtube.com/","https://youtu.be/abc","https://www.youtube.com/watch?v=tooshort"})Throws(()=>Core.NormalizeUrl(s));});
        Test("Windows names and Unicode boundaries",()=>{Check(Core.SafeName("CON.txt")=="_CON.txt");Check(Core.SafeName("a:b/c?<>\\*")=="a_b_c_____");Check(Core.SafeName("..")=="视频素材");Check(!Char.IsHighSurrogate(Core.SafeName(new string('a',89)+"😀").Last()));});
        Test("JSON escaped Chinese, quotes and newline round trip",()=>{var value=new Dictionary<string,object>{{"title","绿幕 😀 \"引号\"\n第二行"},{"items",new object[]{true,1.5,null}}};var d=Json.Object(Json.Write(value));Check(Json.Get(d,"title")==value["title"].ToString());Check(((List<object>)d["items"]).Count==3);});
        Test("JSON rejects malformed, deep and truncated inputs",()=>{foreach(string s in new[]{"{\"a\":}","[]garbage","\"\\x\"",new string('[',66)+"0"+new string(']',66)})Throws(()=>Json.Parse(s));});
        Test("cookie filter only retains Bilibili and YouTube sites",()=>{string p=Path.Combine(root,"cookies-input.txt");File.WriteAllText(p,"# Netscape HTTP Cookie File\n.bilibili.com\tTRUE\t/\tTRUE\t1999999999\tSESSDATA\tprivatevalue\n#HttpOnly_.bilibili.com\tTRUE\t/\tTRUE\t1999999999\tbili_jct\tothervalue\n.youtube.com\tTRUE\t/\tTRUE\t1999999999\tSID\tyoutubevalue\n.google.com\tTRUE\t/\tTRUE\t1999999999\tSAPISID\tgooglevalue\n.evil.com\tTRUE\t/\tTRUE\t1999999999\tsecret\tforeignvalue\n.fakebilibili.com\tTRUE\t/\tTRUE\t1999999999\tsecret\tfakevalue\n.fakeyoutube.com\tTRUE\t/\tTRUE\t1999999999\tsecret\tfakeytvalue\n.evilyoutube.com\tTRUE\t/\tTRUE\t1999999999\tsecret\tevilvalue\n");string v=Core.FilterCookies(p);Check(v.Contains("privatevalue")&&v.Contains("othervalue")&&v.Contains("youtubevalue")&&v.Contains("googlevalue")&&!v.Contains("foreignvalue")&&!v.Contains("fakevalue")&&!v.Contains("fakeytvalue")&&!v.Contains("evilvalue"));});
        Test("credentials and signed media URLs redacted",()=>{string value=Core.Redact("Cookie=SESSDATA=secret SESSDATA=abc Authorization: Bearer-token https://media.example/a?token=secret");Check(!value.Contains("secret")&&!value.Contains("Bearer-token")&&!value.Contains("abc"));});
        Test("output path containment rejects sibling directories",()=>{Check(Core.Inside(Path.Combine(root,"a","b.mp4"),Path.Combine(root,"a")));Check(!Core.Inside(Path.Combine(root,"ab","b.mp4"),Path.Combine(root,"a")));Check(!Core.Inside(Path.Combine(root,"a","..","b.mp4"),Path.Combine(root,"a")));});
        Test("command isolation and paths with spaces",()=>{var a=Core.DownloadArgs("https://www.bilibili.com/video/BV13x41117TL",Path.Combine(root,"空 格"),"",tools,false);Check(a.Contains("--ignore-config")&&a.Contains("--no-plugin-dirs")&&a[a.Count-2]=="--"&&!a.Contains("--exec"));Check(Core.Quote("C:\\path with spaces\\")=="\"C:\\path with spaces\\\\\"");Check(Core.Quote("a\"b")=="\"a\\\"b\"");});
        Test("state history persistence and interrupted state",()=>{string path=Path.Combine(root,"state.json");var state=new AppState{Folder="D:\\绿幕 素材",Compatible=false,Limit1080=true};state.Jobs.Add(new Job{Title="成功素材",Status="成功",Path="a.mp4"});state.Jobs.Add(new Job{Title="未完成",Status="处理中"});state.Save(path);state.Save(path);var loaded=AppState.Load(path);Check(loaded.Folder==state.Folder&&!loaded.Compatible&&loaded.Limit1080);Check(loaded.Jobs.Count==2&&loaded.Jobs[0].Status=="成功"&&loaded.Jobs[1].Status=="已中断");});
        Test("extract only known component names, ignoring archive paths",()=>{string zip=Path.Combine(root,"tools.zip"),outdir=Path.Combine(root,"extract");Directory.CreateDirectory(outdir);using(var z=ZipFile.Open(zip,ZipArchiveMode.Create)){foreach(string name in new[]{"../../bin/ffmpeg.exe","pack/bin/ffprobe.exe","../../unwanted.exe"})using(var w=new StreamWriter(z.CreateEntry(name).Open()))w.Write("test");}RuntimeSetup.ExtractTools(zip,outdir,CancellationToken.None);Check(File.Exists(Path.Combine(outdir,"ffmpeg.exe"))&&File.Exists(Path.Combine(outdir,"ffprobe.exe"))&&Directory.GetFiles(outdir).Length==2&&!File.Exists(Path.Combine(root,"unwanted.exe")));});
        Test("duplicate tool entries rejected",()=>{string zip=Path.Combine(root,"duplicate.zip");using(var z=ZipFile.Open(zip,ZipArchiveMode.Create)){z.CreateEntry("a/bin/ffmpeg.exe");z.CreateEntry("b/bin/ffmpeg.exe");}Throws(()=>RuntimeSetup.ExtractTools(zip,root,CancellationToken.None));});
        Test("runtime PE validation rejects renamed text",()=>{string p=Path.Combine(root,"text.exe");File.WriteAllText(p,"not an executable");Check(!RuntimeSetup.IsPe64(p));});
        Test("compatible H.264 + AAC detected for copy",()=>{var m=MediaInfo.Probe(tools.Ffprobe,Path.Combine(fixtureDir,"h264.mkv"),CancellationToken.None);Check(m.CanCopy&&m.Width==320&&m.Height==240&&m.HasAudio);});
        Test("VP9 Opus requires transcode",()=>{var m=MediaInfo.Probe(tools.Ffprobe,Path.Combine(fixtureDir,"vp9.webm"),CancellationToken.None);Check(m.Video=="vp9"&&m.Audio=="opus"&&!m.CanCopy);});
        Test("H.264 audio-less material supported",()=>{var m=MediaInfo.Probe(tools.Ffprobe,Path.Combine(fixtureDir,"silent.mp4"),CancellationToken.None);Check(!m.HasAudio&&m.CanCopy);});
        Environment.SetEnvironmentVariable("BILI_TEST_MODE","ok");Environment.SetEnvironmentVariable("BILI_TEST_FIXTURE",Path.Combine(fixtureDir,"h264.mkv"));
        Test("full merge / probe / MP4 publication path",()=>{var sink=new Sink();var logs=new List<string>();var result=Download(Options("copy flow"),sink,s=>logs.Add(s));Check(File.Exists(result.Path)&&result.Path.EndsWith("_PR.mp4"));Check(!Directory.Exists(Path.Combine(root,"copy flow",".BiliGreen-work")));Check(sink.Items.Last().Percent==100);Check(MediaInfo.Probe(tools.Ffprobe,result.Path,CancellationToken.None).CanCopy);});
        Test("existing same-named output is not overwritten",()=>{var folder=Path.Combine(root,"copy flow");string first=Directory.GetFiles(folder,"*.mp4").Single();string hash=Core.Hash(first);var result=Download(Options("copy flow"));Check(result.Path.EndsWith("_2.mp4")&&Core.Hash(first)==hash);});
        Environment.SetEnvironmentVariable("BILI_TEST_FIXTURE",Path.Combine(fixtureDir,"vp9.webm"));
        Test("full VP9 Opus conversion emits H264 AAC",()=>{var r=Download(Options("transcode flow"));var info=MediaInfo.Probe(tools.Ffprobe,r.Path,CancellationToken.None);Check(info.Video=="h264"&&info.Audio=="aac"&&info.Pixel=="yuv420p"&&info.Duration>1.8);});
        Test("original encoding output retains exact source bytes",()=>{var r=Download(Options("original flow",false));Check(Core.Hash(r.Path)==Core.Hash(Path.Combine(fixtureDir,"vp9.webm")));});
        Environment.SetEnvironmentVariable("BILI_TEST_FIXTURE",Path.Combine(fixtureDir,"silent.mp4"));
        Test("full silent-video flow completes",()=>{var r=Download(Options("silent flow"));Check(!MediaInfo.Probe(tools.Ffprobe,r.Path,CancellationToken.None).HasAudio);});
        Test("temporary cookies are filtered and removed on success",()=>{var opt=Options("cookies success");opt.Cookies=Path.Combine(root,"cookies-input.txt");Download(opt);Check(!Directory.GetFiles(opt.Folder,"cookies.txt",SearchOption.AllDirectories).Any());});
        Environment.SetEnvironmentVariable("BILI_TEST_MODE","fail");
        Test("failed download never publishes a final file; part retained",()=>{var opt=Options("failed flow");var sink=new Sink();var logs=new List<string>();Throws(()=>Download(opt,sink,s=>logs.Add(s)));Check(Parts(opt.Folder).Length==1&&!Directory.GetFiles(opt.Folder,"*.mp4").Any());Check(!sink.Items.Any(u=>u.Message=="下载完成"));Check(!String.Join("\n",logs).Contains("privatevalue"));});
        Test("temporary cookie copy removed after failure",()=>{var opt=Options("cookies failure");opt.Cookies=Path.Combine(root,"cookies-input.txt");Throws(()=>Download(opt));Check(!Directory.GetFiles(opt.Folder,"cookies.txt",SearchOption.AllDirectories).Any());});
        Environment.SetEnvironmentVariable("BILI_TEST_MODE","wait");
        Test("cancellation stops process, retains part and clears cookies",()=>{var opt=Options("cancel flow");opt.Cookies=Path.Combine(root,"cookies-input.txt");using(var c=new CancellationTokenSource()){c.CancelAfter(1200);bool cancelled=false;try{Download(opt,null,null,c.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&Parts(opt.Folder).Length==1&&!Directory.GetFiles(opt.Folder,"cookies.txt",SearchOption.AllDirectories).Any());}});
        Environment.SetEnvironmentVariable("BILI_TEST_MODE","preview");
        Test("preview-only result is not labelled complete",()=>{var opt=Options("preview flow");Throws(()=>Download(opt));Check(!Directory.GetFiles(opt.Folder,"*.mp4").Any());});
        Environment.SetEnvironmentVariable("BILI_TEST_MODE","outside");
        Test("engine cannot publish a path outside its staging folder",()=>{var opt=Options("outside flow");Throws(()=>Download(opt));Check(!Directory.GetFiles(opt.Folder,"*.mp4").Any());});
        Environment.SetEnvironmentVariable("BILI_TEST_MODE","ok");
        Test("retry reuses the same staging directory",()=>{var opt=Options("failed flow");Download(opt);Check(!Directory.Exists(Path.Combine(opt.Folder,".BiliGreen-work")));});
        Test("observer callback exception cannot kill runner",()=>{var r=Runner.Run(tools.Ffprobe,new[]{"-version"},root,CancellationToken.None,(s,e)=>{throw new Exception("observer error");});Check(r.ExitCode==0&&r.Output.Contains("ffprobe"));});
        Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed");return failed==0?0:1;
    }
}
