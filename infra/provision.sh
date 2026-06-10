#!/usr/bin/env bash
# Provision the PlayMe production stack on Azure.
#
# Idempotent: re-running is safe — every step either creates-if-missing or
# updates-in-place. The script is the source of truth for the prod footprint;
# clicking around the portal afterwards will drift from this and that drift
# disappears on the next run.
#
# Prereqs (one-time, on the operator's machine):
#   - az CLI logged into the right tenant: `az login`
#   - subscription selected:                `az account set -s <id>`
#   - `infra/provision.env` populated      (see provision.env.example)
#
# Usage:
#   cd <repo>
#   cp infra/provision.env.example infra/provision.env
#   <edit infra/provision.env>
#   bash infra/provision.sh

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
ENV_FILE="${SCRIPT_DIR}/provision.env"

if [[ ! -f "${ENV_FILE}" ]]; then
  echo "missing ${ENV_FILE} — copy provision.env.example and fill it in." >&2
  exit 1
fi

# shellcheck disable=SC1090
source "${ENV_FILE}"

: "${AZURE_SUBSCRIPTION_ID:?required}"
: "${LOCATION:?required}"
: "${RG:?required}"
: "${PLAN:?required}"
: "${WEBAPP:?required}"
: "${REDIS:?required}"
: "${API_HOSTNAME:?required}"
: "${WEB_ORIGIN:?required}"

# Vercel (and many hosts) redirect apex ↔ www, so accept both forms in CORS.
# Derive the alternate form from WEB_ORIGIN — strip leading `www.` if present,
# otherwise inject it. Order in the CORS allowlist doesn't matter.
if [[ "${WEB_ORIGIN}" =~ ^https?://www\. ]]; then
  WEB_ORIGIN_ALT="${WEB_ORIGIN/\/\/www./\/\/}"
else
  WEB_ORIGIN_ALT="${WEB_ORIGIN/\/\//\/\/www.}"
fi

: "${GITHUB_OWNER:?required}"
: "${GITHUB_REPO:?required}"
: "${ALERT_EMAIL:?required}"
: "${GHCR_IMAGE:?required}"

# Phase selection (lets CI / unattended runs skip the interactive DNS pause):
#   all       — default: end-to-end, with the interactive DNS pause
#   resources — sections 1–5 only (RG, Redis, plan, webapp, app settings)
#   domain    — sections 6–8 only (custom domain, TLS, AAD, alerts), assumes
#               DNS records already exist; no pause
PHASE="${PROVISION_PHASE:-all}"
case "${PHASE}" in
  all|resources|domain) ;;
  *) echo "PROVISION_PHASE must be one of: all, resources, domain" >&2; exit 1 ;;
esac

# Placeholder image used for the first deploy — the GitHub Actions pipeline
# repoints the web app at the real `${GHCR_IMAGE}` tag on its first run.
PLACEHOLDER_IMAGE="mcr.microsoft.com/azuredocs/aci-helloworld:latest"

log() { printf '\n\033[1;34m==>\033[0m %s\n' "$*"; }
note() { printf '   %s\n' "$*"; }
pause() {
  printf '\n\033[1;33m!!\033[0m %s\n' "$1"
  read -rp "press enter once done… " _
}

az account set --subscription "${AZURE_SUBSCRIPTION_ID}"
TENANT_ID="$(az account show --query tenantId -o tsv)"

if [[ "${PHASE}" == "all" || "${PHASE}" == "resources" ]]; then

# ─── 1. resource group ──────────────────────────────────────────────────────
log "resource group: ${RG}"
az group create --name "${RG}" --location "${LOCATION}" -o none

# ─── 2. redis (Azure Managed Redis — slow, kick off in the background) ──────
# Azure Managed Redis (Redis Enterprise stack), not the legacy Azure Cache for
# Redis — different ARM type (Microsoft.Cache/redisEnterprise), different CLI
# (`az redisenterprise`), endpoint on port 10000 over `*.redis.azure.net`.
# `az redisenterprise create` provisions both the cluster and its default
# database in one call (~10 min). Spawn it as a shell job so the App Service
# work below runs in parallel; we `wait` for it later.
#
# Baseline SKU is Balanced_B0 — single node, no HA, no SLA, cheapest tier
# (~$14/mo). B0 cannot run high availability, so --high-availability is
# Disabled here. To get an SLA, scale to Balanced_B1 with HA enabled by hand
# (see docs/deployment.md) — that drift is intentional, same as the App
# Service S1 bump.
# --client-protocol Encrypted = TLS-only (port 10000). --clustering-policy
# EnterpriseCluster exposes a single logical endpoint so StackExchange.Redis
# and the SignalR backplane need no cluster-awareness; our Redis access is
# single-key only (no multi-key Lua / transactions), so nothing relies on slot
# placement.
REDIS_PID=""
REDIS_LOG="$(mktemp -t provision-redis.XXXXXX)"
log "redis: ${REDIS} (Azure Managed Redis, Balanced B0)"
if az redisenterprise show -g "${RG}" -n "${REDIS}" -o none 2>/dev/null; then
  note "already exists — skipping create"
