param([switch]$RemoveAutostart, [switch]$NoStart)
$ErrorActionPreference = 'Stop'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ($RemoveAutostart) {
    Remove-ItemProperty -LiteralPath $runKey -Name Recording -ErrorAction SilentlyContinue
    Write-Output 'Autostart disabled.'
    return
}
$source = Join-Path $PSScriptRoot 'dist\Recording'
if (-not (Test-Path -LiteralPath (Join-Path $source 'Recording.exe')) -and (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Recording.exe'))) {
    $source = $PSScriptRoot
}
$destination = Join-Path $env:LOCALAPPDATA 'Programs\Recording'
if (-not (Test-Path -LiteralPath (Join-Path $source 'Recording.exe'))) { throw 'Run build.ps1 first.' }
$executable = Join-Path $destination 'Recording.exe'
if (Test-Path -LiteralPath $executable) {
    & (Join-Path $source 'Recording.exe') --quit
    $running = Get-Process Recording -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $executable }
    foreach ($process in $running) {
        if (-not $process.WaitForExit(30000)) { throw 'Recording is still finishing. Try installation again after it exits.' }
    }
}
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $destination -Force
New-Item -Path $runKey -Force | Out-Null
Set-ItemProperty -LiteralPath $runKey -Name Recording -Value ('"' + $executable + '" --tray')
if (-not $NoStart) { Start-Process -FilePath $executable -ArgumentList '--tray' -WindowStyle Hidden }
Write-Output "Installed and added to autostart: $executable"
