// -----------------------------------------------------------------------------
// BeeEye platform — resource-group scoped deployment.
//   az deployment group create -g <rg> -f main.bicep -p environments/dev.bicepparam
// Provisions the standard secured-mode footprint. Private-endpoint / VNet
// integration for private-enterprise mode is layered via additional modules.
// -----------------------------------------------------------------------------
targetScope = 'resourceGroup'

@description('Short prefix for resource names, e.g. "beeeye".')
@minLength(3)
@maxLength(11)
param namePrefix string = 'beeeye'

@description('Environment name.')
@allowed(['dev', 'test', 'uat', 'prod'])
param environmentName string

@description('Azure region. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('PostgreSQL administrator login (password is generated and stored in Key Vault by a secure pipeline step — never hard-coded here).')
param postgresAdminLogin string = 'beeeye_admin'

@secure()
@description('PostgreSQL administrator password. Supplied by the pipeline (which also stores it in Key Vault) — never committed to a .bicepparam file.')
param postgresAdminPassword string

@description('Container image reference (immutable digest) for the API host.')
param apiImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

@description('Entra ID tenant used for application authentication.')
param entraTenantId string = tenant().tenantId

var tags = {
  workload: 'beeeye'
  environment: environmentName
  managedBy: 'bicep'
}

var suffix = '${namePrefix}-${environmentName}'

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    name: suffix
    location: location
    tags: tags
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    name: 'kv-${uniqueString(resourceGroup().id, suffix)}'
    location: location
    tags: tags
    tenantId: entraTenantId
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    name: 'st${uniqueString(resourceGroup().id, suffix)}'
    location: location
    tags: tags
  }
}

module serviceBus 'modules/servicebus.bicep' = {
  name: 'servicebus'
  params: {
    name: 'sb-${suffix}'
    location: location
    tags: tags
  }
}

module registry 'modules/registry.bicep' = {
  name: 'registry'
  params: {
    name: 'cr${uniqueString(resourceGroup().id, suffix)}'
    location: location
    tags: tags
  }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: {
    name: 'psql-${suffix}'
    location: location
    tags: tags
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    // Production sizing is set per-environment in the .bicepparam files.
    skuName: environmentName == 'prod' ? 'Standard_D2ds_v5' : 'Standard_B1ms'
    skuTier: environmentName == 'prod' ? 'GeneralPurpose' : 'Burstable'
  }
}

module appsEnv 'modules/containerapps-env.bicep' = {
  name: 'apps-env'
  params: {
    name: 'cae-${suffix}'
    location: location
    tags: tags
    logAnalyticsCustomerId: monitoring.outputs.logAnalyticsCustomerId
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

module apiApp 'modules/containerapp-api.bicep' = {
  name: 'api-app'
  params: {
    name: 'ca-${suffix}-api'
    location: location
    tags: tags
    environmentId: appsEnv.outputs.environmentId
    image: apiImage
    registryLoginServer: registry.outputs.loginServer
    registryId: registry.outputs.registryId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    keyVaultName: keyVault.outputs.name
    postgresFqdn: postgres.outputs.fqdn
    postgresAdminLogin: postgresAdminLogin
    postgresAdminPassword: postgresAdminPassword
  }
}

output apiFqdn string = apiApp.outputs.fqdn
output keyVaultName string = keyVault.outputs.name
output registryLoginServer string = registry.outputs.loginServer
output dataLakeName string = storage.outputs.name
