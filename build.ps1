# Regenerates the Kill the Frog assets and scene headlessly (Unity must be closed).
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe"
$project = Join-Path $PSScriptRoot "KillTheFrog"
$log = Join-Path $PSScriptRoot "build.log"
Remove-Item $log -ErrorAction SilentlyContinue
Write-Host "Building... (log: $log)"
$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList @(
    '-batchmode', '-quit',
    '-projectPath', "`"$project`"",
    '-executeMethod', 'KillTheFrogBuilder.Build',
    '-logFile', "`"$log`"")
Write-Host "Unity exited with code $($p.ExitCode)"
Select-String -Path $log -Pattern 'error CS|\[KillTheFrog\]' | ForEach-Object { $_.Line }
