@echo off
setlocal
cd /d "%~dp0"
set "BILI_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%BILI_CSC%" (
  echo .NET Framework C# compiler was not found.
  exit /b 1
)
"%BILI_CSC%" /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /win32manifest:app.manifest /win32icon:app.ico /out:..\BiliGreenDownloader.exe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll AssemblyInfo.cs Json.cs Diag.cs Core.cs RuntimeSetup.cs Downloader.cs MainForm.cs
if errorlevel 1 exit /b 1
echo Built ..\BiliGreenDownloader.exe
endlocal
