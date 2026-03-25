param(
    [ValidateSet('stable','beta')]
    [string]$Channel = 'stable',
    [string]$Owner = 'Codename-11',
    [string]$Repo = 'openclaw-windows-node',
    [string]$Arch,
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'

function Get-Arch {
    if ($Arch) { return $Arch }
    if ([Environment]::Is64BitOperatingSystem) {
        if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { return 'win-arm64' }
        return 'win-x64'
    }
    throw 'Unsupported architecture'
}

function Get-LatestReleaseAssetUrl($owner, $repo, $rid) {
    try {
        $release = Invoke-RestMethod -Headers @{ 'User-Agent' = 'OpenClawWindowsNodeInstaller' } -Uri "https://api.github.com/repos/$owner/$repo/releases/latest"
    } catch {
        throw "No GitHub release exists yet for $owner/$repo. Publish the first release before using the stable bootstrap installer."
    }

    $asset = $release.assets | Where-Object { $_.name -like "*${rid}.zip" } | Select-Object -First 1
    if (-not $asset) { throw "No matching ZIP asset found for $rid in latest release" }
    return $asset.browser_download_url
}

$rid = Get-Arch
$installDir = Join-Path $env:LOCALAPPDATA 'OpenClawTray'
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

if ($Channel -ne 'stable') {
    Write-Host 'Beta installer bootstrap is not automatic yet.' -ForegroundColor Yellow
    Write-Host 'Use the app Settings -> Updates -> Beta/dev build after installing the stable bootstrap once.' -ForegroundColor Yellow
}

$zipUrl = Get-LatestReleaseAssetUrl -owner $Owner -repo $Repo -rid $rid
$tmpZip = Join-Path $env:TEMP ("openclaw-tray-{0}.zip" -f [Guid]::NewGuid().ToString('N'))
$tmpDir = Join-Path $env:TEMP ("openclaw-tray-{0}" -f [Guid]::NewGuid().ToString('N'))

Write-Host "Downloading $zipUrl"
Invoke-WebRequest -Headers @{ 'User-Agent' = 'OpenClawWindowsNodeInstaller' } -Uri $zipUrl -OutFile $tmpZip
Expand-Archive -Path $tmpZip -DestinationPath $tmpDir -Force

Get-ChildItem -Path $installDir -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item -Path (Join-Path $tmpDir '*') -Destination $installDir -Recurse -Force

$exe = Get-ChildItem -Path $installDir -Filter 'OpenClaw.Tray.WinUI.exe' -Recurse | Select-Object -First 1
if (-not $exe) { throw 'Installed executable not found after extraction' }

Write-Host "Installed to: $installDir" -ForegroundColor Green
Write-Host "Executable: $($exe.FullName)" -ForegroundColor Green
Write-Host 'Next: open Settings and confirm updater owner/repo is Codename-11/openclaw-windows-node.' -ForegroundColor Cyan

if ($Launch) {
    Start-Process -FilePath $exe.FullName
}
