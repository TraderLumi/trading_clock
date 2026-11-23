# 一键构建：用系统自带 csc 编译，不装任何 SDK / NuGet
# 用法：
#   .\build.ps1            编译 + 跑测试 + 出预览图
#   .\build.ps1 -SkipTests 跳过测试

param([switch]$SkipTests)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "找不到 csc.exe: $csc" }

New-Item -ItemType Directory -Force -Path 'dist' | Out-Null
$common = @('/nologo', '/optimize+', '/codepage:65001')

Write-Host '[1/4] 编译引擎测试 ...' -ForegroundColor Cyan
& $csc @common /target:exe /out:dist\tests.exe `
    tests\EngineTests.cs src\TimeEngine.cs src\SessionRules.cs src\HolidayRules.cs
if ($LASTEXITCODE -ne 0) { throw "测试程序编译失败 ($LASTEXITCODE)" }

if (-not $SkipTests) {
    Write-Host '[2/4] 运行引擎测试 ...' -ForegroundColor Cyan
    & '.\dist\tests.exe'
    if ($LASTEXITCODE -ne 0) { throw "测试未通过 ($LASTEXITCODE)" }
} else {
    Write-Host '[2/4] 跳过测试' -ForegroundColor DarkGray
}

Write-Host '[3/4] 编译桌面小窗 ...' -ForegroundColor Cyan
& $csc @common /target:winexe /out:dist\TradingClock.exe `
    src\Program.cs src\WidgetForm.cs src\WidgetRenderer.cs `
    src\TimeEngine.cs src\SessionRules.cs src\HolidayRules.cs src\Config.cs `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll
if ($LASTEXITCODE -ne 0) { throw "主程序编译失败 ($LASTEXITCODE)" }

Write-Host '[4/4] 生成界面预览图 ...' -ForegroundColor Cyan
& $csc @common /target:exe /out:dist\preview.exe `
    tools\Preview.cs src\WidgetRenderer.cs src\TimeEngine.cs src\SessionRules.cs src\HolidayRules.cs `
    /r:System.Drawing.dll /r:System.Windows.Forms.dll
if ($LASTEXITCODE -ne 0) { throw "预览工具编译失败 ($LASTEXITCODE)" }
& '.\dist\preview.exe' 'preview' 2 | Out-Null

Copy-Item 'holidays.txt' 'dist\holidays.txt' -Force

$exe = Get-Item 'dist\TradingClock.exe'
Write-Host ''
Write-Host ('完成: {0}  ({1:N0} 字节)' -f $exe.FullName, $exe.Length) -ForegroundColor Green
Write-Host '双击 dist\TradingClock.exe 运行；预览图在 preview\'
