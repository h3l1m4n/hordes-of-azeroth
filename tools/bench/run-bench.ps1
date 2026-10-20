param(
    [Parameter(Mandatory)][ValidateSet('warrior', 'paladin', 'hunter', 'rogue', 'priest', 'shaman', 'mage', 'warlock', 'druid')][string]$Class,
    [string[]]$Builds = @(),
    [string]$Account = 'player',
    [switch]$Markdown
)
. "$PSScriptRoot\..\common.ps1"

$ClassIds = @{ warrior = 1; paladin = 2; hunter = 3; rogue = 4; priest = 5; shaman = 7; mage = 8; warlock = 9; druid = 11 }
$AnchorKey = 'anchor'
$BuildSeconds = 45
$StartupSeconds = 120
$EnterTries = 3

$mariadb = Join-Path $MariaDir 'bin\mariadb.exe'
$addonSource = Join-Path $PSScriptRoot 'BenchTest'
$addonTarget = Join-Path $RepoRoot 'Interface\AddOns\BenchTest'
$statsFile = Join-Path $RepoRoot 'Interface\GlueXML\BonkStats.lua'
$chatLog = Join-Path $RepoRoot 'Logs\WoWChatLog.txt'
$realmLog = Join-Path $LogDir 'Realmd.log'
$manifestFile = Join-Path $env:TEMP "bench-$Class.json"

Add-Type -Namespace Bench -Name Window -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
'@

function Invoke-Sql([string]$Sql, [string]$Database) {
    $rows = & $mariadb @DbRootArgs -N -B -e $Sql $Database
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $Sql" }
    @($rows)
}

function Get-GameProcess {
    Get-Process HordesOfAzeroth, VanillaBonk, WoW -ErrorAction SilentlyContinue
}

function Read-Builds {
    $all = foreach ($line in Get-Content (Join-Path $PSScriptRoot 'builds.txt')) {
        $fields = $line.Trim() -split '\s+', 7
        if ($fields.Count -lt 7) { continue }
        [pscustomobject]@{ Class = $fields[0]; Key = $fields[1]; Ids = [int[]]$fields[2..5]; Label = $fields[6] }
    }
    $anchor = $all | Where-Object { $_.Class -eq 'warrior' -and $_.Key -eq $AnchorKey } | Select-Object -First 1
    if (-not $anchor) { throw "builds.txt has no warrior $AnchorKey row." }
    $chosen = @($all | Where-Object { $_.Class -eq $Class -and $_.Key -ne $AnchorKey -and ($Builds.Count -eq 0 -or $Builds -contains $_.Key) })
    @($anchor) + $chosen
}

function Get-Spells([object[]]$BuildList) {
    $ids = ($BuildList | ForEach-Object { $_.Ids } | Sort-Object -Unique) -join ','
    $spells = @{}
    foreach ($row in Invoke-Sql "SELECT id, evolves_from, name FROM bonk_weapon WHERE id IN ($ids)" 'mangos') {
        $id, $from, $name = $row -split "`t", 3
        $spells[[int]$id] = [pscustomobject]@{ Id = [int]$id; Verb = $(if ([int]$from) { 'evolve' } else { 'spell' }); Name = $name }
    }
    foreach ($id in $ids -split ',') { if (-not $spells.ContainsKey([int]$id)) { throw "Spell $id is not in bonk_weapon." } }
    $spells
}

