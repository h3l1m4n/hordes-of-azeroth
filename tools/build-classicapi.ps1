. "$PSScriptRoot\common.ps1"

$Tag     = 'v1.15.16'
$repoUrl = 'https://github.com/brues-code/ClassicAPI.git'
$source  = Join-Path $RepoRoot 'vendor\ClassicAPI'
$dll     = Join-Path $RepoRoot 'ClassicAPI.dll'
$dllsTxt = Join-Path $RepoRoot 'dlls.txt'

if (Get-Process HordesOfAzeroth, WoW -ErrorAction SilentlyContinue) { throw 'Close the game first: it has the DLL loaded.' }

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) { throw 'Visual Studio 2022 Build Tools with the C++ workload are needed (see README).' }
$cmake = & $vswhere -all -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath |
    ForEach-Object { Join-Path $_ 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe' } |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $cmake) { throw 'Visual Studio 2022 Build Tools with the C++ workload are needed (see README).' }

function Invoke-Native([string]$What, [string]$Exe, [string[]]$Arguments) {
    $ErrorActionPreference = 'Continue'
    & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$What failed with exit code $LASTEXITCODE." }
}

if (-not (Test-Path $source)) {
    New-Item -ItemType Directory -Force (Split-Path $source) | Out-Null
    Invoke-Native 'git clone' 'git' @('clone', '--depth', '1', '--branch', $Tag, '--recurse-submodules', '--shallow-submodules', $repoUrl, $source)
} elseif (-not ((& git -C $source tag --points-at HEAD) -contains $Tag)) {
    Invoke-Native 'git fetch' 'git' @('-C', $source, 'fetch', '--depth', '1', 'origin', 'tag', $Tag)
    Invoke-Native 'git checkout' 'git' @('-C', $source, 'checkout', '--quiet', $Tag)
    Invoke-Native 'git submodule update' 'git' @('-C', $source, 'submodule', 'update', '--init', '--recursive', '--depth', '1')
}

$used = [System.IO.DriveInfo]::GetDrives() | ForEach-Object { $_.Name.Substring(0, 1) }
$letter = 'ZYXWVUTSRQPONM'.ToCharArray() | Where-Object { $used -notcontains [string]$_ } | Select-Object -First 1
if (-not $letter) { throw 'No free drive letter to build through.' }
$drive = "${letter}:"
Invoke-Native 'subst' 'subst' @($drive, $source)
try {
    $build = "$drive\build"
    if (Test-Path $build) { Remove-Item $build -Recurse -Force }
    Invoke-Native 'CMake configure' $cmake @('-S', "$drive/", '-B', $build, '-A', 'Win32')
    Invoke-Native 'CMake build' $cmake @('--build', $build, '--config', 'Release', '--', '/m', '/v:minimal')
    if (Get-Process HordesOfAzeroth, WoW -ErrorAction SilentlyContinue) { throw 'The game was started while this built: close it and rerun this to install the DLL.' }
    Copy-Item "$build\Release\ClassicAPI.dll" $dll -Force
} finally {
    & subst $drive /D
}

if (-not (Test-Path $dllsTxt)) {
    Write-Warning 'No dlls.txt: extract VanillaFixes into the repository root (see README), then rerun this.'
} elseif (-not ((Get-Content $dllsTxt) -contains 'ClassicAPI.dll')) {
    Add-Content -Path $dllsTxt -Value 'ClassicAPI.dll' -Encoding Ascii
}
Write-Host "Built ClassicAPI $Tag into $dll"