else
  note "kicking off async create (~10 min); log → ${REDIS_LOG}"
  az redisenterprise create \
    -g "${RG}" -n "${REDIS}" -l "${LOCATION}" \
    --sku Balanced_B0 \
    --high-availability Disabled \
    --minimum-tls-version 1.2 \
    --client-protocol Encrypted \
    --clustering-policy EnterpriseCluster \
    >"${REDIS_LOG}" 2>&1 &
  REDIS_PID=$!
fi

# ─── 3. app service plan + web app ──────────────────────────────────────────
log "app service plan: ${PLAN} (Linux B1)"
az appservice plan create \
  -g "${RG}" -n "${PLAN}" -l "${LOCATION}" \
  --is-linux --sku B1 -o none

log "web app: ${WEBAPP} (placeholder image until first CI deploy)"
if ! az webapp show -g "${RG}" -n "${WEBAPP}" -o none 2>/dev/null; then
  az webapp create \
    -g "${RG}" -p "${PLAN}" -n "${WEBAPP}" \
    --deployment-container-image-name "${PLACEHOLDER_IMAGE}" -o none
  # ARM has an eventual-consistency window right after `webapp create`: the
  # follow-up `webapp config set` can 404 even though the resource exists.
  # Block until it's actually queryable before continuing. (`az webapp wait
  # --created` is not available in current CLI versions — it errors with
  # "'wait' is misspelled or not recognized" — so poll `show` instead.)
  for _ in $(seq 1 24); do
    az webapp show -g "${RG}" -n "${WEBAPP}" -o none 2>/dev/null && break
    sleep 5
  done
fi

log "web app: enable websockets, always-on, https-only, tls 1.2"
az webapp config set \
  -g "${RG}" -n "${WEBAPP}" \
  --web-sockets-enabled true \
  --always-on true \
  --min-tls-version 1.2 \
  --http20-enabled true -o none
az webapp update -g "${RG}" -n "${WEBAPP}" --https-only true -o none

log "web app: system-assigned managed identity"
az webapp identity assign -g "${RG}" -n "${WEBAPP}" -o none

# ─── 4. wait for redis and capture its connection string ────────────────────
if [[ -n "${REDIS_PID}" ]]; then
  log "redis: waiting on background create (pid ${REDIS_PID})"
  if ! wait "${REDIS_PID}"; then
    echo "redis create failed; output follows:" >&2
    cat "${REDIS_LOG}" >&2
    exit 1
  fi
  rm -f "${REDIS_LOG}"
else
  log "redis: already provisioned"
fi

REDIS_HOST="$(az redisenterprise show -g "${RG}" -n "${REDIS}" --query hostName -o tsv)"
REDIS_KEY="$(az redisenterprise database list-keys -g "${RG}" --cluster-name "${REDIS}" --query primaryKey -o tsv)"
REDIS_CONN="${REDIS_HOST}:10000,password=${REDIS_KEY},ssl=True,abortConnect=False"

# ─── 5. app settings ────────────────────────────────────────────────────────
log "web app: app settings (CORS, Redis, ASP.NET env, telemetry)"
# `--settings` here REPLACES the listed keys but PRESERVES other keys already
# on the app (e.g. WEBSITES_PORT, SCM creds). That's what we want — additive
# in spirit, deterministic for the keys we own.
# Derive the registry URL from GHCR_IMAGE so the App Service container
# pull configuration matches the image we actually ship. Without this it
# stays at https://mcr.microsoft.com from the placeholder image and is
# merely misleading (the deploy workflow sets the full image path), but
# keeping it accurate avoids "wait, why does this say mcr?" later.
REGISTRY_URL="https://$(echo "${GHCR_IMAGE}" | cut -d/ -f1)"

# Telemetry secrets are optional — empty disables the integration (Sentry
# SDK + the API's analytics adapter both treat empty as "off"). Default to
# empty under `set -u` rather than requiring them, so a fresh install can
# stand up before the Sentry / PostHog projects exist. See §6.12.
SENTRY_DSN="${SENTRY_DSN:-}"
POSTHOG_API_KEY="${POSTHOG_API_KEY:-}"

