@description('Container registry name (globally unique, 5-50 alphanumeric).')
param name string
param location string
param tags object

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: name
  location: location
  tags: tags
  sku: { name: 'Standard' }
  properties: {
    // IP protection: admin credentials disabled — pulls use managed identity only.
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

output registryId string = registry.id
output loginServer string = registry.properties.loginServer
