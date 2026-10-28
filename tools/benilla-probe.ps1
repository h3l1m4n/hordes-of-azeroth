param(
    [string]$Lua = 'local w, n, u, m = UnitPosition("player"); ProbeLog("UnitPosition " .. tostring(w) .. " " .. tostring(n) .. " " .. tostring(u) .. " map " .. tostring(m) .. " facing " .. tostring(GetPlayerFacing and GetPlayerFacing()))',
    [int]$ExitAfter = 60,
    [int]$RunLuaAt = 30,
    [string]$Shot = '',
    [string]$Keys = '',
    [double]$ShotAt = 0,
    [int]$ShotCount = 1,
    [double]$ShotEvery = 1,
    [string]$Target = 'target',
    [string]$Char = ''
)
$ErrorActionPreference = 'Stop'

$client = Join-Path (Split-Path -Parent $PSScriptRoot) 'client'
$exe = Join-Path $client "$Target\release\HordesOfAzeroth.exe"
if (-not (Test-Path $exe)) { throw 'Build first: cargo build --release -p hoa (in client\).' }
$log = Join-Path $client 'benilla-config\probe.log'

$identity = Join-Path $client '.probe-identity'
if (-not (Test-Path $identity)) { throw 'client\.probe-identity is missing: WOW_USER=, WOW_PASS=, WOW_CHAR= of the test account.' }
foreach ($line in Get-Content $identity) {
    if ($line -match '^(WOW_USER|WOW_PASS|WOW_CHAR)=(.*)$') { Set-Item "env:$($Matches[1])" $Matches[2] }
}
if (-not $env:WOW_USER -or -not $env:WOW_PASS) { throw 'client\.probe-identity names no account.' }
if ($Char) { $env:WOW_CHAR = $Char }

$env:BENILLA_HOME = Join-Path $client 'benilla-config'
$env:WOW_UNATTENDED = '1'
if (-not $env:PROBE_SOUND) { $env:WOW_NOSOUND = '1' } else { Remove-Item Env:WOW_NOSOUND -ErrorAction SilentlyContinue }
$env:WOW_PROBE_EXIT_AT = "$ExitAfter"
$env:WOW_PROBE_LUA = $Lua
$env:WOW_PROBE_LUA_AT = "$RunLuaAt"
$env:WOW_PROBE_KEY = $Keys
if ($Shot) {
    $env:WOW_LIVE_SHOT = $Shot
    $env:WOW_LIVE_SHOT_AT = if ($ShotAt -gt 0) { "$ShotAt" } else { "$($RunLuaAt + 2)" }
    $env:WOW_LIVE_SHOT_COUNT = "$ShotCount"
    $env:WOW_LIVE_SHOT_EVERY = "$ShotEvery"
}

Push-Location $client
try {
    $ErrorActionPreference = 'Continue'
    & cmd /c "`"$exe`" > `"$log`" 2>&1"
} finally {
    Pop-Location
}
Get-Content $log | ForEach-Object { $_ -replace '\x1b\[[0-9;]*m', '' } |
    Select-String 'probe-log:|ERROR|Hordes of Azeroth|parked at|lua error' |
    Select-Object -First 30 | ForEach-Object { $_.Line.Substring([Math]::Min(28, $_.Line.Length)) }