az webapp config appsettings set \
  -g "${RG}" -n "${WEBAPP}" \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    WEBSITES_PORT=8080 \
    DOCKER_REGISTRY_SERVER_URL="${REGISTRY_URL}" \
    ConnectionStrings__Redis="${REDIS_CONN}" \
    Cors__AllowedOrigins__0="${WEB_ORIGIN}" \
    Cors__AllowedOrigins__1="${WEB_ORIGIN_ALT}" \
    Sentry__Dsn="${SENTRY_DSN}" \
    PostHog__ApiKey="${POSTHOG_API_KEY}" \
  -o none

fi # end PHASE=all|resources

if [[ "${PHASE}" == "all" || "${PHASE}" == "domain" ]]; then

# ─── 6. custom domain + managed TLS cert ────────────────────────────────────
#
# Heads-up on the App Service Managed Certificate step below: the
# `az webapp config ssl create --hostname` call is documented as in-preview
# and is known to hang silently on first-time provisioning for some
# subscriptions / TLDs. We observed it on `.ge` in both Italy North and
# West Europe — PUT returns 202 with an operation ID, but the resource
# never materializes and the operation eventually times out (~4 h window).
# When that happens, fall back to a CDN-fronted TLS solution (e.g.
# Cloudflare proxying api.<domain>) rather than burning more cycles here.
DEFAULT_HOSTNAME="$(az webapp show -g "${RG}" -n "${WEBAPP}" --query defaultHostName -o tsv)"
VERIFICATION_ID="$(az webapp show -g "${RG}" -n "${WEBAPP}" --query customDomainVerificationId -o tsv)"

CURRENT_HOSTNAMES="$(az webapp config hostname list -g "${RG}" --webapp-name "${WEBAPP}" --query "[].name" -o tsv || true)"
if ! grep -qx "${API_HOSTNAME}" <<<"${CURRENT_HOSTNAMES}"; then
  log "custom domain: ${API_HOSTNAME} — DNS records required before binding"
  cat <<EOF

  Add these records at your DNS provider (Cloudflare / Namecheap / wherever
  playme.ge is hosted), then continue:

    Type   Name                       Value
    CNAME  api                        ${DEFAULT_HOSTNAME}
    TXT    asuid.api                  ${VERIFICATION_ID}

  Wait ~1 minute after adding them for propagation.
EOF
  if [[ "${PHASE}" == "all" ]]; then
    pause "added the CNAME + TXT records?"
  fi

  az webapp config hostname add \
    -g "${RG}" --webapp-name "${WEBAPP}" \
    --hostname "${API_HOSTNAME}" -o none
fi

log "managed TLS cert for ${API_HOSTNAME}"
THUMBPRINT="$(az webapp config ssl list -g "${RG}" --query "[?subjectName=='${API_HOSTNAME}'].thumbprint | [0]" -o tsv 2>/dev/null || true)"
if [[ -z "${THUMBPRINT}" || "${THUMBPRINT}" == "null" ]]; then
  THUMBPRINT="$(az webapp config ssl create \
    -g "${RG}" --name "${WEBAPP}" --hostname "${API_HOSTNAME}" \
    --query thumbprint -o tsv)"
fi
az webapp config ssl bind \
  -g "${RG}" --name "${WEBAPP}" \
  --certificate-thumbprint "${THUMBPRINT}" \
  --ssl-type SNI -o none

# ─── 7. GitHub Actions OIDC: AAD app + federated credential ─────────────────
AAD_APP_NAME="playme-github-deploy"
log "AAD app for GitHub Actions: ${AAD_APP_NAME}"
APP_ID="$(az ad app list --display-name "${AAD_APP_NAME}" --query "[0].appId" -o tsv)"
if [[ -z "${APP_ID}" ]]; then
  APP_ID="$(az ad app create --display-name "${AAD_APP_NAME}" --query appId -o tsv)"
fi

SP_ID="$(az ad sp list --filter "appId eq '${APP_ID}'" --query "[0].id" -o tsv)"
if [[ -z "${SP_ID}" ]]; then
  SP_ID="$(az ad sp create --id "${APP_ID}" --query id -o tsv)"
fi

SUBJECT="repo:${GITHUB_OWNER}/${GITHUB_REPO}:ref:refs/heads/main"
EXISTING_FC="$(az ad app federated-credential list --id "${APP_ID}" --query "[?subject=='${SUBJECT}'].name | [0]" -o tsv)"
if [[ -z "${EXISTING_FC}" ]]; then
  az ad app federated-credential create --id "${APP_ID}" --parameters "$(cat <<EOF
{
  "name": "github-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "${SUBJECT}",
  "audiences": ["api://AzureADTokenExchange"]
}
EOF
)" -o none
fi

log "role assignment: Contributor on ${RG}"
RG_ID="$(az group show -n "${RG}" --query id -o tsv)"
az role assignment create \
  --assignee-object-id "${SP_ID}" \
  --assignee-principal-type ServicePrincipal \
  --role Contributor \
  --scope "${RG_ID}" -o none 2>/dev/null || true

