param(
    [string[]]$Classes = @(),
    [string[]]$Specs = @(),
    [switch]$NoCapstone,
    [switch]$BaseOnly,
    [switch]$EvolvedOnly,
    [int]$Tier = 2,
    [int]$Stage = 1,
    [int]$Rank = 3,
    [int]$Minute = 5,
    [int]$Training = 3,
    [int[]]$Items = @(902241, 902028, 902031),
    [string[]]$Cards = @(),
    [int[]]$Glyphs = @(),
    [string]$Tag = '',
    [switch]$Benilla,
    [switch]$Dummies
)
$Classes = @($Classes | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$Specs = @($Specs | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$runTag = if ($Tag) { $Tag } else { "ref-t$Tier-s$Stage" + $(if ($NoCapstone) { '-nocap' } else { '' }) }

foreach ($line in Get-Content (Join-Path $PSScriptRoot 'reference.txt')) {
    $class, $tree, $base, $evolved, $talentList = $line -split ' '
    if ($Classes.Count -and $class -notin $Classes) { continue }
    if ($Specs.Count -and $base -notin $Specs) { continue }
    $talents = @($talentList -split ',')
    if ($NoCapstone) { $talents = $talents[0..($talents.Count - 2)] }
    $builds = if ($BaseOnly) { @($base) } elseif ($EvolvedOnly) { @($evolved) } else { @($base, $evolved) }
    Write-Host "== $class $base ($tree)"
    & (Join-Path $PSScriptRoot 'run-bench.ps1') -Class $class -Builds $builds -NoAnchor -Waves:(-not $Dummies) -Dummies:$Dummies -DummyCrowd:$Dummies -DummyCount 20 -Tier $Tier -Stage $Stage -Rank $Rank -Minute $Minute `
        -Talents $talents -Training $Training -Items $Items -Cards $Cards -Glyphs $Glyphs -Benilla:$Benilla -Tag "$runTag-$tree" 2>&1 |
        Select-String 'k/s|error|Exception|throw'
}
