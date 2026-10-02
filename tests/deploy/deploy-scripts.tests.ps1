# Tests health-check.ps1, deploy-snapshot.ps1 and deploy-rollback.ps1 against a local HttpListener standing in for the site.
# The listener returns 503 when app_offline.htm exists, 'Healthy' when healthy.txt exists, otherwise 500.
$ErrorActionPreference = 'Stop'

$scripts = (Resolve-Path (Join-Path $PSScriptRoot '..\..\.github\scripts')).Path
$root = Join-Path ([IO.Path]::GetTempPath()) "deploy-scripts-test-$PID"
$site = Join-Path $root 'site'
$previous = Join-Path $root 'previous'
$publish = Join-Path $root 'publish'
$port = 5199
$url = "http://127.0.0.1:$port/health"
$fast = @{ HealthUrl = $url; Attempts = 2; DelaySeconds = 1 }
$script:failures = 0

$server = Start-Job -ArgumentList $site, $port -ScriptBlock {
    param($site, $port)
    $listener = New-Object System.Net.HttpListener
    $listener.Prefixes.Add("http://127.0.0.1:$port/")
    $listener.Start()
    while ($true) {
        $context = $listener.GetContext()
        if (Test-Path -LiteralPath (Join-Path $site 'app_offline.htm')) { $status = 503; $body = 'Offline' }
        elseif (Test-Path -LiteralPath (Join-Path $site 'healthy.txt')) { $status = 200; $body = 'Healthy' }
        else { $status = 500; $body = 'Broken' }
        $bytes = [Text.Encoding]::UTF8.GetBytes($body)
        $context.Response.StatusCode = $status
        $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
        $context.Response.Close()
    }
}

function Check([bool] $ok, [string] $message) {
    if ($ok) { Write-Host "ok - $message" }
    else { Write-Host "::error title=Deploy scripts test::$message"; $script:failures++ }
}

function Reset-Folders {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
    foreach ($dir in $site, $previous, $publish) { New-Item -ItemType Directory -Path $dir | Out-Null }
    $env:GITHUB_OUTPUT = Join-Path $root 'github-output.txt'
    Set-Content -LiteralPath $env:GITHUB_OUTPUT -Value '' -NoNewline
}

function Write-File([string] $path, [string] $text) {
    New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
    Set-Content -LiteralPath $path -Value $text -NoNewline
}

function Read-File([string] $path) {
    if (Test-Path -LiteralPath $path) { return (Get-Content -LiteralPath $path -Raw) } else { return $null }
}

function Invoke-Script([string] $name, [hashtable] $parameters) {
    & (Join-Path $scripts $name) @parameters | Out-Host
    return $LASTEXITCODE
}

function Output-Lines { return @(Get-Content -LiteralPath $env:GITHUB_OUTPUT | Where-Object { $_ }) }

try {
    $ready = $false
    for ($i = 0; $i -lt 20 -and -not $ready; $i++) {
        Start-Sleep -Milliseconds 500
        $ErrorActionPreference = 'Continue'
        & curl.exe --silent --max-time 2 $url *> $null
        $ready = $LASTEXITCODE -eq 0
        $ErrorActionPreference = 'Stop'
    }
    Check $ready 'listener started'

    Write-Host '--- Healthy site: snapshot, failed deploy, rollback'
    Reset-Folders
    Write-File (Join-Path $site 'healthy.txt') 'ok'
    Write-File (Join-Path $site 'app.dll') 'v1'
    Write-File (Join-Path $site 'logs\site.log') 'log'
    Write-File (Join-Path $previous 'stale.dll') 'old'

    Check ((Invoke-Script 'health-check.ps1' @{ Url = $url; Attempts = 1 }) -eq 0) 'health check passes on a healthy site'
    Check ((Invoke-Script 'deploy-snapshot.ps1' (@{ SitePath = $site; PreviousPath = $previous } + $fast)) -eq 0) 'snapshot exits 0'
    Check ((Output-Lines) -contains 'available=true') 'snapshot reports available=true'
    Check ((Read-File (Join-Path $previous 'app.dll')) -eq 'v1') 'snapshot holds the live app.dll'
    Check (-not (Test-Path -LiteralPath (Join-Path $previous 'stale.dll'))) 'snapshot mirror removes stale files'
    Check (-not (Test-Path -LiteralPath (Join-Path $previous 'logs'))) 'snapshot excludes logs'

    Write-File (Join-Path $publish 'app.dll') 'v2'
    robocopy $publish $site /MIR /XF app_offline.htm /XD logs /NP /NFL /NDL | Out-Null
    Check ((Invoke-Script 'health-check.ps1' @{ Url = $url; Attempts = 2; DelaySeconds = 1 }) -eq 1) 'health check fails after a broken deploy'

    Check ((Invoke-Script 'deploy-rollback.ps1' (@{ SitePath = $site; PreviousPath = $previous; OfflineWaitSeconds = 0 } + $fast)) -eq 0) 'rollback exits 0'
    Check ((Read-File (Join-Path $site 'app.dll')) -eq 'v1') 'rollback restores app.dll'
    Check (Test-Path -LiteralPath (Join-Path $site 'healthy.txt')) 'rollback restores the healthy site'
    Check (-not (Test-Path -LiteralPath (Join-Path $site 'app_offline.htm'))) 'rollback removes app_offline.htm'
    Check ((Read-File (Join-Path $site 'logs\site.log')) -eq 'log') 'rollback keeps logs'

    Write-Host '--- Unhealthy site: existing snapshot kept'
    Reset-Folders
    Write-File (Join-Path $site 'app.dll') 'bad'
    Write-File (Join-Path $previous 'app.dll') 'good'
    Write-File (Join-Path $previous 'healthy.txt') 'ok'
    Check ((Invoke-Script 'deploy-snapshot.ps1' (@{ SitePath = $site; PreviousPath = $previous } + $fast)) -eq 0) 'snapshot exits 0 when the site is unhealthy'
    Check ((Output-Lines) -contains 'available=true') 'existing snapshot reports available=true'
    Check ((Read-File (Join-Path $previous 'app.dll')) -eq 'good') 'existing snapshot is unchanged'

    Write-Host '--- Unhealthy site, no snapshot'
    Reset-Folders
    Write-File (Join-Path $site 'app.dll') 'bad'
    Check ((Invoke-Script 'deploy-snapshot.ps1' (@{ SitePath = $site; PreviousPath = $previous } + $fast)) -eq 0) 'snapshot exits 0 with nothing to keep'
    Check ((Output-Lines) -contains 'available=false') 'empty snapshot reports available=false'

    Write-Host '--- Broken snapshot: rollback fails and leaves the site offline'
    Reset-Folders
    Write-File (Join-Path $site 'app.dll') 'v2'
    Write-File (Join-Path $previous 'app.dll') 'v1'
    Check ((Invoke-Script 'deploy-rollback.ps1' (@{ SitePath = $site; PreviousPath = $previous; OfflineWaitSeconds = 0 } + $fast)) -eq 1) 'rollback exits 1 when the restored site is unhealthy'
    Check (Test-Path -LiteralPath (Join-Path $site 'app_offline.htm')) 'app_offline.htm is left in place'
    Check ((Read-File (Join-Path $site 'app.dll')) -eq 'v1') 'restored files are in place'
}
finally {
    Stop-Job $server -ErrorAction SilentlyContinue
    Remove-Job $server -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
}

if ($script:failures -gt 0) {
    Write-Host "$($script:failures) check(s) failed."
    exit 1
}

Write-Host 'All checks passed.'
exit 0
