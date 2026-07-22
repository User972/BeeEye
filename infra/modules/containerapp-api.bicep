param name string
param location string
param tags object
param environmentId string
param image string
param registryLoginServer string
param registryId string
param appInsightsConnectionString string
param keyVaultName string

@description('Minimum replica count. 0 allows scale-to-zero in non-prod.')
param minReplicas int = 1
param maxReplicas int = 5

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: { type: 'SystemAssigned' }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: registryLoginServer
          identity: 'system' // managed-identity pull; no admin creds
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: image
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
            { name: 'AZURE_KEY_VAULT_NAME', value: keyVaultName }
          ]
          probes: [
            { type: 'Liveness', httpGet: { path: '/health/live', port: 8080 } }
            { type: 'Readiness', httpGet: { path: '/health/ready', port: 8080 } }
          ]
        }
      ]
      scale: { minReplicas: minReplicas, maxReplicas: maxReplicas }
    }
  }
}

// AcrPull for the app's managed identity so it can pull images.
resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registryId, containerApp.id, 'AcrPull')
  scope: registryResource
  properties: {
    principalId: containerApp.identity.principalId
    principalType: 'ServicePrincipal'
    // AcrPull built-in role.
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  }
}

resource registryResource 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' existing = {
  name: last(split(registryId, '/'))
}

output fqdn string = containerApp.properties.configuration.ingress.fqdn
output principalId string = containerApp.identity.principalId
