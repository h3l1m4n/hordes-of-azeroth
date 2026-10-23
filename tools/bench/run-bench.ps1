param(
    [Parameter(Mandatory)][ValidateSet('warrior', 'paladin', 'hunter', 'rogue', 'priest', 'shaman', 'mage', 'warlock', 'druid')][string]$Class,
    [string[]]$Builds = @(),
    [string]$Account = 'player',
    [string]$Hero = '',
    [switch]$Dummies,
    [switch]$Melee,
    [switch]$NoAnchor,
    [switch]$Markdown,
    [switch]$Singles,
    [int]$Rank = 0,
    [string[]]$Passives = @(),
    [int]$Minute = 5,
    [int]$Level = 20,
    [string]$Tag = '',
    [int]$DummyCount = 10
)
. "$PSScriptRoot\..\common.ps1"
$Builds = @($Builds | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$Passives = @($Passives | ForEach-Object { $_ -split ',' } | Where-Object { $_ })

$ClassIds = @{ warrior = 1; paladin = 2; hunter = 3; rogue = 4; priest = 5; shaman = 7; mage = 8; warlock = 9; druid = 11 }
$AnchorKey = 'anchor'
$BuildSeconds = if ($Rank -gt 1) { 52 } else { 45 }
$QuitDelaySeconds = 3
$DummyRingYards = 7
$SetupSeconds = if ($Dummies) { 14 } else { 13 }
$EnterWaitSeconds = 10
$QuitMarginSeconds = 90

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
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
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
        $fields = @($line.Trim() -split '\s+')
        $idCount = 0
        while ($idCount -lt 4 -and 2 + $idCount -lt $fields.Count -and $fields[2 + $idCount] -match '^\d+$') { $idCount++ }
        if ($idCount -lt 1 -or $fields.Count -lt 3 + $idCount) { continue }
        [pscustomobject]@{ Class = $fields[0]; Key = $fields[1]; Ids = [int[]]$fields[2..(1 + $idCount)]; Label = $fields[(2 + $idCount)..($fields.Count - 1)] -join ' ' }
    }
    if ($Singles) {
        $ids = @($all | Where-Object { $_.Class -eq $Class -and $_.Key -ne $AnchorKey } | ForEach-Object { $_.Ids } | Sort-Object -Unique)
        return @($ids | Where-Object { $Builds.Count -eq 0 -or $Builds -contains "s$_" } | ForEach-Object {
            [pscustomobject]@{ Class = $Class; Key = "s$_"; Ids = [int[]]@($_); Label = "Spell $_ alone" }
        })
    }
    if ($NoAnchor) {
        return @($all | Where-Object { $_.Class -eq $Class -and ($Builds.Count -eq 0 -or $Builds -contains $_.Key) })
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
    $setupSteps = @('bench', "level $Level", 'quiet', "minute $Minute") + @($Passives | ForEach-Object { "passive $($_ -replace ':', ' ')" })
    if ($Dummies) { $setupSteps += 'deep' }
    $setup = ($setupSteps | ForEach-Object { '"' + $_ + '"' }) -join ', '
    $lines = @("BenchSetup = { $setup }", "BenchBuildSeconds = $BuildSeconds", '')
    if ($Dummies) { $lines += "BenchDummies = `"$DummyCount $DummyRingYards`"" }
    if ($Melee) { $lines += 'BenchPack = "pack 150 1"' }
    $lines += 'BenchBuilds = {'

    foreach ($build in $BuildList) {
        $stepList = @($build.Ids | ForEach-Object { $Spells[$_].Verb + ' ' + $_ })
        if ($Rank -gt 1) { $stepList += @($build.Ids | ForEach-Object { "maxrank $_ $Rank" }) }
        $steps = ($stepList | ForEach-Object { '"' + $_ + '"' }) -join ', '
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


function Send-Enter([System.Diagnostics.Process]$Game) {
    $hwnd = $Game.MainWindowHandle
    for ($try = 0; $try -lt 10 -and [Bench.Window]::GetForegroundWindow() -ne $hwnd; $try++) {
        [Bench.Window]::ShowWindow($hwnd, 9) | Out-Null
        [Bench.Window]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
        [Bench.Window]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
        [Bench.Window]::SetForegroundWindow($hwnd) | Out-Null
        Start-Sleep -Milliseconds 400
    }
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

function Wait-BenchDone([System.Diagnostics.Process]$Game, [long]$ChatStart, [int]$BuildCount) {
    $scheduleEnd = (Get-Date).AddSeconds($SetupSeconds + $BuildCount * $BuildSeconds + $QuitDelaySeconds + $QuitMarginSeconds)
    while ((Get-Date) -lt $scheduleEnd) {
        if ($Game.HasExited) { return }
        if (Test-ChatMarker 'bench done' $ChatStart) {
            Start-Sleep -Seconds $QuitDelaySeconds
            return
        }
        Start-Sleep -Seconds 1
    }
}

function Enter-World([long]$ChatStart) {
    $realmStart = if (Test-Path $realmLog) { (Get-Item $realmLog).Length } else { 0 }
    & (Join-Path $RepoRoot 'tools\launch-client.ps1') -Account $Account -Password $Account | Out-Null
    Wait-Until {
        (Test-Path $realmLog) -and (Get-Item $realmLog).Length -gt $realmStart -and
            ((Get-Content $realmLog -Tail 30 | Out-String) -match 'successfully authenticated')
    } 90 'the client to log in'
    $game = Get-GameProcess | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $game) { throw 'The game window did not open.' }
    Start-Sleep -Seconds 8
    Send-Enter $game
    Start-Sleep -Seconds $EnterWaitSeconds
    if ($game.HasExited) { throw 'The client closed before entering the world.' }
    $game
}

if (Get-GameProcess) { throw 'The game client is running. Close it (or wait for the session using it) first.' }
if (-not (Get-Process mangosd -ErrorAction SilentlyContinue)) { throw 'mangosd is not running (tools\start-server.ps1).' }

$buildList = Read-Builds
$spells = Get-Spells $buildList
$accountId = @(Invoke-Sql "SELECT id FROM account WHERE username = '$($Account.ToUpper())'" 'realmd')[0]
$heroFilter = if ($Hero) { "name = '$Hero'" } else { "class = $($ClassIds[$Class])" }
$hero = @(Invoke-Sql "SELECT name FROM characters WHERE account = $accountId AND $heroFilter" 'characters')[0]
if (-not $hero) { throw "Account $Account has no $(if ($Hero) { "hero named $Hero" } else { "$Class hero" })." }

Write-Host "Bench: $Class on $hero, builds $(($buildList | ForEach-Object { $_.Key }) -join ', ')"
$since = (Get-Date).ToString('MM/dd HH:mm:ss')
$lastHero = $null
$game = $null
try {
    if (Test-Path $addonTarget) { Remove-Item $addonTarget -Recurse -Force }
    Copy-Item $addonSource $addonTarget -Recurse
    Write-AddonBuilds $buildList $spells
    $lastHero = Set-LastHero $hero
    $chatStart = if (Test-Path $chatLog) { (Get-Item $chatLog).Length } else { 0 }
    $game = Enter-World $chatStart
    Wait-BenchDone $game $chatStart $buildList.Count
} finally {
    Get-GameProcess | Stop-Process -Force -ErrorAction SilentlyContinue
    if ($null -ne $lastHero) { Set-LastHero $lastHero | Out-Null }
    if (Test-Path $addonTarget) { Remove-Item $addonTarget -Recurse -Force }
}

Write-Manifest $buildList $spells $hero $since
$parseArgs = @((Join-Path $PSScriptRoot 'parse_bench.py'), $manifestFile)
if ($Markdown) { $parseArgs += '--markdown' }
$mode = if ($Dummies) { 'dummies' } elseif ($Melee) { 'melee' } else { 'pack' }
if ($Tag) { $mode = "$mode-$Tag" }
$parseArgs += @('--save', (Join-Path $PSScriptRoot 'results.json'), '--mode', $mode)
& python @parseArgs
