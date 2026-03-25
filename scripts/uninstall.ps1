$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA 'OpenClawTray'

Get-Process OpenClaw.Tray.WinUI -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

if (Test-Path $installDir) {
    Remove-Item -Recurse -Force $installDir
    Write-Host "Removed $installDir" -ForegroundColor Green
} else {
    Write-Host 'Nothing to remove.' -ForegroundColor Yellow
}
