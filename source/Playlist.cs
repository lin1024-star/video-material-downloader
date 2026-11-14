using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace BiliGreenDownloader {
    /// <summary>多 P 视频里的一个 P（也可表示合集里的一个视频）。</summary>
    internal sealed class PlaylistPage {
        public int Index;          // 从 1 开始
        public string Title="";
        public double Duration;    // 秒；0 表示引擎没给
        public string Url="";      // yt-dlp 给的单条链接；可能为空
    }

    /// <summary>链接解析出的分 P 信息。</summary>
    internal sealed class PlaylistInfo {
        public string Title="";
        public List<PlaylistPage> Pages=new List<PlaylistPage>();
        public bool IsMultiPage{get{return Pages.Count>1;}}
    }

    /// <summary>
    /// 用 yt-dlp 的 --flat-playlist -J 读取链接一共几 P（只读，不下载）。
    /// 2026-11-14 新增：使用者反馈「多 P 视频的链接不带分 P 参数时只能下第一 P，
    /// 新手不知道要自己拼 ?p=2」——现在先探测，多 P 就让使用者选。
    /// </summary>
    internal static class PlaylistProbe {
        internal static List<string> ProbeArgs(string url) {
            return new List<string>{"--ignore-config","--no-plugin-dirs","--flat-playlist","-J","--no-warnings","--encoding","utf-8","--",url};
        }

        /// <summary>解析 yt-dlp -J --flat-playlist 的输出：单视频、多 P 合集都能处理。</summary>
        internal static PlaylistInfo Parse(string json) {
            var root=Json.Object(json);
            var info=new PlaylistInfo();
            object value;
            info.Title=Json.Get(root,"title")??"";
            string type=Json.Get(root,"_type")??"";
            var entries=new List<object>();
            if(root.TryGetValue("entries",out value)){var list=value as List<object>;if(list!=null)entries=list;}
            if(entries.Count==0){
                // yt-dlp 说这是合集却一条都没解析出来：不能当单视频蒙混过去。
                if(type=="playlist")throw new InvalidOperationException("链接里没有解析出任何分 P。");
                // 单视频（_type=video 或没有 entries）：只有一个「P」。
                info.Pages.Add(new PlaylistPage{Index=1,Title=info.Title});
                return info;
            }
            int index=0;
            foreach(object row in entries){
                var entry=row as Dictionary<string,object>;
                if(entry==null)continue;
                index++;
                double duration;
                Double.TryParse(Json.Get(entry,"duration"),NumberStyles.Float,CultureInfo.InvariantCulture,out duration);
                string title=Json.Get(entry,"title");
                string url=Json.Get(entry,"url");
                if(String.IsNullOrEmpty(url))url=Json.Get(entry,"webpage_url");
                info.Pages.Add(new PlaylistPage{Index=index,Title=String.IsNullOrEmpty(title)?("P"+index):title,Duration=duration,Url=url??""});
            }
            if(info.Pages.Count==0)throw new InvalidOperationException("没有从链接里解析出分 P 信息。");
            return info;
        }

        /// <summary>调 yt-dlp 探测；失败抛异常，由调用方决定是否按原方式继续。</summary>
        internal static PlaylistInfo Probe(string ytdlp,string url,System.Threading.CancellationToken token) {
            // 先走 B 站官方接口：一次请求就能拿到每个 P 的标题和时长，弹窗里新手看得懂。
            // 接口失败（风控 / 网络）再退回 yt-dlp 的 --flat-playlist（快，但只有 P 数）。
            PlaylistInfo pages=null;
            try{pages=BilibiliPages(url,token);}catch(Exception){pages=null;}
            if(pages!=null)return pages;
            return ProbeFlat(ytdlp,url,token);
        }

        /// <summary>B 站官方接口：/x/web-interface/view 返回 data.pages（每个 P 的标题和时长）。</summary>
        internal static PlaylistInfo BilibiliPages(string url,System.Threading.CancellationToken token) {
            token.ThrowIfCancellationRequested();
            Uri uri;
            if(!Uri.TryCreate(url??"",UriKind.Absolute,out uri))return null;
            if(!Core.BilibiliHosts.Contains(uri.Host.ToLowerInvariant()))return null;
            var match=Regex.Match(uri.AbsolutePath,@"^/video/(BV[0-9A-Za-z]{10}|av[0-9]+)/?$",RegexOptions.IgnoreCase);
            if(!match.Success)return null;
            string id=match.Groups[1].Value;
            string query=id.StartsWith("av",StringComparison.OrdinalIgnoreCase)?"aid="+id.Substring(2):"bvid="+id;
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create("https://api.bilibili.com/x/web-interface/view?"+query);
            request.UserAgent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) BiliGreenDownloader/"+Core.Version;
            request.Referer="https://www.bilibili.com/";
            request.Timeout=12000;request.ReadWriteTimeout=12000;
            using(token.Register(request.Abort))
            using(var response=(HttpWebResponse)request.GetResponse())
            using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)){
                string text=reader.ReadToEnd();
                var root=Json.Object(text);
                int code;Int32.TryParse(Json.Get(root,"code"),out code);
                if(code!=0)throw new InvalidOperationException("B 站接口返回错误："+(Json.Get(root,"message")??code.ToString()));
                object value;
                var data=(root.TryGetValue("data",out value)?value:null) as Dictionary<string,object>;
                if(data==null)throw new InvalidOperationException("B 站接口没有返回视频信息。");
                var info=new PlaylistInfo{Title=Json.Get(data,"title")??""};
                var rows=(data.TryGetValue("pages",out value)?value:null) as List<object>;
                if(rows==null||rows.Count==0)throw new InvalidOperationException("B 站接口没有返回分 P 信息。");
                foreach(object row in rows){
                    var page=row as Dictionary<string,object>;
                    if(page==null)continue;
                    int index;Int32.TryParse(Json.Get(page,"page"),out index);
                    if(index<=0)index=info.Pages.Count+1;
                    double duration;Double.TryParse(Json.Get(page,"duration"),NumberStyles.Float,CultureInfo.InvariantCulture,out duration);
                    info.Pages.Add(new PlaylistPage{Index=index,Title=Json.Get(page,"part")??("P"+index),Duration=duration});
                }
                if(info.Pages.Count==0)throw new InvalidOperationException("B 站接口没有返回分 P 信息。");
                return info;
            }
        }

        /// <summary>兜底：yt-dlp --flat-playlist -J（快，但通常不带标题/时长）。</summary>
        internal static PlaylistInfo ProbeFlat(string ytdlp,string url,System.Threading.CancellationToken token) {
            var result=Runner.Run(ytdlp,ProbeArgs(url),Path.GetDirectoryName(ytdlp),token);
            if(result.ExitCode!=0)throw new InvalidOperationException("读取分 P 信息失败："+Core.Redact(result.Error));
            return Parse(result.Output);
        }

        /// <summary>把分 P 序号拼回 B 站链接；非 B 站链接返回 null（这个功能只做 B 站）。</summary>
        internal static string PageUrl(string url,int page) {
            if(page<1)return null;
            Uri uri;
            if(!Uri.TryCreate(url,UriKind.Absolute,out uri))return null;
            if(!Core.BilibiliHosts.Contains(uri.Host.ToLowerInvariant()))return null;
            return "https://www.bilibili.com"+uri.AbsolutePath+"?p="+page;
        }

        /// <summary>选择结果 → 真正要下载的链接：优先用引擎给的单条链接，拼不出就退回 ?p= 形式。</summary>
        internal static string ChosenUrl(string originalUrl,PlaylistPage page) {
            if(page==null)return null;
            Uri parsed;
            if(!String.IsNullOrEmpty(page.Url)&&Uri.TryCreate(page.Url,UriKind.Absolute,out parsed))return page.Url;
            return PageUrl(originalUrl,page.Index);
        }
    }
}
