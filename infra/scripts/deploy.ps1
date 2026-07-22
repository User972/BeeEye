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

Write-Host "Validating $template against $ResourceGroup ($Environment)..."
az deployment group what-if `
  --resource-group $ResourceGroup `
  --template-file $template `
  --parameters $params

if ($Apply) {
  Write-Host 'Applying deployment...'
  az deployment group create `
    --resource-group $ResourceGroup `
    --template-file $template `
    --parameters $params
}
else {
  Write-Host 'What-if only. Re-run with -Apply to deploy.'
}