# ─── 8. alerts → email action group ─────────────────────────────────────────
ACTION_GROUP="playme-oncall"
log "action group: ${ACTION_GROUP} → ${ALERT_EMAIL}"
az monitor action-group create \
  -g "${RG}" -n "${ACTION_GROUP}" \
  --short-name "playme" \
  --action email oncall "${ALERT_EMAIL}" -o none

WEBAPP_ID="$(az webapp show -g "${RG}" -n "${WEBAPP}" --query id -o tsv)"
REDIS_ID="$(az redisenterprise show -g "${RG}" -n "${REDIS}" --query id -o tsv)"
AG_ID="$(az monitor action-group show -g "${RG}" -n "${ACTION_GROUP}" --query id -o tsv)"

log "alert: HTTP 5xx > 10 per 5 min"
az monitor metrics alert create \
  -g "${RG}" -n "playme-api-5xx" \
  --scopes "${WEBAPP_ID}" \
  --description "API 5xx responses" \
  --condition "total Http5xx > 10" \
  --window-size 5m --evaluation-frequency 1m \
  --severity 2 \
  --action "${AG_ID}" -o none 2>/dev/null || \
  az monitor metrics alert update -g "${RG}" -n "playme-api-5xx" --add-action "${AG_ID}" -o none || true

log "alert: response time > 2s (avg, 5 min)"
az monitor metrics alert create \
  -g "${RG}" -n "playme-api-slow" \
  --scopes "${WEBAPP_ID}" \
  --description "API response time over 2s" \
  --condition "avg HttpResponseTime > 2" \
  --window-size 5m --evaluation-frequency 1m \
  --severity 3 \
  --action "${AG_ID}" -o none 2>/dev/null || true

log "alert: redis used-memory > 90% (sustained 5 min)"
# Azure Managed Redis (Microsoft.Cache/redisEnterprise) exposes a different
# metric set than legacy Azure Cache — there is no `serverLoad`. We alert on
# `usedmemorypercentage` as the capacity-pressure signal instead. If this name
# ever fails to bind, list the live metric names with:
#   az monitor metrics list-definitions --resource "${REDIS_ID}" --query "[].name.value"
az monitor metrics alert create \
  -g "${RG}" -n "playme-redis-high-load" \
  --scopes "${REDIS_ID}" \
  --description "Redis memory usage is high — capacity bump may be needed" \
  --condition "avg usedmemorypercentage > 90" \
  --window-size 5m --evaluation-frequency 1m \
  --severity 2 \
  --action "${AG_ID}" -o none 2>/dev/null || true

# ─── 9. done — print the secrets the GitHub repo needs ──────────────────────
cat <<EOF

╭─ provisioning complete ─────────────────────────────────────────────────────╮
│                                                                             │
│  Set these secrets at:                                                      │
│    https://github.com/${GITHUB_OWNER}/${GITHUB_REPO}/settings/secrets/actions
│                                                                             │
│    AZURE_CLIENT_ID         ${APP_ID}
│    AZURE_TENANT_ID         ${TENANT_ID}
│    AZURE_SUBSCRIPTION_ID   ${AZURE_SUBSCRIPTION_ID}
│                                                                             │
│  And these (non-secret) variables — Settings → Secrets and variables →      │
│  Actions → Variables tab:                                                   │
│                                                                             │
│    AZURE_RG                ${RG}
│    AZURE_WEBAPP            ${WEBAPP}
│    GHCR_IMAGE              ${GHCR_IMAGE}
│                                                                             │
│  Next:                                                                      │
│    1. Make the GHCR package public after the first CI push                  │
│       (GitHub → your profile → Packages → playme-api → Package settings)    │
│    2. Push to main — deploy-api.yml will build + deploy the real image      │
│    3. Point Vercel's PLAYME_API_URL / NEXT_PUBLIC_API_URL at                │
│       https://${API_HOSTNAME}                                               │
│                                                                             │
╰─────────────────────────────────────────────────────────────────────────────╯
EOF

fi # end PHASE=all|domain

if [[ "${PHASE}" == "resources" ]]; then
  # In phased runs the summary above is suppressed; print the DNS records that
  # need to be set up before invoking PROVISION_PHASE=domain.
  DEFAULT_HOSTNAME="$(az webapp show -g "${RG}" -n "${WEBAPP}" --query defaultHostName -o tsv)"
  VERIFICATION_ID="$(az webapp show -g "${RG}" -n "${WEBAPP}" --query customDomainVerificationId -o tsv)"
  cat <<EOF

resources phase complete. Add these DNS records, then re-run with
PROVISION_PHASE=domain bash infra/provision.sh:

  CNAME  api          ${DEFAULT_HOSTNAME}
  TXT    asuid.api    ${VERIFICATION_ID}

EOF
fi
