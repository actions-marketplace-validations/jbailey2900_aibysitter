# Exits 0 when the URL returns the body 'Healthy' within the given attempts.
param(
    [Parameter(Mandatory = $true)] [string] $Url,
    [string] $Resolve,
    [int] $Attempts = 5,
    [int] $DelaySeconds = 5
)

$curlArgs = @('--silent', '--show-error', '--insecure', '--max-time', '30')
if ($Resolve) { $curlArgs += @('--resolve', $Resolve) }

for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
    $ErrorActionPreference = 'Continue'
    $body = & curl.exe @curlArgs $Url 2>&1 | Out-String
    $code = $LASTEXITCODE
    $body = $body.Trim()
    if ($code -eq 0 -and $body -eq 'Healthy') {
        Write-Host "Health check passed on attempt $attempt."
        exit 0
    }

    Write-Host "Attempt ${attempt}: curl exit $code, body '$body'"
    if ($attempt -lt $Attempts) { Start-Sleep -Seconds $DelaySeconds }
}

Write-Host "Health check failed: $Url did not return 'Healthy'."
exit 1
