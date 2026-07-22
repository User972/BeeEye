@description('Storage account name (globally unique, 3-24 lowercase).')
@maxLength(24)
param name string
param location string
param tags object

@description('Data-lake zones created as filesystems (containers).')
param zones array = [
  'raw'
  'validated'
  'curated'
  'quarantine'
  'model-input'
  'model-output'
  'export'
]

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    isHnsEnabled: true // ADLS Gen2 hierarchical namespace
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false // force Entra / managed-identity auth
    supportsHttpsTrafficOnly: true
  }
}

resource blob 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource zoneContainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = [
  for zone in zones: {
    parent: blob
    name: zone
    properties: { publicAccess: 'None' }
  }
]

output id string = storage.id
output name string = storage.name
