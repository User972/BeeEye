<#
.SYNOPSIS
  Deploy the BeeEye platform to a resource group. Runs a what-if by default.
.EXAMPLE
  ./deploy.ps1 -ResourceGroup rg-beeeye-dev -Environment dev
  ./deploy.ps1 -ResourceGroup rg-beeeye-dev -Environment dev -Apply
#>
param(
  [Parameter(Mandatory = $true)][string]$ResourceGroup,
  [Parameter(Mandatory = $true)][ValidateSet('dev', 'test', 'uat', 'prod')][string]$Environment,
  [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$template = Join-Path $root 'main.bicep'
$params = Join-Path $root "environments/$Environment.bicepparam"

# Secure parameter: never stored in a .bicepparam file. The pipeline generates it
# and stores it in Key Vault; locally, export POSTGRES_ADMIN_PASSWORD before running.
if (-not $env:POSTGRES_ADMIN_PASSWORD) {
  throw 'POSTGRES_ADMIN_PASSWORD is not set. Export it (the pipeline stores the same value in Key Vault) before deploying.'
}

Write-Host "Validating $template against $ResourceGroup ($Environment)..."
az deployment group what-if `
  --resource-group $ResourceGroup `
  --template-file $template `
  --parameters $params `
  --parameters postgresAdminPassword=$env:POSTGRES_ADMIN_PASSWORD

if ($Apply) {
  Write-Host 'Applying deployment...'
  az deployment group create `
    --resource-group $ResourceGroup `
    --template-file $template `
    --parameters $params `
    --parameters postgresAdminPassword=$env:POSTGRES_ADMIN_PASSWORD
}
else {
  Write-Host 'What-if only. Re-run with -Apply to deploy.'
}
