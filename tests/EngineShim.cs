// 把 fake_engine.py 包成一个 exe，供回归测试当「下载引擎」用。
//
// 为什么需要这一层：Core.Runner 用 UseShellExecute=false 起进程，
// 不能直接执行 .py（Windows 不认）也不能执行 .cmd（需要经过 cmd.exe）。
// 所以这里做一个小壳：把参数原样转给 python fake_engine.py。
//
// 只用于测试，不参与主程序构建。
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

internal static class EngineShim {
    static int Main(string[] args) {
        string here = AppDomain.CurrentDomain.BaseDirectory;
        string script = Path.Combine(here, "fake_engine.py");
        if (!File.Exists(script)) {
            Console.Error.WriteLine("找不到 " + script);
            return 2;
        }
        string python = Environment.GetEnvironmentVariable("BILI_TEST_PYTHON");
        if (String.IsNullOrEmpty(python)) python = "python";

        var sb = new StringBuilder();
        sb.Append(Quote(script));
        foreach (string a in args) sb.Append(' ').Append(Quote(a));

        var psi = new ProcessStartInfo(python, sb.ToString()) {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };
        // 和主程序一样把 Python 的输出固定成 UTF-8，否则中文会乱码
        psi.EnvironmentVariables["PYTHONUTF8"] = "1";
        psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        try {
            using (var p = Process.Start(psi)) {
                p.OutputDataReceived += (s, e) => { if (e.Data != null) Console.Out.WriteLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                return p.ExitCode;
            }
        } catch (Exception e) {
            Console.Error.WriteLine("起不了 fake_engine：" + e.Message);
            return 3;
        }
    }

    static string Quote(string s) {
        if (s.Length > 0 && s.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return s;
        var sb = new StringBuilder("\"");
        int backslashes = 0;
        foreach (char c in s) {
            if (c == '\\') { backslashes++; continue; }
            if (c == '"') { sb.Append('\\', backslashes * 2 + 1).Append('"'); backslashes = 0; continue; }
            sb.Append('\\', backslashes).Append(c);
            backslashes = 0;
        }
        sb.Append('\\', backslashes * 2).Append('"');
        return sb.ToString();
    }
}
