# CivilGPT build helper.
# Cleans output folders, warns if Civil 3D is running (it locks the plugin DLL)
# and builds the plugin for x64.

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$acad = Get-Process -Name 'acad' -ErrorAction SilentlyContinue
if ($acad) {
    Write-Warning ("Civil 3D is running (PID: {0}). Close it before building: " -f (($acad | ForEach-Object { $_.Id }) -join ', '))
    Write-Warning 'Otherwise the plugin DLL stays locked and the build cannot overwrite it.'
}

foreach ($dir in @('bin', 'obj')) {
    $path = Join-Path $root $dir
    if (-not (Test-Path $path)) { continue }

    try {
        Remove-Item -Recurse -Force $path
    }
    catch {
        Write-Warning ("Cannot clean '{0}': {1}" -f $dir, $_.Exception.Message)
    }
}

dotnet build 'CivilGPT.csproj' -c $Configuration -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) {
    throw 'Build failed.'
}

$output = Join-Path $root ("bin\x64\{0}" -f $Configuration)
$plugin = Join-Path $output 'CivilGPT.dll'

Write-Host ''
Write-Host ("Output folder : {0}" -f $output)
Write-Host ("Plugin DLL    : {0}" -f $plugin)
Write-Host ''
Write-Host 'Load in Civil 3D: NETLOAD -> select CivilGPT.dll'
