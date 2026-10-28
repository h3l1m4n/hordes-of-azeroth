param(
    [ValidateSet('all', 'server', 'client')][string]$Part = 'all',
    [string]$ClientSource,
    [string]$ServerSource,
    [string]$Out
)

. "$PSScriptRoot\common.ps1"

if (-not $ClientSource) { $ClientSource = Join-Path $RepoRoot 'client' }
if (-not $ServerSource) { $ServerSource = Join-Path $RepoRoot 'server\core' }
if (-not $Out) { $Out = Join-Path $RepoRoot 'release\build\linux' }

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker Desktop is needed for the Linux build.' }
docker info --format '{{.ServerVersion}}' 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Start Docker Desktop first.' }

New-Item -ItemType Directory -Force $Out | Out-Null

$ErrorActionPreference = 'Continue'
docker pull -q ubuntu:22.04 | Out-Null
docker run --rm `
    -v "${ClientSource}:/src/client:ro" `
    -v "${ServerSource}:/src/server:ro" `
    -v "$(Join-Path $PSScriptRoot 'linux'):/tools:ro" `
    -v "${Out}:/out" `
    -v hoa-linux-cache:/cache `
    -v hoa-linux-cargo:/root/.cargo `
    -v hoa-linux-rustup:/root/.rustup `
    ubuntu:22.04 bash /tools/build.sh $Part
if ($LASTEXITCODE -ne 0) { throw "Linux build failed with exit code $LASTEXITCODE." }
