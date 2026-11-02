<#
.SYNOPSIS
    Deploys one m4d-api environment: infrastructure, the Search index role, and the code.

.DESCRIPTION
    Creates the resource group (m4d-api-test or m4d-api), deploys main.bicep with the matching
    .bicepparam file, grants the function's identity Search Index Data Reader on the index the
    environment's alias points at, then publishes and zip-deploys the function code and checks
    /v1/health.

    The infrastructure steps create role assignments, so they need Owner (or Contributor plus
    User Access Administrator) on the subscription. Routine code deploys don't: -SkipInfrastructure
    only needs rights to deploy to the function app, which is what a pipeline would use.

    Search indexes aren't ARM resources, so the index role can't live in Bicep. After an index
    version cutover, grant the role on the new index before repointing the alias; rerunning this
    script after the repoint also works, but leaves a window where the API gets 403s.

.PARAMETER Environment
    test or prod.

.PARAMETER SkipInfrastructure
    Deploy code only.

.PARAMETER SkipCode
    Deploy infrastructure only.

.EXAMPLE
    ./m4d-api/infra/deploy.ps1 -Environment test
#>
param(
    [Parameter(Mandatory)]
    [ValidateSet('test', 'prod')]
    [string]$Environment,

    [string]$Location = 'westus',

    [switch]$SkipInfrastructure,

    [switch]$SkipCode
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

# Resource group, function app, and baseName in the .bicepparam file share one name
$name = if ($Environment -eq 'prod') { 'm4d-api' } else { 'm4d-api-test' }
$alias = "songs-api-$Environment"
$searchService = 'music4dance'
$searchResourceGroup = 'm4d-Web'

$infra = $PSScriptRoot
$project = Split-Path $infra -Parent
$repoRoot = Split-Path $project -Parent

if (-not $SkipInfrastructure) {
    Write-Host "Deploying infrastructure to resource group $name"
    az group create --name $name --location $Location --output none

    # Test only: the person deploying can add keys by hand until the key issuing page exists
    $extraParameters = @()
    if ($Environment -eq 'test') {
        $me = az ad signed-in-user show --query id --output tsv
        $extraParameters = @('--parameters', "keyWriterUserIds=$me")
    }

    $outputs = az deployment group create --resource-group $name --name m4d-api `
        --template-file "$infra/main.bicep" --parameters "$infra/$Environment.bicepparam" `
        @extraParameters --query properties.outputs | ConvertFrom-Json

    Write-Host "Granting Search Index Data Reader on the index behind $alias"
    $adminKey = az search admin-key show --service-name $searchService --resource-group $searchResourceGroup `
        --query primaryKey --output tsv
    $aliasInfo = Invoke-RestMethod -Headers @{ 'api-key' = $adminKey } `
        -Uri "https://$searchService.search.windows.net/aliases/$($alias)?api-version=2026-04-01"
    $index = $aliasInfo.indexes[0]
    $searchId = az search service show --name $searchService --resource-group $searchResourceGroup `
        --query id --output tsv
    az role assignment create --assignee-object-id $outputs.functionPrincipalId.value `
        --assignee-principal-type ServicePrincipal --role 'Search Index Data Reader' `
        --scope "$searchId/indexes/$index" --output none
    Write-Host "  $alias -> $index"
}

if (-not $SkipCode) {
    Write-Host "Publishing and deploying code to $name"
    $publish = Join-Path $repoRoot 'local/m4d-api-publish'
    $zip = "$publish.zip"
    Remove-Item $publish, $zip -Recurse -Force -ErrorAction SilentlyContinue
    dotnet publish "$project/m4d-api.csproj" --configuration Release --output $publish
    Compress-Archive -Path "$publish/*" -DestinationPath $zip
    az functionapp deployment source config-zip --resource-group $name --name $name --src $zip --output none
}

# az functionapp show leaves defaultHostName empty for Flex apps; the resource itself has it
$hostName = az resource show --resource-group $name --name $name --resource-type Microsoft.Web/sites `
    --query properties.defaultHostName --output tsv
Write-Host "Checking https://$hostName/v1/health"
$response = Invoke-WebRequest -Uri "https://$hostName/v1/health" -SkipHttpErrorCheck
Write-Host "  $($response.StatusCode) $($response.Content)"
