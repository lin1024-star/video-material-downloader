# 视频素材下载器 1.1.0

Windows 10 / 11 64 位小工具。粘贴 B 站或 YouTube 视频链接，下载视频素材（含绿幕素材）。

## 1.1.0 更新方法

关闭旧版软件，解压本包，把其中的 `BiliGreenDownloader.exe` 和
`BiliGreenDownloader.exe.config` 覆盖到原文件夹，也可以直接运行新解压目录里的程序。
打开后，窗口标题应显示 **1.1.0**。

本次新增 YouTube 支持：同一个软件现在既能下载 B 站，也能下载 YouTube
（watch、youtu.be、shorts 链接均可；只下载链接对应的单个视频，不下载整个播放列表）。
已有下载组件、下载记录和设置继续复用，不需要删除旧缓存或重新安装环境。

（历史）1.0.1 修复了准备组件时的“找不到方法：System.String.Split(Char, StringSplitOptions)”
错误，以及分享短链、登录文件和输出路径处理中的同类框架接口兼容问题。

## 开始使用

1. 把压缩包**完整解压**到一个文件夹。
2. 双击 `BiliGreenDownloader.exe`。
3. 粘贴 B 站链接 / BV 号，或 YouTube 链接，选择保存位置，点“开始下载”。

不用安装 Python、CUDA，也不用输入命令。主程序使用 Windows 的 .NET Framework 4.5 或以上版本，Windows 10 / 11 一般已带有更高版本。

**首次运行会联网下载组件，合计约 130 MB**，需要能访问 GitHub 和 gyan.dev。之后复用组件，不会每次下载视频都重新下载。程序压缩包本身很小，未捆绑模型、视频样本或大型运行组件。

## 主要功能

- B 站：接受完整视频链接、BV / av 号、b23.tv 短链和带链接的分享文字。
- YouTube：接受 `watch?v=…`、`youtu.be/…`、shorts / embed 链接；只下载链接对应的单个视频。
- 默认取得当前可用的最高画质；也可选择优先 1080P 及以下。
- 默认“PR 兼容 MP4”：已经适合的 H.264 + AAC 素材直接封装；其他编码按需转为 H.264、8 位 YUV420P、AAC 的 MP4。无声视频也支持。
- 转码使用 CPU，不要求显卡或 CUDA。4K、长视频和 HDR 素材转换可能需要较长时间；转码会有少量画质损失。
- 取消勾选“PR 兼容 MP4”可保留原始编码；它可能保存为 MKV / WebM 等容器。
- 不覆盖同名文件；新增的文件名会带数字后缀。
- 成功、失败和取消都有记录。中断后的片段会保留，重新下载同一链接时尽量复用。
- 支持查看任务详情、打开文件所在位置、复制 / 导出运行日志。

## 分 P、播放列表、画质与登录

- B 站：带 `?p=2` 的链接下载第 2 P；不带 `p` 参数时下载第 1 P。每次下载一条，不下载整个合集。
- B 站暂不支持直播、番剧页面、个人空间和批量队列。YouTube 只下载单个视频，不下载整个播放列表、频道或首页。
- “最高画质”是当前账号与原视频实际允许取得的最高画质，不保证每个视频都能取得 1080P / 4K。
- 一般先直接下载。如果视频或清晰度要求登录，可以选择自己导出的 **Netscape 格式 cookies.txt**（B 站或 YouTube）。只读取所选文件中的 B 站 / YouTube 条目，不自动读取浏览器密码，不代替账号权限。
- 登录文件只在本次打开期间选中，不将其内容保存到设置。运行时的临时副本在任务结束或正常取消后清理。不要把原始 cookies.txt 发给别人。

## 首次准备组件失败怎么办

先检查是否能访问 GitHub 和 gyan.dev，然后重试。“更新下载组件”会更新 yt-dlp，并补齐缺失的音视频组件。

也可以用“导入已有组件”：选择包含下列三个 **Windows 64 位**文件的文件夹（也支持其 `bin` / `tools` 子目录）：

- `yt-dlp.exe`
- `ffmpeg.exe`
- `ffprobe.exe`

软件从原项目获取下载组件，并核对发布者提供的 SHA-256 校验值后安装。导入已有组件时，请选择自己信任的文件。

官方来源：

- yt-dlp：https://github.com/yt-dlp/yt-dlp
- yt-dlp 当前下载通道：https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest
- FFmpeg Windows 构建：https://www.gyan.dev/ffmpeg/builds/
- FFmpeg 项目：https://ffmpeg.org/

## 下载失败与清理

失败后查看“运行日志”页，导出日志即可反馈。412 / 429 通常需要稍后重试；需要登录的内容应使用有观看权限的账号。站点仅返回试看片段时，不会把它记录为完整素材。

下载 YouTube 需要网络能访问该站点。若提示要求验证身份（Sign in to confirm you're not a bot），通常是因为当前网络被判定为数据中心，请先开启代理后重试。

组件和设置放在 `%LOCALAPPDATA%\BiliGreenDownloader`。未完成片段放在所选保存目录下的隐藏文件夹 `.BiliGreen-work`。关掉软件后可删除临时目录以回收空间；删除后无法继续复用片段。异常断电或强制结束时，临时登录副本可能来不及清理，也在该目录中。

## 测试范围

详见 `VALIDATION.txt`。1.1.0 已在 Windows 10 / 11 x64 实机编译，并跑完 32 项回归检查（含真实 FFmpeg 转码、VP9→H.264、取消与失败恢复）。未做 Windows 实机界面点选与 Premiere Pro 导入验证。

`source` 为主程序源码，`tests` 为测试辅助文件，普通使用不需要打开。源码可在带 .NET Framework 编译器的 Windows 电脑上运行 `source\build.cmd` 重建。
