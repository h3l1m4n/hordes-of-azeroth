param([switch]$Force)
. "$PSScriptRoot\common.ps1"

$spec = Get-Content (Join-Path $ConfigDir 'client-patches.json') -Raw | ConvertFrom-Json
$source = Join-Path $RepoRoot $spec.source
$target = Join-Path $RepoRoot $spec.target
$sourceMd5 = $spec.sourceMd5
$patches = @($spec.patches | ForEach-Object {
    @{ Offset = [Convert]::ToInt32($_.offset, 16); Original = $_.original; Patched = $_.patched }
})

function ConvertTo-Bytes([string]$Hex) { [byte[]]($Hex -split ' ' | ForEach-Object { [Convert]::ToByte($_, 16) }) }

function Test-Bytes([byte[]]$Data, [int]$Offset, [byte[]]$Expected) {
    for ($i = 0; $i -lt $Expected.Length; $i++) {
        if ($Data[$Offset + $i] -ne $Expected[$i]) { return $false }
    }
    return $true
}

if ((Test-Path $target) -and -not $Force) {
    $existing = [System.IO.File]::ReadAllBytes($target)
    $patched = @($patches | Where-Object { Test-Bytes $existing $_.Offset (ConvertTo-Bytes $_.Patched) }).Count
    if ($patched -eq $patches.Count) {
        Write-Host "$($spec.target) is up to date"
        return
    }
    Write-Host "$($spec.target) is not patched as expected; rebuilding it"
}

if (-not (Test-Path $source)) { throw "$source not found." }
$md5 = (Get-FileHash $source -Algorithm MD5).Hash
if ($md5 -ne $sourceMd5) {
    throw "WoW.exe has MD5 $md5, expected $sourceMd5 (the enUS 1.12.1 build 5875 client). Not patching an unknown build."
}

$data = [System.IO.File]::ReadAllBytes($source)
foreach ($patch in $patches) {
    $original = ConvertTo-Bytes $patch.Original
    $replacement = ConvertTo-Bytes $patch.Patched
    if ($original.Length -ne $replacement.Length) {
        throw ('Patch at 0x{0:X6} replaces {1} bytes with {2}.' -f $patch.Offset, $original.Length, $replacement.Length)
    }
    if (-not (Test-Bytes $data $patch.Offset $original)) {
        throw ('WoW.exe does not contain {0} at 0x{1:X6}.' -f $patch.Original, $patch.Offset)
    }
    [Array]::Copy($replacement, 0, $data, $patch.Offset, $replacement.Length)
}

[System.IO.File]::WriteAllBytes($target, $data)
Write-Host "Wrote $target ($($patches.Count) patches)"
