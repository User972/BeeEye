using '../main.bicep'

param environmentName = 'uat'
param namePrefix = 'beeeye'
param postgresAdminLogin = 'beeeye_admin'
// apiImage is overridden by the release pipeline with an immutable digest, e.g.
// param apiImage = 'crbeeeyeuat.azurecr.io/beeeye-api@sha256:<digest>'
