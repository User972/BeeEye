#!/usr/bin/env bash
# Deploy the BeeEye platform to a resource group. What-if unless --apply is passed.
#   ./deploy.sh <resource-group> <dev|test|uat|prod> [--apply]
set -euo pipefail

RG="${1:?resource group required}"
ENV="${2:?environment required}"
APPLY="${3:-}"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TEMPLATE="$ROOT/main.bicep"
PARAMS="$ROOT/environments/$ENV.bicepparam"

echo "What-if for $RG ($ENV)..."
az deployment group what-if \
  --resource-group "$RG" \
  --template-file "$TEMPLATE" \
  --parameters "$PARAMS"

if [[ "$APPLY" == "--apply" ]]; then
  echo "Applying deployment..."
  az deployment group create \
    --resource-group "$RG" \
    --template-file "$TEMPLATE" \
    --parameters "$PARAMS"
else
  echo "What-if only. Pass --apply to deploy."
fi
