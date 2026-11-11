# CivilGPT build helper.
# Cleans output folders and builds the plugin for x64.
#
# Switch -CloseAcad closes Civil 3D when it holds the plugin DLL. Before closing it
# checks every open drawing for unsaved changes and refuses to continue if any is dirty,
# so nothing is lost.

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$CloseAcad
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

function Get-UnsavedDrawings {
    param($Application)

    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        try {
            $documents = $Application.Documents
            $count = [int]$documents.Count

            $unsaved = @()
            for ($i = 0; $i -lt $count; $i++) {
                $document = $documents.Item($i)
                if (-not $document.Saved) { $unsaved += $document.Name }
            }

            # Запятая сохраняет пустой массив: иначе он превращается в $null.
            return ,$unsaved
        }
        catch {
            Start-Sleep -Milliseconds 1500
        }
    }

    return $null
}

function Close-Civil3D {
    $process = Get-Process -Name 'acad' -ErrorAction SilentlyContinue
    if (-not $process) {
        Write-Host 'Civil 3D не запущен.'
        return $true
    }

    Write-Host ("Civil 3D запущен (PID: {0}). Пробую закрыть." -f (($process | ForEach-Object { $_.Id }) -join ', '))

    $application = $null
    try {
        $application = [Runtime.InteropServices.Marshal]::GetActiveObject('AutoCAD.Application')
    }
    catch {
        $application = $null
    }

    if ($null -eq $application) {
        Write-Warning 'Не удалось подключиться к запущенному Civil 3D (COM недоступен).'
        Write-Warning 'Закройте его вручную, иначе сборка не сможет заменить DLL.'
        return $false
    }

    $unsaved = Get-UnsavedDrawings -Application $application

    if ($null -eq $unsaved) {
        Write-Warning 'Не удалось проверить сохранность чертежей — закрывать не буду.'
        return $false
    }

    if ($unsaved.Count -gt 0) {
        Write-Warning ('Есть несохранённые чертежи: {0}' -f ($unsaved -join ', '))
        Write-Warning 'Не закрываю, чтобы не потерять изменения. Сохраните и повторите.'
        return $false
    }

    try {
        $application.Quit()
    }
    catch {
        Write-Warning ('Штатное закрытие не удалось: {0}' -f $_.Exception.Message)
    }

    for ($i = 0; $i -lt 15; $i++) {
        Start-Sleep -Milliseconds 400
        if (-not (Get-Process -Name 'acad' -ErrorAction SilentlyContinue)) {
            Write-Host 'Civil 3D закрыт.'
            return $true
        }
    }

    Write-Warning 'Civil 3D не закрылся штатно, завершаю процесс.'
    try {
        Stop-Process -Name 'acad' -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 800
    }
    catch {
        Write-Warning ('Не удалось завершить процесс: {0}' -f $_.Exception.Message)
    }

    $still = Get-Process -Name 'acad' -ErrorAction SilentlyContinue
    if ($still) {
        Write-Warning 'Civil 3D всё ещё запущен.'
        return $false
    }

    Write-Host 'Civil 3D закрыт.'
    return $true
}

$acad = Get-Process -Name 'acad' -ErrorAction SilentlyContinue

if ($acad) {
    if ($CloseAcad) {
        if (-not (Close-Civil3D)) {
            throw 'Civil 3D не закрыт — сборка отменена, чтобы не оставить старую DLL.'
        }
    }
    else {
        Write-Warning ("Civil 3D is running (PID: {0}). Close it before building: " -f (($acad | ForEach-Object { $_.Id }) -join ', '))
        Write-Warning 'Otherwise the plugin DLL stays locked and the build cannot overwrite it.'
        Write-Warning 'Use -CloseAcad to close it automatically (unsaved drawings are protected).'
    }
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
