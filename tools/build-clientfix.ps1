. "$PSScriptRoot\common.ps1"

$source  = Join-Path $PSScriptRoot 'clientfix'
$build   = Join-Path $source 'build'
$dll     = Join-Path $RepoRoot 'HordesOfAzerothClientFix.dll'
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

Invoke-Native 'CMake configure' $cmake @('-S', $source, '-B', $build, '-A', 'Win32')
Invoke-Native 'CMake build' $cmake @('--build', $build, '--config', 'Release', '--', '/m', '/v:minimal')
if (Get-Process HordesOfAzeroth, WoW -ErrorAction SilentlyContinue) { throw 'The game was started while this built: close it and rerun this to install the DLL.' }
Copy-Item (Join-Path $build 'Release\HordesOfAzerothClientFix.dll') $dll -Force

if (-not (Test-Path $dllsTxt)) {
    Write-Warning 'No dlls.txt: extract VanillaFixes into the repository root (see README), then rerun this.'
} elseif (-not ((Get-Content $dllsTxt) -contains 'HordesOfAzerothClientFix.dll')) {
    Add-Content -Path $dllsTxt -Value 'HordesOfAzerothClientFix.dll' -Encoding Ascii
}
Write-Host "Built HordesOfAzerothClientFix into $dll"
