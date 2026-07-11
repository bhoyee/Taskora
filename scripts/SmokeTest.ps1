param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$BaseUrl,

    [int]$TimeoutSeconds = 20
)

$ErrorActionPreference = "Stop"

$normalizedBaseUrl = $BaseUrl.TrimEnd("/")
$endpoints = @(
    "/health/live",
    "/health/ready"
)

foreach ($endpoint in $endpoints) {
    $uri = "$normalizedBaseUrl$endpoint"
    Write-Host "Checking $uri"

    $response = Invoke-WebRequest `
        -Uri $uri `
        -UseBasicParsing `
        -TimeoutSec $TimeoutSeconds

    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "Smoke test failed for $uri with status code $($response.StatusCode)."
    }
}

Write-Host "Smoke tests passed for $normalizedBaseUrl"
