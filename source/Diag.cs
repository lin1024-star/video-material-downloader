using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BiliGreenDownloader {
    /// <summary>
    /// 报错信息统一格式 —— 「我实际看到的是」那一段是灵魂。
    ///
    /// 起因：有使用者报「没有取得唯一的已下载视频文件」，但不知道是拿到 0 个
    /// 还是 5 个 —— 这两种是完全不同的问题（前者多半是链接或网络，
    /// 后者多半是合集 / 多 P），却报了同一句话，他自己没法查。
    ///
    /// 格式：
    ///   &lt;出了什么事&gt;
    ///
    ///   我实际看到的是：
    ///   　· &lt;检查项&gt; —— &lt;实际状态&gt;
    ///
    ///   你可以这样办：
    ///   　1) &lt;具体到能照着点&gt;
    ///
    ///   （还不行：把上面这一整段发给开发者。）
    ///
    /// 规矩：状态必须区分「不存在 / 文件在 / 目录空的 / 里面有 abc」——
    /// 四种情况的解决办法完全不同，混着说等于没说。
    /// </summary>
    internal static class Diag {
        /// <summary>把路径的状态说清楚。这是整套格式里最有用的一句。</summary>
        public static string Describe(string path) {
            if (String.IsNullOrEmpty(path)) return "（没有路径）";
            try {
                if (File.Exists(path)) {
                    return path + "（文件在，" + Size(new FileInfo(path).Length) + "）";
                }
                if (!Directory.Exists(path)) return path + "（这个路径不存在）";
                string[] names = Directory.GetFileSystemEntries(path);
                if (names.Length == 0) return path + "（目录在，但是空的）";
                // 这里刻意用最朴素的写法：不用 LINQ 方法组。
                // 上一版写成 names.Take(8).Select(Path.GetFileName) 会在运行时崩 ——
                // 就是 VALIDATION.txt 里记过的老坑：编译环境支持的 API
                // 不一定属于目标 .NET Framework。报错函数自己崩掉最要命。
                var sb = new StringBuilder();
                int show = names.Length < 8 ? names.Length : 8;
                for (int i = 0; i < show; i++) {
                    if (i > 0) sb.Append("、");
                    sb.Append(FileNameOf(names[i]));
                }
                string more = names.Length > 8 ? " 等 " + names.Length + " 项" : "";
                return path + "（里面有：" + sb.ToString() + more + "）";
            } catch (Exception e) {
                return path + "（打不开：" + e.GetType().Name + "）";
            }
        }

        /// <summary>自己从路径里取文件名，不依赖 Path.GetFileName 的重载解析。</summary>
        public static string FileNameOf(string path) {
            if (String.IsNullOrEmpty(path)) return "";
            int cut = path.LastIndexOfAny(new[] { '\\', '/' });
            return cut >= 0 ? path.Substring(cut + 1) : path;
        }

        /// <summary>多久没动 / 文件多大，说成人看得懂的单位。</summary>
        public static string Size(long bytes) {
            if (bytes < 1024) return bytes + " 字节";
            if (bytes < 1024L * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / 1024.0 / 1024).ToString("0.0") + " MB";
            return (bytes / 1024.0 / 1024 / 1024).ToString("0.00") + " GB";
        }

        /// <summary>一句话说清出了什么事 + 试了几次。</summary>
        public static string Head(string what, int attempt, int total) {
            return attempt > 1 ? what + "（第 " + attempt + " / " + total + " 次尝试都失败）" : what;
        }

        /// <summary>
        /// 拼出统一格式的报错文本。
        /// checks 每条写「我看到了什么」，steps 每条写「你可以怎么办」。
        /// </summary>
        public static string Diagnose(string headline, IEnumerable<string> checks,
                                      IEnumerable<string> steps, string extra = null) {
            var sb = new StringBuilder((headline ?? "").TrimEnd());
            var c = (checks ?? Enumerable.Empty<string>())
                    .Where(x => !String.IsNullOrEmpty(x)).ToList();
            if (c.Count > 0) {
                sb.Append("\r\n\r\n我实际看到的是：\r\n");
                foreach (string line in c) sb.Append("　· " + line + "\r\n");
            }
            var s = (steps ?? Enumerable.Empty<string>())
                    .Where(x => !String.IsNullOrEmpty(x)).ToList();
            if (s.Count > 0) {
                sb.Append("\r\n你可以这样办：\r\n");
                for (int i = 0; i < s.Count; i++) sb.Append("　" + (i + 1) + ") " + s[i] + "\r\n");
            }
            if (!String.IsNullOrEmpty(extra)) sb.Append("\r\n" + extra.TrimEnd() + "\r\n");
            sb.Append("\r\n（还不行：把上面这一整段发给开发者。）");
            return sb.ToString();
        }

        /// <summary>
        /// 把引擎的原始输出裁成一小段放进报错里。
        /// 不裁的话几百行日志会淹掉重点；不放进来的话，使用者又没法提供线索。
        /// </summary>
        public static string Tail(string text, int lines = 6, int width = 120) {
            if (String.IsNullOrEmpty(text)) return "（引擎没有输出）";
            var all = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            if (all.Count == 0) return "（引擎没有输出）";
            var last = all.Skip(Math.Max(0, all.Count - lines)).ToList();
            var sb = new StringBuilder();
            if (all.Count > lines) sb.Append("（一共 " + all.Count + " 行，取最后 " + lines + " 行）");
            foreach (string line in last) {
                sb.Append("\r\n　　　" + (line.Length > width ? line.Substring(0, width) + "…" : line));
            }
            return sb.ToString();
        }

        /// <summary>链接显示用：太长就截中间，避免把界面撑破。</summary>
        public static string ShortUrl(string url, int keep = 96) {
            if (String.IsNullOrEmpty(url)) return "（空）";
            return url.Length <= keep ? url : url.Substring(0, keep) + "…";
        }
    }
}
