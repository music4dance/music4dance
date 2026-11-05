<#
.SYNOPSIS
    Issues, lists or revokes partner API keys by hand, straight in the partner-keys vault.

.DESCRIPTION
    The manual fallback from architecture/plans/partner-api-standalone-host.md: the web app's
    Partner API keys page is the normal way to issue keys, and this does the same vault writes
    for when that page isn't available. You need Key Vault Secrets Officer on the vault (deploy.ps1
    grants it to the person deploying the test environment).

    A new key is shown once, or written to -OutFile, and never stored: the vault holds only its
    SHA-256 hash, in secret key-{keyId} tagged with the client. Revoking disables that secret; the
    function drops it at its next refresh (within 5 minutes).

    The client's limits live in secret client-{clientId} as JSON. Pass -Name (and any limits) to
    create or replace it; a client without one gets the function's default limits.

.EXAMPLE
    ./m4d-api/infra/new-key.ps1 -Environment test -ClientId test-partner -Name 'Test partner' -OutFile local/test-key.txt

.EXAMPLE
    ./m4d-api/infra/new-key.ps1 -Environment test -List

.EXAMPLE
    ./m4d-api/infra/new-key.ps1 -Environment test -Revoke 7KQ2M9XA4PZT
#>
[CmdletBinding(DefaultParameterSetName = 'Issue')]
param(
    [Parameter(Mandatory)]
    [ValidateSet('test', 'prod')]
    [string]$Environment,

    [Parameter(Mandatory, ParameterSetName = 'Issue')]
    [ValidatePattern('^[a-z0-9][a-z0-9-]{1,39}$')]
    [string]$ClientId,

    [Parameter(ParameterSetName = 'Issue')]
    [string]$Name,

    [Parameter(ParameterSetName = 'Issue')]
    [double]$RatePerSecond,

    [Parameter(ParameterSetName = 'Issue')]
    [int]$Burst,

    [Parameter(ParameterSetName = 'Issue')]
    [long]$DailyCap,

    [Parameter(ParameterSetName = 'Issue')]
    [string]$Label,

    # Write the key here instead of showing it
    [Parameter(ParameterSetName = 'Issue')]
    [string]$OutFile,

    [Parameter(Mandatory, ParameterSetName = 'List')]
    [switch]$List,

    [Parameter(Mandatory, ParameterSetName = 'Revoke')]
    [string]$Revoke
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$vault = if ($Environment -eq 'prod') { 'm4d-api-keys' } else { 'm4d-api-keys-test' }
$prefix = if ($Environment -eq 'prod') { 'm4d_live_' } else { 'm4d_test_' }

if ($List) {
    az keyvault secret list --vault-name $vault --query '[].{name:name, client:tags.client, label:tags.label, enabled:attributes.enabled, created:attributes.created}' --output table
    return
}

if ($Revoke) {
    az keyvault secret set-attributes --vault-name $vault --name "key-$Revoke" --enabled false --output none
    Write-Host "Revoked $Revoke; the API drops it within 5 minutes"
    return
}

if ($Name) {
    $config = [ordered]@{ name = $Name }
    if ($PSBoundParameters.ContainsKey('RatePerSecond')) { $config.ratePerSecond = $RatePerSecond }
    if ($PSBoundParameters.ContainsKey('Burst')) { $config.burst = $Burst }
    if ($PSBoundParameters.ContainsKey('DailyCap')) { $config.dailyCap = $DailyCap }
    $config.status = 'active'
    $json = $config | ConvertTo-Json -Compress
    # az mangles quotes in --value on Windows, so pass the JSON through a file
    $configFile = New-TemporaryFile
    try {
        Set-Content -Path $configFile -Value $json -NoNewline
        az keyvault secret set --vault-name $vault --name "client-$ClientId" --file $configFile --output none
    }
    finally {
        Remove-Item $configFile
    }
    Write-Host "Set client-$ClientId to $json"
}

# Same format as m4d-api/PartnerKeyFormat.cs: prefix, 12-character keyId, 256-bit base64url secret
$alphabet = '23456789ABCDEFGHJKLMNPQRSTUVWXYZ'
$keyId = -join (1..12 | ForEach-Object { $alphabet[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($alphabet.Length)] })
$secret = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
$key = "$prefix$($keyId)_$secret"
$hash = [Convert]::ToHexStringLower([System.Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($key)))

$tags = @("client=$ClientId", 'issuedBy=new-key.ps1')
if ($Label) { $tags += "label=$Label" }
az keyvault secret set --vault-name $vault --name "key-$keyId" --value $hash --tags @tags --output none

if ($OutFile) {
    Set-Content -Path $OutFile -Value $key -NoNewline
    Write-Host "Issued key $keyId for $ClientId; written to $OutFile"
}
else {
    Write-Host "Issued key $keyId for $ClientId. Copy it now; it isn't stored anywhere:"
    Write-Host $key
}
Write-Host 'The API picks it up within 5 minutes (or at the next instance start).'
