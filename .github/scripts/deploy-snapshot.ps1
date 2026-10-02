# Mirrors the live site to PreviousPath when the site is healthy; otherwise keeps the existing snapshot.
# Writes available=true|false to GITHUB_OUTPUT: true when PreviousPath holds a snapshot.
param(
    [Parameter(Mandatory = $true)] [string] $SitePath,
    [Parameter(Mandatory = $true)] [string] $PreviousPath,
    [Parameter(Mandatory = $true)] [string] $HealthUrl,
    [string] $Resolve,
    [int] $Attempts = 3,
    [int] $DelaySeconds = 5
)

function Write-Output-Available([bool] $value) {
    if ($env:GITHUB_OUTPUT) { [IO.File]::AppendAllText($env:GITHUB_OUTPUT, "available=$($value.ToString().ToLowerInvariant())`n") }
}

$available = [bool](Get-ChildItem -LiteralPath $PreviousPath -Force -ErrorAction SilentlyContinue | Select-Object -First 1)

& (Join-Path $PSScriptRoot 'health-check.ps1') -Url $HealthUrl -Resolve $Resolve -Attempts $Attempts -DelaySeconds $DelaySeconds
if ($LASTEXITCODE -ne 0) {
    Write-Host "::warning title=Snapshot kept::The live site is not healthy, so $PreviousPath was not replaced."
    Write-Output-Available $available
    exit 0
}

robocopy $SitePath $PreviousPath /MIR /XF app_offline.htm /XD logs /R:5 /W:2 /NP /NFL /NDL
$code = $LASTEXITCODE
if ($code -ge 8) {
    Write-Host "::error title=Snapshot failed::robocopy exit code $code copying $SitePath to $PreviousPath."
    exit 1
}

Write-Host "Snapshot of $SitePath saved to $PreviousPath."
Write-Output-Available $true
exit 0
