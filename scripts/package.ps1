param(
    [Parameter(Mandatory = $true)]
    [string]$PlatformToolsPath,
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'AppBridge.csproj'
$projectVersion = ([xml](Get-Content -LiteralPath $projectFile -Raw)).Project.PropertyGroup.Version
if (-not $Version) { $Version = $projectVersion }
if ($Version -ne $projectVersion) { throw 'Version must match AppBridge.csproj and the application captions.' }
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw 'Version must contain three numeric components.'
}

$toolsRoot = (Resolve-Path -LiteralPath $PlatformToolsPath).Path
$toolFiles = @('adb.exe', 'AdbWinApi.dll', 'AdbWinUsbApi.dll', 'NOTICE.txt', 'source.properties')
foreach ($name in $toolFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $toolsRoot $name) -PathType Leaf)) {
        throw "Missing Platform-Tools file: $name"
    }
}

# A fresh staging directory prevents local backups or settings from entering a release.
$artifacts = Join-Path $projectRoot 'artifacts'
$staging = Join-Path $artifacts ("package-$Version-" + [guid]::NewGuid().ToString('N'))
$appFolder = Join-Path $staging "AndroidAppBridge-$Version"
New-Item -ItemType Directory -Path $appFolder -Force | Out-Null
& dotnet publish $projectFile -c Release -o $appFolder
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$destinationTools = Join-Path $appFolder 'platform-tools'
New-Item -ItemType Directory -Path $destinationTools | Out-Null
if (Test-Path -LiteralPath (Join-Path $toolsRoot 'package.xml') -PathType Leaf) { $toolFiles += 'package.xml' }
foreach ($name in $toolFiles) {
    Copy-Item -LiteralPath (Join-Path $toolsRoot $name) -Destination $destinationTools
}
foreach ($name in @('README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $appFolder
}

# List runtime files explicitly rather than archiving all publish or working files.
$runtimeFiles = @('AndroidAppBridge.exe', 'AndroidAppBridge.dll', 'AndroidAppBridge.deps.json', 'AndroidAppBridge.runtimeconfig.json', 'README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'platform-tools')
$packageFiles = $runtimeFiles | ForEach-Object { Join-Path $appFolder $_ }
$archive = Join-Path $artifacts "AndroidAppBridge-$Version-Windows.zip"
Compress-Archive -LiteralPath $packageFiles -DestinationPath $archive -Force
Write-Output "Package: $archive"
