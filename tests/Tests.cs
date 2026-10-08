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
        // ---- 报错信息格式（Diag）：纯函数，不需要引擎、网络或 ffmpeg ----
        Test("Diag.Describe tells the four path states apart",()=>{
            string tmp=Path.Combine(root,"diag_probe");if(Directory.Exists(tmp))Directory.Delete(tmp,true);
            Directory.CreateDirectory(tmp);
            Check(Diag.Describe(Path.Combine(tmp,"nope")).Contains("不存在"),"missing path");
            Check(Diag.Describe(tmp).Contains("空的"),"empty dir");
            string f=Path.Combine(tmp,"v.mp4");File.WriteAllBytes(f,new byte[1536*1024]);
            Check(Diag.Describe(f).Contains("文件在"),"file present");
            Check(Diag.Describe(f).Contains("1.5 MB"),"file size shown");
            Directory.CreateDirectory(Path.Combine(tmp,"a"));File.WriteAllText(Path.Combine(tmp,"c.txt"),"x");
            string many=Diag.Describe(tmp);
            Check(many.Contains("里面有")&&many.Contains("v.mp4"),"lists contents");
            Check(Diag.Describe(null).Contains("没有路径"),"null path must not throw");
            Directory.Delete(tmp,true);
        });
        Test("Diag.Size uses readable units",()=>{
            Check(Diag.Size(0)=="0 字节","bytes");
            Check(Diag.Size(1536)=="1.5 KB","kb");
            Check(Diag.Size(5L<<20)=="5.0 MB","mb");
        });
        Test("Diag.Tail keeps the last lines and never throws",()=>{
            Check(Diag.Tail("").Contains("没有输出"),"empty input");
            Check(Diag.Tail(null).Contains("没有输出"),"null input");
            string t=Diag.Tail("a\r\nb\r\nc\r\nd",2);
            Check(t.Contains("c")&&t.Contains("d"),"keeps the tail");
            Check(t.Contains("一共 4 行"),"says how many lines were dropped");
            Check(Diag.Tail(new string('x',500),1).Length<300,"very long line is trimmed");
        });
        Test("Diag.Diagnose always carries the four sections",()=>{
            string t=Diag.Diagnose("出事了。",new[]{"检查一 —— 空的"},new[]{"点这里"},null);
            Check(t.Contains("出事了。"),"headline");
            Check(t.Contains("我实际看到的是："),"checks section");
            Check(t.Contains("你可以这样办："),"steps section");
            Check(t.Contains("发给开发者"),"tail");
            Check(t.Contains("　· 检查一"),"check bullet");
            Check(t.Contains("　1) 点这里"),"numbered step");
            Check(Diag.Diagnose("x",null,null).Contains("x"),"null lists must not throw");
        });
        Test("failure hints keep the engine output",()=>{
            // 老版本只给一句结论，把引擎输出丢了 —— 使用者没法提供线索。
            string raw="ERROR: [BiliBili] Unable to download webpage: HTTP Error 404: Not Found\r\nERROR: [BiliBili] Failed to resolve";
            string hint=Core.FailureHint(raw);
            Check(hint.Contains("视频不存在"),"classified as 404");
            Check(hint.Contains("我实际看到的是："),"carries the evidence");
            Check(hint.Contains("HTTP Error 404"),"keeps the raw line");
            Check(hint.Contains("你可以这样办："),"carries the steps");
            Check(Core.FailureHint("Sign in to confirm you're not a bot").Contains("验证身份"),"youtube branch");
            Check(Core.FailureHint("HTTP Error 429").Contains("限制了请求"),"rate limit branch");
            Check(Core.FailureHint("").Contains("我实际看到的是："),"unknown branch still formatted");
            Check(Core.FailureHint(null).Contains("我实际看到的是："),"null must not throw");
        });
        Test("multi-candidate failure says how many were found",()=>{
            // 实测踩过：拿到 0 个和拿到 5 个报的是同一句话，使用者没法自查。
            string zero=Diag.Diagnose("没能从这个链接解析出视频。",
                new[]{"链接解析出 0 个候选 —— 多半是链接本身、网络，或站点改了规则"},new[]{"粘到浏览器里试试"});
            Check(zero.Contains("0 个候选"),"zero case is explicit");
            string many=Diag.Diagnose("这个链接里有多个视频，下载器不知道该下哪一个。",
                new[]{"链接解析出 12 个候选（多个）—— 多半是合集、多 P 或播放列表链接"},new[]{"用带 p= 的分 P 链接"});
            Check(many.Contains("12 个候选"),"many case says the count");
            Check(many.Contains("分 P"),"many case gives the right fix");
        });
        Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed");return failed==0?0:1;
    }
}
