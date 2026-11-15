param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$version = '9.0.2'
$expectedHash = '60F467265B1E312373DBCD92200C2618A74850F98D3D078E94296BB3FA2047BA'
$toolsDirectory = Join-Path $projectRoot '.tools'
$archive = Join-Path $toolsDirectory "ffmpeg-$version-essentials_build.zip"
$unpackDirectory = Join-Path $toolsDirectory 'ffmpeg'
$executable = Join-Path $unpackDirectory "ffmpeg-$version-essentials_build\bin\ffmpeg.exe"
$downloadUrl = "https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-$version-essentials_build.zip"

New-Item -ItemType Directory -Force -Path $toolsDirectory | Out-Null
if (-not (Test-Path -LiteralPath $archive)) {
    $legacyArchive = Join-Path $toolsDirectory 'ffmpeg.zip'
    if ((Test-Path -LiteralPath $legacyArchive) -and (Get-FileHash -LiteralPath $legacyArchive -Algorithm SHA256).Hash -eq $expectedHash) {
        Copy-Item -LiteralPath $legacyArchive -Destination $archive
    } else {
        Invoke-WebRequest -Uri $downloadUrl -OutFile $archive
    }
}
$actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
if ($actualHash -ne $expectedHash) { throw "FFmpeg archive checksum mismatch. Expected $expectedHash; received $actualHash. Archive retained for inspection." }
if (-not (Test-Path -LiteralPath $executable)) {
    New-Item -ItemType Directory -Force -Path $unpackDirectory | Out-Null
    Expand-Archive -LiteralPath $archive -DestinationPath $unpackDirectory -Force
}
Write-Output "FFmpeg $version archive verified: $executable"
