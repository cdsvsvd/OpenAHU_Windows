param(
    [string]$Dotnet = 'dotnet',
    [switch]$Integration,
    [string]$NuGetConfig
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$project = Join-Path $PSScriptRoot 'OpenAHU.Features.Verification.csproj'
$apiProject = Join-Path $repoRoot 'src/OpenAHU.Api/OpenAHU.Api.csproj'
$output = Join-Path $repoRoot '.dsh-tmp/verification'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$log = Join-Path $output 'verification.log'
function Run-Dotnet([string[]]$Arguments) {
    & $Dotnet @Arguments 2>&1 | Tee-Object -FilePath $log -Append | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet command failed (exit $LASTEXITCODE): $($Arguments[0])" }
}
Set-Content -LiteralPath $log -Value 'Member02 WPF verification (fixtures are fictional; integration database is isolated).'
$restoreArgs = @('restore', $project)
if ($NuGetConfig) { $restoreArgs += @('--configfile', $NuGetConfig) }
Run-Dotnet $restoreArgs
Run-Dotnet @('build', $project, '-c', 'Release', '--no-restore')
$verifier = Join-Path $PSScriptRoot 'bin/Release/net10.0-windows/OpenAHU.Features.Verification.dll'
Run-Dotnet @($verifier, '--render', (Join-Path $output 'screenshots'))
if ($Integration) {
    $restoreArgs = @('restore', $apiProject)
    if ($NuGetConfig) { $restoreArgs += @('--configfile', $NuGetConfig) }
    Run-Dotnet $restoreArgs
    Run-Dotnet @('build', $apiProject, '-c', 'Release', '--no-restore')
    $dotnetPath = (Get-Command $Dotnet -ErrorAction Stop).Source
    Run-Dotnet @($verifier, '--integration', (Join-Path $repoRoot 'src/OpenAHU.Api/bin/Release/net10.0/OpenAHU.Api.dll'), $dotnetPath, $output)
}
Write-Host "Verification log and screenshots: $output"
