// Infrastructure for one m4d-api environment (test or prod), deployed into its own resource
// group by deploy.ps1. See architecture/plans/partner-api-standalone-host.md.
//
// Not covered here: the Search Index Data Reader role on the index behind the alias. Search
// indexes aren't ARM resources, so Bicep can't scope a role assignment to one; deploy.ps1 does it.

@description('Base name for the environment, e.g. m4d-api-test')
param baseName string

@description('Storage account name (3-24 lowercase letters and digits, globally unique)')
param storageAccountName string

@description('Partner-keys Key Vault name (globally unique)')
param keyVaultName string

param location string = resourceGroup().location

@description('Search service the API reads from')
param searchServiceName string

@description('Index alias the API queries, so index cutovers are an alias repoint')
param searchIndexAlias string

@description('Web app that issues partner keys; gets Key Vault Secrets Officer on the vault')
param keyWriterWebAppName string

param keyWriterWebAppResourceGroup string

@description('Comma-separated object ids of users who can add or revoke keys by hand; gets Key Vault Secrets Officer')
param keyWriterUserIds string = ''

param maximumInstanceCount int = 2

param instanceMemoryMB int = 2048

var deploymentContainer = 'app-package'

// Built-in role definition ids
var roles = {
  storageBlobDataOwner: 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
  storageQueueDataContributor: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
  storageTableDataContributor: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  keyVaultSecretsOfficer: 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${baseName}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: baseName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
  }
}

// The function's own storage: host state, deployment packages, and later the usage table and
// unmatched queue. Identity access only; no account keys.
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource packageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: deploymentContainer
}

// Partner key hashes and per-client limits only; RBAC mode so access can be granted per identity
// without touching the site's own vault.
resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
  }
}

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${baseName}-plan'
  location: location
  kind: 'functionapp'
  sku: { name: 'FC1', tier: 'FlexConsumption' }
  properties: { reserved: true }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: baseName
  location: location
  kind: 'functionapp,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'AzureWebJobsStorage__accountName', value: storage.name }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
        { name: 'SEARCH_ENDPOINT', value: 'https://${searchServiceName}.search.windows.net' }
        { name: 'SEARCH_INDEX', value: searchIndexAlias }
        { name: 'PARTNER_KEYS_VAULT_URI', value: vault.properties.vaultUri }
      ]
    }
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${deploymentContainer}'
          authentication: { type: 'SystemAssignedIdentity' }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: maximumInstanceCount
        instanceMemoryMB: instanceMemoryMB
      }
      runtime: { name: 'dotnet-isolated', version: '10.0' }
    }
  }
}

resource functionStorageRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for role in [roles.storageBlobDataOwner, roles.storageQueueDataContributor, roles.storageTableDataContributor]: {
    name: guid(storage.id, functionApp.id, role)
    scope: storage
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role)
      principalId: functionApp.identity.principalId
      principalType: 'ServicePrincipal'
    }
  }
]

resource functionVaultReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, functionApp.id, roles.keyVaultSecretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource keyWriterWebApp 'Microsoft.Web/sites@2024-04-01' existing = {
  name: keyWriterWebAppName
  scope: resourceGroup(keyWriterWebAppResourceGroup)
}

resource webAppVaultWriter 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, keyWriterWebApp.id, roles.keyVaultSecretsOfficer)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsOfficer)
    principalId: keyWriterWebApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource userVaultWriters 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for userId in (empty(keyWriterUserIds) ? [] : split(keyWriterUserIds, ',')): {
    name: guid(vault.id, userId, roles.keyVaultSecretsOfficer)
    scope: vault
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsOfficer)
      principalId: userId
      principalType: 'User'
    }
  }
]

output functionAppName string = functionApp.name
output functionPrincipalId string = functionApp.identity.principalId
output defaultHostName string = functionApp.properties.defaultHostName
output vaultUri string = vault.properties.vaultUri
