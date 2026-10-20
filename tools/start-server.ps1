param([switch]$Play, [switch]$DatabaseOnly)
. "$PSScriptRoot\common.ps1"

if (-not (Test-Path (Join-Path $RunDir 'mangosd.conf'))) { & "$PSScriptRoot\apply-config.ps1" }
New-Item -ItemType Directory -Force $LogDir | Out-Null

if (Test-DbUp) {
    Write-Host 'MariaDB already running'
} else {
    Write-Host 'Starting MariaDB...'
    Start-Quoted (Join-Path $MariaDir 'bin\mariadbd.exe') @(
        "--defaults-file=$(Join-Path $ConfigDir 'mariadb.ini')",
        "--basedir=$MariaDir",
        "--datadir=$MariaData"
    ) $MariaDir 'Hidden' | Out-Null
    Wait-Until { Test-DbUp } 60 'MariaDB'
}
if ($DatabaseOnly) { return }

if (Get-Process realmd -ErrorAction SilentlyContinue) {
    Write-Host 'realmd already running'
} else {
    Write-Host 'Starting realmd...'
    Start-Quoted (Join-Path $RunDir 'realmd.exe') @() $RunDir 'Minimized' | Out-Null
    Wait-Until { Test-PortOpen 3724 } 30 'realmd (port 3724)'
}

if (Get-Process mangosd -ErrorAction SilentlyContinue) {
    Write-Host 'mangosd already running'
} else {
    Write-Host 'Starting mangosd (world load takes a moment)...'
    $world = Start-Quoted (Join-Path $RunDir 'mangosd.exe') @() $RunDir 'Minimized'
    Wait-Until {
        if ($world.HasExited) { throw "mangosd exited with code $($world.ExitCode). See $LogDir\Server.log." }
        Test-PortOpen 8085
    } 300 'mangosd (port 8085)'
}

Write-Host 'Server ready on 127.0.0.1'
if ($Play) { & "$PSScriptRoot\launch-client.ps1" }
