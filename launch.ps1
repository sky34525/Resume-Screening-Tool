# ResumeScreening 一键启动脚本
$ErrorActionPreference = 'Stop'
$apiPort = 52081
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiDir = "$projectRoot\publish\api"
$desktopProj = "$projectRoot\src\ResumeScreening.Desktop\ResumeScreening.Desktop.csproj"

Write-Host '==== ResumeScreening ====' -ForegroundColor Cyan

Write-Host "`n[1/4] 清理旧进程..." -ForegroundColor Yellow
Get-Process -Name 'ResumeScreening.Api' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process -Name 'ResumeScreening.Desktop' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Write-Host '  已清理' -ForegroundColor Green

Write-Host "`n[2/4] 检查发布文件..." -ForegroundColor Yellow
if (-not (Test-Path "$apiDir\ResumeScreening.Api.exe")) {
    Write-Host '  未找到 API 发布文件，请先执行 dotnet publish' -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $desktopProj)) {
    Write-Host '  未找到桌面应用项目文件' -ForegroundColor Red
    exit 1
}
Write-Host '  发布文件就绪' -ForegroundColor Green

Write-Host "`n[3/4] 启动 API 后端..." -ForegroundColor Yellow
$apiProc = Start-Process -FilePath "$apiDir\ResumeScreening.Api.exe" -WorkingDirectory $apiDir -WindowStyle Hidden -PassThru
Write-Host "  API 已启动 (PID: $($apiProc.Id))" -ForegroundColor Green

Write-Host "`n[4/4] 等待 API 就绪..." -ForegroundColor Yellow
for ($i = 1; $i -le 15; $i++) {
    try {
        Invoke-WebRequest -Uri "http://localhost:$apiPort/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop | Out-Null
        Write-Host "  API 已就绪 (耗时 $i 秒)" -ForegroundColor Green
        break
    } catch {
        if ($i -eq 15) { 
            Write-Host '  API 启动超时' -ForegroundColor Red
            Stop-Process -Name 'ResumeScreening.Api' -Force -ErrorAction SilentlyContinue
            exit 1 
        }
        Write-Host "  等待中... ($i/15)" -ForegroundColor Gray
        Start-Sleep -Seconds 1
    }
}

Write-Host "`n启动桌面应用..." -ForegroundColor Cyan
$desktopProjectDir = Split-Path $desktopProj -Parent
Start-Process -FilePath "dotnet" -ArgumentList "run --no-build -c Release -p:Platform=x64" -WorkingDirectory $desktopProjectDir -WindowStyle Normal
Write-Host '桌面应用已启动' -ForegroundColor Green
Write-Host "`nAPI 地址: http://localhost:$apiPort" -ForegroundColor Gray