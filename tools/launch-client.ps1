param(
    [string]$Account = 'player',
    [string]$Password = 'player',
    [string[]]$ClientArgs = @()
)
. "$PSScriptRoot\common.ps1"

& "$PSScriptRoot\patch-client.ps1"

function ConvertTo-LuaString([string]$Text) {
    '"' + ($Text -replace '\\', '\\' -replace '"', '\"') + '"'
}

$accountFile = Join-Path $RepoRoot 'Interface\GlueXML\BonkAccount.lua'
[System.IO.File]::WriteAllLines($accountFile, @(
    "BONK_ACCOUNT = $(ConvertTo-LuaString $Account);",
    "BONK_PASSWORD = $(ConvertTo-LuaString $Password);"
))

$statsFile = Join-Path $RepoRoot 'Interface\GlueXML\BonkStats.lua'
if (-not (Test-Path $statsFile)) {
    [System.IO.File]::WriteAllLines($statsFile, @('BONK_STATS = nil;'))
}

Add-Type -Namespace Bonk -Name Cpu -MemberDefinition '[DllImport("kernel32.dll")] public static extern bool IsProcessorFeaturePresent(uint feature);'
$turboBuild = if ([Bonk.Cpu]::IsProcessorFeaturePresent(40)) { 'avx' } elseif ([Bonk.Cpu]::IsProcessorFeaturePresent(38)) { 'sse' } else { $null }
$dllsTxt = Join-Path $RepoRoot 'dlls.txt'
$dllLines = @(if (Test-Path $dllsTxt) { Get-Content $dllsTxt | Where-Object { $_ -notmatch 'wow_turbo' -or $_ -match '^\s*#' } })
if ($turboBuild -and (Test-Path (Join-Path $RepoRoot "mods\wow_turbo\$turboBuild\wow_turbo.dll"))) { $dllLines += "mods\wow_turbo\$turboBuild\wow_turbo.dll" }
[System.IO.File]::WriteAllLines($dllsTxt, [string[]]$dllLines)

$cacheDir = Join-Path $RepoRoot 'WDB'
if (Test-Path $cacheDir) {
    Get-ChildItem $cacheDir -Filter '*.wdb' | Remove-Item -Force
}

$env:WOW_TURBO_SKIP = 'FloatingTextPlaceRect__509520,FloatingFrame_AnchorAtScreenPos__509ec0,CreateWorldText__6c73f0,WorldTextOwner__Destroy__6c6cd0,WorldTextOwner__Draw__6c6e00,WorldTextOwner__Update__6c6d40,WorldText__Release__6c86a0,WorldText__Update__6c7cc0'
$vanillaFixes = Join-Path $RepoRoot 'VanillaFixes.exe'
if (Test-Path $vanillaFixes) {
    Start-Quoted $vanillaFixes (@('HordesOfAzeroth.exe') + $ClientArgs) $RepoRoot | Out-Null
    Write-Host "Started HordesOfAzeroth.exe through VanillaFixes; it logs in as $Account"
} else {
    Start-Quoted (Join-Path $RepoRoot 'HordesOfAzeroth.exe') $ClientArgs $RepoRoot | Out-Null
    Write-Host "Started HordesOfAzeroth.exe; it logs in as $Account"
}
