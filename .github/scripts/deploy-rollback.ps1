# Restores PreviousPath into the site and re-checks health.
# Healthy: exit 0. Unhealthy or copy failed: app_offline.htm is left in place, exit 1.
param(
    [Parameter(Mandatory = $true)] [string] $SitePath,
    [Parameter(Mandatory = $true)] [string] $PreviousPath,
    [Parameter(Mandatory = $true)] [string] $HealthUrl,
    [string] $Resolve,
    [int] $Attempts = 5,
    [int] $DelaySeconds = 5,
    [int] $OfflineWaitSeconds = 5
)

$offline = Join-Path $SitePath 'app_offline.htm'
$offlineHtml = '<!DOCTYPE html><html><head><title>Aibysitter</title></head><body><p>Restoring the previous version. Back in a moment.</p></body></html>'

Set-Content -LiteralPath $offline -Encoding utf8 -Value $offlineHtml
Start-Sleep -Seconds $OfflineWaitSeconds

robocopy $PreviousPath $SitePath /MIR /XF app_offline.htm /XD logs /R:5 /W:2 /NP /NFL /NDL
$code = $LASTEXITCODE
if ($code -ge 8) {
    Write-Host "::error title=Rollback failed::robocopy exit code $code copying $PreviousPath to $SitePath."
    exit 1
}

Remove-Item -LiteralPath $offline -Force
& (Join-Path $PSScriptRoot 'health-check.ps1') -Url $HealthUrl -Resolve $Resolve -Attempts $Attempts -DelaySeconds $DelaySeconds
if ($LASTEXITCODE -ne 0) {
    Set-Content -LiteralPath $offline -Encoding utf8 -Value $offlineHtml
    Write-Host "::error title=Rollback unhealthy::Restored $PreviousPath, but /health did not return 'Healthy'."
    exit 1
}

Write-Host "::error title=Rolled back::The deploy failed; the previous version from $PreviousPath is restored and healthy."
exit 0
