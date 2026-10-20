. "$PSScriptRoot\common.ps1"

function Merge-Config([string]$Name) {
    $dist      = Join-Path $RunDir "$Name.conf.dist"
    $overrides = Join-Path $ConfigDir "$Name.overrides.conf"
    $target    = Join-Path $RunDir "$Name.conf"

    $lines = [System.Collections.Generic.List[string]][System.IO.File]::ReadAllLines($dist)
    $applied = 0
    foreach ($line in [System.IO.File]::ReadAllLines($overrides)) {
        if ($line -notmatch '^\s*([\w.]+)\s*=') { continue }
        $key = $Matches[1]
        $pattern = '^\s*' + [regex]::Escape($key) + '\s*='
        $index = $lines.FindIndex([Predicate[string]]{ param($l) $l -match $pattern })
        if ($index -lt 0) { throw "$overrides sets '$key', which $dist does not define." }
        $lines[$index] = $line.Trim()
        $applied++
    }

    [System.IO.File]::WriteAllLines($target, $lines)
    Write-Host "$Name.conf: $applied override(s) applied"
}

New-Item -ItemType Directory -Force $LogDir | Out-Null
Merge-Config 'mangosd'
Merge-Config 'realmd'
