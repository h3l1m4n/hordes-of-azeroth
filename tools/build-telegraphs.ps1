. "$PSScriptRoot\common.ps1"

$source = Join-Path $PSScriptRoot 'telegraphs'
$binDir = Join-Path $source 'bin'
$exe    = Join-Path $binDir 'TelegraphKit.exe'
$csc    = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $csc)) { throw "The C# compiler was not found at $csc." }
if (Get-Process HordesOfAzeroth, WoW -ErrorAction SilentlyContinue) { throw 'Close the game first: it keeps the patch open.' }

New-Item -ItemType Directory -Force $binDir | Out-Null
$files = Get-ChildItem $source -Filter '*.cs' | ForEach-Object { $_.FullName }
& $csc -nologo -optimize '-r:System.Drawing.dll' "-out:$exe" @files
if ($LASTEXITCODE -ne 0) { throw 'TelegraphKit did not compile.' }

& $exe build $RepoRoot
if ($LASTEXITCODE -ne 0) { throw 'TelegraphKit could not build the patch.' }
