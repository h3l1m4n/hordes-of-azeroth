. "$PSScriptRoot\common.ps1"

foreach ($name in 'mangosd', 'realmd') {
    foreach ($proc in Get-Process $name -ErrorAction SilentlyContinue) {
        Write-Host "Stopping $name..."
        if (-not $proc.CloseMainWindow() -or -not $proc.WaitForExit(20000)) {
            Write-Host "$name did not exit in time, killing it"
            $proc.Kill()
            $proc.WaitForExit()
        }
    }
}

if (Test-DbUp) {
    Write-Host 'Stopping MariaDB...'
    & (Join-Path $MariaDir 'bin\mariadb-admin.exe') @DbRootArgs shutdown
    if ($LASTEXITCODE -ne 0) { throw "mariadb-admin shutdown failed with exit code $LASTEXITCODE." }
}

Write-Host 'Server stopped'
