param([switch]$Test)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$dotnetExe = Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetExe)) {
    $installed = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($installed) { $dotnetExe = $installed.Source }
    else { throw 'Install .NET SDK 8 or newer, or put it into .tools\dotnet.' }
}
& $dotnetExe build Recording.csproj -c Release -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$destination = Join-Path $PSScriptRoot 'dist\Recording'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
foreach ($file in @('Recording.exe','Recording.exe.config')) {
    Copy-Item -LiteralPath (Join-Path 'bin\x64\Release\net48' $file) -Destination $destination -Force
}
Get-ChildItem -LiteralPath 'bin\x64\Release\net48' -Filter '*.dll' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $destination -Force }
$encoderPath = Join-Path $PSScriptRoot '.tools\ffmpeg\ffmpeg-9.0.2-essentials_build\bin\ffmpeg.exe'
if (-not (Test-Path -LiteralPath $encoderPath)) { throw 'FFmpeg 9.0.2 essentials build is required in .tools/ffmpeg. See third_party/FFmpeg.NOTICE.txt.' }
Copy-Item -LiteralPath $encoderPath -Destination $destination -Force
Copy-Item -LiteralPath $encoderPath -Destination 'bin\x64\Release\net48\ffmpeg.exe' -Force
Copy-Item -LiteralPath 'README.md' -Destination $destination -Force
Copy-Item -Path 'third_party\*.txt' -Destination $destination -Force
Copy-Item -LiteralPath 'LICENSE' -Destination $destination -Force
Copy-Item -LiteralPath 'THIRD_PARTY_NOTICES.md' -Destination $destination -Force
Copy-Item -LiteralPath 'install.ps1' -Destination $destination -Force
if ($Test) {
    $process = Start-Process -FilePath (Join-Path $PSScriptRoot 'bin\x64\Release\net48\Recording.exe') -ArgumentList '--self-test' -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw 'Integration test failed. See tests/results/integration.json and %LOCALAPPDATA%/Recording/app.log.' }
}
Write-Output "Ready: $destination\Recording.exe"
