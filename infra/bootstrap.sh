#!/usr/bin/env bash
# One-off bootstrap. Creates the things Terraform cannot create for itself:
#   1. Remote state storage (chicken-and-egg — state must exist before init)
#   2. An Entra app registration GitHub Actions signs into via OIDC (no secrets)
#
# Run once, from a machine logged in with `az login`, then paste the printed
# values into GitHub. Everything else is managed by Terraform.
#
#   GITHUB_REPO=owner/parish-rota ./bootstrap.sh

set -euo pipefail

: "${GITHUB_REPO:?Set GITHUB_REPO=owner/repo}"
LOCATION="${LOCATION:-uksouth}"
PREFIX="${PREFIX:-parishrota}"

STATE_RG="rg-${PREFIX}-tfstate"
STATE_CONTAINER="tfstate"
APP_NAME="sp-${PREFIX}-github"

SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
TENANT_ID="$(az account show --query tenantId -o tsv)"

# Storage account names are globally unique, max 24 chars, lowercase alnum.
STATE_SA="${STATE_SA:-st${PREFIX}tf$(openssl rand -hex 3)}"

echo "Subscription : ${SUBSCRIPTION_ID}"
echo "State account: ${STATE_SA}"
echo

# --- 1. Remote state ---------------------------------------------------------
az group create -n "${STATE_RG}" -l "${LOCATION}" -o none

az storage account create \
  --name "${STATE_SA}" \
  --resource-group "${STATE_RG}" \
  --location "${LOCATION}" \
  --sku Standard_LRS \
  --min-tls-version TLS1_2 \
  --allow-blob-public-access false \
  -o none

# Versioning gives you a way back from a corrupted or truncated state file.
az storage account blob-service-properties update \
  --account-name "${STATE_SA}" \
  --resource-group "${STATE_RG}" \
  --enable-versioning true \
  -o none

az storage container create \
  --name "${STATE_CONTAINER}" \
  --account-name "${STATE_SA}" \
  --auth-mode login \
  -o none

# --- 2. GitHub OIDC identity -------------------------------------------------
APP_ID="$(az ad app list --display-name "${APP_NAME}" --query '[0].appId' -o tsv)"
if [[ -z "${APP_ID}" ]]; then
  APP_ID="$(az ad app create --display-name "${APP_NAME}" --query appId -o tsv)"
fi
az ad sp create --id "${APP_ID}" -o none 2>/dev/null || true

# Contributor to manage resources; Blob Data Contributor to read/write state
# (the backend uses Entra auth rather than a storage access key).
az role assignment create \
  --assignee "${APP_ID}" \
  --role Contributor \
  --scope "/subscriptions/${SUBSCRIPTION_ID}" \
  -o none 2>/dev/null || true

az role assignment create \
  --assignee "${APP_ID}" \
  --role "Storage Blob Data Contributor" \
  --scope "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${STATE_RG}/providers/Microsoft.Storage/storageAccounts/${STATE_SA}" \
  -o none 2>/dev/null || true

# One federated credential per token subject GitHub will present.
# The `environment:prod` subject is required because the apply and deploy jobs
# declare `environment: prod` — that changes the subject claim.
add_fic() {
  local name="$1" subject="$2"
  az ad app federated-credential create --id "${APP_ID}" --parameters "{
    \"name\": \"${name}\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"${subject}\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }" -o none 2>/dev/null || echo "  (federated credential '${name}' already exists)"
}

# Repositories created (or renamed/transferred) after 15 July 2026 present an
# *immutable* subject claim that embeds numeric owner and repository IDs:
#
#   repo:OWNER@OWNER_ID/REPO@REPO_ID:environment:prod
#
# Those IDs cannot be stripped via sub-claim customization, so the credential
# has to match them exactly. Older repositories still present the plain-name
# form, so register both and let Entra match whichever actually turns up.
OWNER="${GITHUB_REPO%%/*}"
REPO="${GITHUB_REPO##*/}"

if command -v gh >/dev/null 2>&1; then
  read -r OWNER_ID REPO_ID < <(gh api "repos/${GITHUB_REPO}" --jq '"\(.owner.id) \(.id)"')
else
  read -r OWNER_ID REPO_ID < <(
    curl -fsSL ${GITHUB_TOKEN:+-H "Authorization: Bearer ${GITHUB_TOKEN}"} \
      "https://api.github.com/repos/${GITHUB_REPO}" |
      python3 -c 'import json,sys; d=json.load(sys.stdin); print(d["owner"]["id"], d["id"])'
  )
fi

IMMUTABLE="${OWNER}@${OWNER_ID}/${REPO}@${REPO_ID}"
echo "Immutable subject prefix: repo:${IMMUTABLE}"

add_fic "main"        "repo:${GITHUB_REPO}:ref:refs/heads/main"
add_fic "pullrequest" "repo:${GITHUB_REPO}:pull_request"
add_fic "prod"        "repo:${GITHUB_REPO}:environment:prod"

add_fic "main-immutable"        "repo:${IMMUTABLE}:ref:refs/heads/main"
add_fic "pullrequest-immutable" "repo:${IMMUTABLE}:pull_request"
add_fic "prod-immutable"        "repo:${IMMUTABLE}:environment:prod"

# --- 3. What to paste into GitHub -------------------------------------------
cat <<EOF

Done. Configure the repository (Settings -> Secrets and variables -> Actions):

  Variables:
    AZURE_CLIENT_ID          ${APP_ID}
    AZURE_TENANT_ID          ${TENANT_ID}
    AZURE_SUBSCRIPTION_ID    ${SUBSCRIPTION_ID}
    TFSTATE_RESOURCE_GROUP   ${STATE_RG}
    TFSTATE_STORAGE_ACCOUNT  ${STATE_SA}
    WHATSAPP_PHONE_NUMBER_ID <from Meta>

  Secrets:
    ANTHROPIC_API_KEY        <from console.anthropic.com>
    WHATSAPP_ACCESS_TOKEN    <from Meta>
    WHATSAPP_VERIFY_TOKEN    <invent one; Meta echoes it back on webhook setup>

Also create an Environment named 'prod' (Settings -> Environments) and add
yourself as a required reviewer if you want a manual gate before apply.

After the first successful infra run, set AZURE_FUNCTIONAPP_NAME from the
'function_app_name' Terraform output so the deploy workflow knows its target.
EOF
