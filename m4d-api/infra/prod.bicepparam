using 'main.bicep'

param baseName = 'm4d-api'
param storageAccountName = 'm4dapiprod'
param keyVaultName = 'm4d-api-keys'
param searchServiceName = 'music4dance'
param searchIndexAlias = 'songs-api-prod'
param keyWriterWebAppName = 'msc4dnc'
param keyWriterWebAppResourceGroup = 'm4d-Web'

// No always-ready instance until the partner API brings in income: scale to zero and accept the
// cold start. See partner-api-standalone-host.md § Decisions so far before raising it.
param alwaysReadyHttpInstances = 0
