@description('Key Vault name (globally unique, 3-24 chars).')
@maxLength(24)
param name string
param location string
param tags object
param tenantId string

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: tenantId
    sku: { family: 'A', name: 'standard' }
    // RBAC authorization — no legacy access policies. Managed identities are
    // granted 'Key Vault Secrets User' out of band.
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled' // tightened to 'Disabled' + private endpoint in private mode
  }
}

output id string = keyVault.id
output name string = keyVault.name
output uri string = keyVault.properties.vaultUri
