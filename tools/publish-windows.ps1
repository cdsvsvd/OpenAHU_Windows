# 6号成员 - Windows x64 executable distribution.
# Run from repository root: pwsh -File ./tools/publish-windows.ps1
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $root 'artifacts'
$desktopOut = Join-Path $out '_desktop'
$apiOut = Join-Path $out '_api'
$package = Join-Path $out 'OpenAHU-win-x64'
$zip = Join-Path $out 'OpenAHU-win-x64.zip'
foreach($dir in @($desktopOut,$apiOut,$package)) {
    if(Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}
$desktop = Join-Path $root 'src/OpenAHU.Desktop/OpenAHU.Desktop.csproj'
$api = Join-Path $root 'src/OpenAHU.Api/OpenAHU.Api.csproj'
$publishArgs = @('-c','Release','-r','win-x64','--self-contained','true',
                 '-p:PublishSingleFile=true','-p:PublishTrimmed=false',
                 '-p:IncludeNativeLibrariesForSelfExtract=true')
& dotnet publish $desktop @publishArgs '-o' $desktopOut
if($LASTEXITCODE -ne 0) { throw 'WPF publish failed' }
& dotnet publish $api @publishArgs '-o' $apiOut
if($LASTEXITCODE -ne 0) { throw 'API publish failed' }
$desktopExe = Join-Path $desktopOut 'OpenAHU.Desktop.exe'
$apiExe = Join-Path $apiOut 'OpenAHU.Api.exe'
foreach($file in @($desktopExe,$apiExe)) {
    if(-not (Test-Path $file)) { throw "Missing published executable: $file" }
    Copy-Item $file $package -Force
}
Copy-Item (Join-Path $root 'deploy/appsettings.sqlite.json') (Join-Path $package 'appsettings.json')
Copy-Item (Join-Path $root 'deploy/appsettings.sqlserver.example.json') $package
Copy-Item (Join-Path $root 'deploy/SQLServer_配置指南.md') $package
Copy-Item (Join-Path $root 'sqlserver/01_Create_CampusDemo.sql') $package
Copy-Item (Join-Path $root 'sqlserver/02_Check_CampusDemo.sql') $package
if(Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -CompressionLevel Optimal -Force
Write-Host "Published EXE distribution: $zip"
Get-Item $zip | Select-Object Name,Length
