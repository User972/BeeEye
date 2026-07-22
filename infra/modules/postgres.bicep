param name string
param location string
param tags object
param administratorLogin string
param skuName string = 'Standard_B1ms'
param skuTier string = 'Burstable'
param postgresVersion string = '16'

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: name
  location: location
  tags: tags
  sku: { name: skuName, tier: skuTier }
  properties: {
    version: postgresVersion
    // Password authentication is disabled — Entra ID (managed identity) only.
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Disabled'
      tenantId: subscription().tenantId
    }
    administratorLogin: administratorLogin
    storage: { storageSizeGB: 32 }
    backup: {
      backupRetentionDays: 14
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: { mode: 'Disabled' }
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: server
  name: 'beeeye'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

output id string = server.id
output fqdn string = server.properties.fullyQualifiedDomainName
