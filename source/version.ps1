# Single source of truth for the product version: Core.cs const Version.
# Regenerates AssemblyInfo.cs and keeps app.manifest / THIRD_PARTY.txt in sync
# (2026-10-24: the three had drifted; see 评估 notes in VALIDATION.txt).
param([string]$Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$corePath = Join-Path $PSScriptRoot 'Core.cs'
$text = [IO.File]::ReadAllText($corePath)
$m = [regex]::Match($text, 'internal const string Version="([0-9]+(?:\.[0-9]+){1,3})"')
if (-not $m.Success) { Write-Host 'ERROR: Version constant not found in Core.cs'; exit 1 }
$version = $m.Groups[1].Value
$parts = @($version.Split('.'))
while ($parts.Count -lt 4) { $parts += '0' }
$asm = ($parts[0..3] -join '.')
$assembly = @"
using System.Reflection;
using System.Runtime.Versioning;

[assembly: AssemblyTitle("BiliGreenDownloader")]
[assembly: AssemblyProduct("视频素材下载器")]
[assembly: AssemblyDescription("Windows 64 位 B 站 / YouTube 视频与音乐素材下载工具")]
[assembly: AssemblyVersion("$asm")]
[assembly: AssemblyFileVersion("$asm")]
[assembly: TargetFramework(".NETFramework,Version=v4.5",FrameworkDisplayName=".NET Framework 4.5")]
"@
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'AssemblyInfo.cs'), $assembly, $utf8)

$manifestPath = Join-Path $PSScriptRoot 'app.manifest'
$manifest = [IO.File]::ReadAllText($manifestPath)
$manifest = [regex]::Replace($manifest, '(assemblyIdentity version=")[^"]+(")', '${1}' + $asm + '${2}')
[IO.File]::WriteAllText($manifestPath, $manifest, $utf8)

$third = Join-Path $Root 'THIRD_PARTY.txt'
$thirdText = [IO.File]::ReadAllText($third)
$thirdText = [regex]::Replace($thirdText, '(?m)^BiliGreenDownloader [0-9.]+$', 'BiliGreenDownloader ' + $version)
[IO.File]::WriteAllText($third, $thirdText, $utf8)
Write-Host "Version $version -> AssemblyInfo.cs / app.manifest / THIRD_PARTY.txt"
