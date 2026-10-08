@echo off
rem One-click regression suite for BiliGreenDownloader.
rem
rem Steps:
rem   1. generate three test fixtures with local ffmpeg (320x240, 2s, no third-party media)
rem   2. wrap tests\fake_engine.py into an exe (Core.Runner cannot launch .py directly)
rem   3. compile the test program
rem   4. run it and report
rem
rem Requires: .NET Framework compiler (ships with Windows), ffmpeg, ffprobe, python.
rem Override paths with environment variables; otherwise they are looked up in PATH:
rem   set BILI_FFMPEG=D:\tools\ffmpeg.exe
rem   set BILI_FFPROBE=D:\tools\ffprobe.exe
rem   set BILI_TEST_PYTHON=D:\python\python.exe
rem
rem NOTE: keep this file ASCII-only. cmd.exe reads it in the OEM codepage,
rem so UTF-8 Chinese bytes get mis-parsed and can even swallow line breaks.
setlocal
cd /d "%~dp0.."
set "ROOT=%CD%"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [ERROR] .NET Framework compiler not found: %CSC%
  exit /b 1
)
if "%BILI_FFMPEG%"=="" set "BILI_FFMPEG=ffmpeg"
if "%BILI_FFPROBE%"=="" set "BILI_FFPROBE=ffprobe"
if "%BILI_TEST_PYTHON%"=="" set "BILI_TEST_PYTHON=python"
set "REF=/reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll"
set "SRC=source\AssemblyInfo.cs source\Json.cs source\Diag.cs source\Core.cs source\RuntimeSetup.cs source\Downloader.cs source\MainForm.cs"

set "TMP=%ROOT%\_testrun"
set "FIX=%TMP%\fixtures"
set "WORK=%TMP%\work"

echo === 1/4 build fixtures ===
if exist "%TMP%" rmdir /s /q "%TMP%"
mkdir "%FIX%" 2>nul
mkdir "%WORK%" 2>nul

rem H.264 + AAC: can be copied without re-encoding
"%BILI_FFMPEG%" -hide_banner -loglevel error -y -f lavfi -i "testsrc2=size=320x240:rate=25:duration=2" -f lavfi -i "sine=frequency=440:duration=2" -c:v libx264 -pix_fmt yuv420p -c:a aac -shortest "%FIX%\h264.mkv"
if errorlevel 1 (
  echo [ERROR] failed to build h264.mkv - check BILI_FFMPEG
  exit /b 1
)

rem VP9 + Opus: must be transcoded
"%BILI_FFMPEG%" -hide_banner -loglevel error -y -f lavfi -i "testsrc2=size=320x240:rate=25:duration=2" -f lavfi -i "sine=frequency=440:duration=2" -c:v libvpx-vp9 -b:v 200k -c:a libopus -shortest "%FIX%\vp9.webm"
if errorlevel 1 (
  echo [ERROR] failed to build vp9.webm - does this ffmpeg have libvpx-vp9 and libopus?
  exit /b 1
)

rem video only, no audio track
"%BILI_FFMPEG%" -hide_banner -loglevel error -y -f lavfi -i "testsrc2=size=320x240:rate=25:duration=2" -c:v libx264 -pix_fmt yuv420p -an "%FIX%\silent.mp4"
if errorlevel 1 (
  echo [ERROR] failed to build silent.mp4
  exit /b 1
)

echo === 2/4 wrap fake_engine.py ===
"%CSC%" /nologo /target:exe /platform:x64 /codepage:65001 /out:"%TMP%\fake_engine.exe" "%ROOT%\tests\EngineShim.cs"
if errorlevel 1 (
  echo [ERROR] failed to compile EngineShim
  exit /b 1
)
copy /y "%ROOT%\tests\fake_engine.py" "%TMP%\fake_engine.py" >nul
if errorlevel 1 (
  echo [ERROR] failed to copy fake_engine.py
  exit /b 1
)

echo === 3/4 compile tests ===
"%CSC%" /nologo /target:exe /platform:x64 /codepage:65001 /main:Tests /out:"%TMP%\tests.exe" %REF% %SRC% tests\Tests.cs tests\CompatibilityAudit.cs tests\ComponentChecks.cs tests\GuiSmoke.cs tests\LiveSmoke.cs
if errorlevel 1 (
  echo [ERROR] failed to compile the test program
  exit /b 1
)

echo === 4/4 run ===
"%TMP%\tests.exe" "%WORK%" "%TMP%\fake_engine.exe" "%BILI_FFMPEG%" "%BILI_FFPROBE%" "%FIX%"
set "CODE=%ERRORLEVEL%"
echo.
if "%CODE%"=="0" (echo ALL PASSED) else (echo FAILURES - see the FAIL lines above)
endlocal & exit /b %CODE%