function Write-AddonBuilds([object[]]$BuildList, [hashtable]$Spells) {
    $lines = @('BenchSetup = { "bench", "level 20", "quiet", "minute 5" }', '', 'BenchBuilds = {')
    foreach ($build in $BuildList) {
        $steps = ($build.Ids | ForEach-Object { '"' + $Spells[$_].Verb + ' ' + $_ + '"' }) -join ', '
        $lines += "    { name = `"$($build.Key)`", steps = { $steps } },"
    }
    $lines += '}'
    [System.IO.File]::WriteAllLines((Join-Path $addonTarget 'BenchBuilds.lua'), [string[]]$lines)
}

function Write-Manifest([object[]]$BuildList, [hashtable]$Spells, [string]$Hero, [string]$Since) {
    $manifest = [ordered]@{
        since = $Since
        date = (Get-Date).ToString('yyyy-MM-dd')
        class = $Class
        hero = $Hero
        builds = @($BuildList | ForEach-Object {
            [ordered]@{ key = $_.Key; label = $_.Label; spells = @($_.Ids | ForEach-Object { [ordered]@{ id = $_; name = $Spells[$_].Name } }) }
        })
    }
    [System.IO.File]::WriteAllText($manifestFile, ($manifest | ConvertTo-Json -Depth 6))
}

function Set-LastHero([string]$Name) {
    $text = [System.IO.File]::ReadAllText($statsFile)
    $match = [regex]::Match($text, 'lastHero = "([^"]*)"')
    if (-not $match.Success) { throw "BonkStats.lua has no lastHero." }
    [System.IO.File]::WriteAllText($statsFile, $text.Remove($match.Groups[1].Index, $match.Groups[1].Length).Insert($match.Groups[1].Index, $Name))
    $match.Groups[1].Value
}

function Set-GmLevel([int]$Level) {
    Invoke-Sql "UPDATE account SET gmlevel = $Level WHERE username = '$($Account.ToUpper())'" 'realmd' | Out-Null
}

function Send-Enter([System.Diagnostics.Process]$Game) {
    $hwnd = $Game.MainWindowHandle
    [Bench.Window]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Milliseconds 400
    if ([Bench.Window]::GetForegroundWindow() -ne $hwnd) { throw 'The game window did not come to the front (someone is using the PC?). Stopping.' }
    [Bench.Window]::keybd_event(0x0D, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [Bench.Window]::keybd_event(0x0D, 0, 2, [UIntPtr]::Zero)
}

function Test-ChatMarker([string]$Marker, [long]$From) {
    if (-not (Test-Path $chatLog)) { return $false }
    $stream = [System.IO.File]::Open($chatLog, 'Open', 'Read', 'ReadWrite')
    try {
        if ($stream.Length -le $From) { return $false }
        $stream.Seek($From, 'Begin') | Out-Null
        (New-Object System.IO.StreamReader($stream)).ReadToEnd().Contains(": $Marker")
    } finally {
        $stream.Close()
    }
}

function Enter-World {
    $chatStart = if (Test-Path $chatLog) { (Get-Item $chatLog).Length } else { 0 }
    $realmStart = if (Test-Path $realmLog) { (Get-Item $realmLog).Length } else { 0 }
    & (Join-Path $RepoRoot 'tools\launch-client.ps1') -Account $Account -Password $Account | Out-Null
    Wait-Until {
        (Test-Path $realmLog) -and (Get-Item $realmLog).Length -gt $realmStart -and
            ((Get-Content $realmLog -Tail 30 | Out-String) -match 'successfully authenticated')
    } 90 'the client to log in'
    $game = Get-GameProcess | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $game) { throw 'The game window did not open.' }
    Start-Sleep -Seconds 8
    for ($try = 1; $try -le $EnterTries; $try++) {
        Send-Enter $game
        $deadline = (Get-Date).AddSeconds(20)
        while ((Get-Date) -lt $deadline) {
            if (Test-ChatMarker 'bench start' $chatStart) { return $game }
            if ($game.HasExited) { throw 'The client closed before entering the world.' }
            Start-Sleep -Milliseconds 500
        }
    }
    throw "The hero did not enter the world after $EnterTries Enter presses."
}

if (Get-GameProcess) { throw 'The game client is running. Close it (or wait for the session using it) first.' }
if (-not (Get-Process mangosd -ErrorAction SilentlyContinue)) { throw 'mangosd is not running (tools\start-server.ps1).' }

$buildList = Read-Builds
$spells = Get-Spells $buildList
$accountId = (Invoke-Sql "SELECT id FROM account WHERE username = '$($Account.ToUpper())'" 'realmd')[0]
$hero = (Invoke-Sql "SELECT name FROM characters WHERE account = $accountId AND class = $($ClassIds[$Class])" 'characters')[0]
if (-not $hero) { throw "Account $Account has no $Class hero." }
$gmBefore = [int](Invoke-Sql "SELECT gmlevel FROM account WHERE id = $accountId" 'realmd')[0]

Write-Host "Bench: $Class on $hero, builds $(($buildList | ForEach-Object { $_.Key }) -join ', ')"
$since = (Get-Date).ToString('MM/dd HH:mm:ss')
$lastHero = $null
$game = $null
try {
    if (Test-Path $addonTarget) { Remove-Item $addonTarget -Recurse -Force }
    Copy-Item $addonSource $addonTarget -Recurse
    Write-AddonBuilds $buildList $spells
    $lastHero = Set-LastHero $hero
    Set-GmLevel 3
    $game = Enter-World
    $limit = $StartupSeconds + $buildList.Count * $BuildSeconds
    if (-not $game.WaitForExit($limit * 1000)) { throw "The client was still running after $limit s." }
} finally {
    if ($game -and -not $game.HasExited) { Stop-Process -Id $game.Id -Force }
    Set-GmLevel $gmBefore
    if ($null -ne $lastHero) { Set-LastHero $lastHero | Out-Null }
    if (Test-Path $addonTarget) { Remove-Item $addonTarget -Recurse -Force }
}

Write-Manifest $buildList $spells $hero $since
$parseArgs = @((Join-Path $PSScriptRoot 'parse_bench.py'), $manifestFile)
if ($Markdown) { $parseArgs += '--markdown' }
& python @parseArgs
