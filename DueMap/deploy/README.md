# DueMap — Deploy to Azure

End-to-end: a fresh Azure subscription to a running app in ~25 minutes. The
Bicep template provisions Container Apps + Azure SQL serverless + ACR + Azure
Files for Data Protection keys. Cost shape: ~$70/mo idle, ~$200–300/mo at
production scale (matches the cost analysis in the root README).

## What gets provisioned

- **Log Analytics workspace** (logs sink for Container Apps)
- **Azure Container Registry** (Standard SKU)
- **Azure SQL** logical server + **Serverless `GP_S_Gen5_2`** database (`autoPauseDelay: 60`, `minCapacity: 0.5`)
- **Storage Account** with an Azure Files share `dp-keys` (mounted into both apps for the shared Data Protection keyring)
- **User-assigned managed identity** with the `AcrPull` role on the registry
- **Container App: Web** with external ingress on 8080, scales 0–3 on HTTP concurrency
- **Container App: Worker** with no ingress, fixed 1 replica (Hangfire wants a single executor)

The full source: [`azure/main.bicep`](azure/main.bicep).

## Prereqs

- Azure CLI ≥ 2.60
- Docker (for the image build/push step)
- Bicep CLI (bundled with recent `az`)
- Logged in: `az login` and `az account set --subscription <id>`

## One-time setup

```sh
# Create the RG
az group create --name rg-duemap-dev --location eastus

# Copy parameters template and edit the secrets out-of-band
cp deploy/azure/parameters.example.json deploy/azure/parameters.dev.json
# Edit parameters.dev.json: set sqlAdminLogin + sqlAdminPassword.
# Other secrets (SendGrid/Twilio/QB/Xero) can stay empty until you're ready
# to wire them — the apps return clean "Failed" results when keys are empty.

# Deploy infra (this is safe to re-run — it's idempotent)
az deployment group create \
    --resource-group rg-duemap-dev \
    --template-file deploy/azure/main.bicep \
    --parameters @deploy/azure/parameters.dev.json
```

The deployment prints output values including the ACR login server, the SQL
FQDN, and the public Web FQDN. The Web app comes up red until you push images.

## Push images to ACR

```sh
ACR=$(az deployment group show -g rg-duemap-dev -n main --query properties.outputs.acrLoginServer.value -o tsv)
az acr login --name "$ACR"

# Build + push from the solution root
docker build -f src/DueMap.Web/Dockerfile    -t "$ACR/duemap/web:1.0.0" .
docker build -f src/DueMap.Worker/Dockerfile -t "$ACR/duemap/worker:1.0.0" .
docker push "$ACR/duemap/web:1.0.0"
docker push "$ACR/duemap/worker:1.0.0"

# Roll the container apps to the new tags
az deployment group create \
    --resource-group rg-duemap-dev \
    --template-file deploy/azure/main.bicep \
    --parameters @deploy/azure/parameters.dev.json \
    --parameters webImageTag=duemap/web:1.0.0 workerImageTag=duemap/worker:1.0.0
```

## Bootstrap the schema

Container Apps doesn't have a "run this script once" primitive equivalent to
docker-compose's bootstrap sidecar, so apply schema + seeds from your laptop
against the Azure SQL FQDN:

```sh
SQL_FQDN=$(az deployment group show -g rg-duemap-dev -n main --query properties.outputs.sqlServerFqdn.value -o tsv)
SQL_DB=$(az deployment group show -g rg-duemap-dev -n main --query properties.outputs.sqlDatabaseName.value -o tsv)

# Whitelist your IP temporarily (the Bicep template only allows Azure-internal traffic by default)
MY_IP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create \
    --resource-group rg-duemap-dev \
    --server "${SQL_FQDN%%.*}" \
    --name "laptop-bootstrap" \
    --start-ip-address "$MY_IP" --end-ip-address "$MY_IP"

# Apply all schema + seed scripts in order
SCRIPTS="duemap_schema.sql \
         duemap_schema_v2_lease_settings.sql \
         duemap_schema_v3_assessments.sql \
         duemap_schema_v4_pm_processing.sql \
         duemap_schema_v5_accounting.sql \
         duemap_schema_v6_customers_invoices.sql \
         duemap_schema_v7_identity.sql \
         seed_state_rules.sql \
         seed_notice_templates.sql"

for s in $SCRIPTS; do
    sqlcmd -S "$SQL_FQDN" -d "$SQL_DB" -U duemap_admin -P "$SQL_ADMIN_PASSWORD" -i "db/$s"
done

# Remove the temporary rule
az sql server firewall-rule delete \
    --resource-group rg-duemap-dev \
    --server "${SQL_FQDN%%.*}" \
    --name "laptop-bootstrap"
```

## Browse

```sh
echo "https://$(az deployment group show -g rg-duemap-dev -n main --query properties.outputs.webFqdn.value -o tsv)"
```

Register a PM org through `/Account/Register` — the demo seeder only runs in
Development. In Production the first user creates the first PM.

## Honest caveats

**Secrets in parameters.** The current template takes SA password and provider
secrets as plain `@secure()` params. For real production, those should come
from Key Vault references in your pipeline, not from a checked-in parameters
file. The example file is named `.example.json` to make this clear.

**SQL admin is the connection string user.** Production should use Azure AD
authentication (managed identity for the apps, no static password). That's a
deferred refinement — moves the schema apply step to a one-shot Container App
job instead of laptop sqlcmd, and adds AAD config to the SQL server.

**Hangfire dashboard gating** is now in place: any authenticated user can see
`/hangfire`. Before exposing dashboard access to PMs themselves, swap the
filter for a role check (the `HangfireDashboardAuthorizationFilter` source
notes the upgrade path). For platform-only access, the current filter is the
right level.

**Data Protection on Azure Files.** Works fine, but Azure recommends Blob +
Key Vault-protected keys for compliance-sensitive workloads. The current
setup is appropriate for v1; revisit before SOC2.

**No Application Insights.** Logs go to Log Analytics via the Container Apps
env. Add App Insights when you want sampled distributed traces / live metrics.

## Self-hosted VM alternative (sketch only)

If you'd rather run a single small VM:
1. Provision an Ubuntu VM, install Docker + docker compose.
2. `git clone`, `docker compose up -d` (uses `docker-compose.yml` at the repo root).
3. Front it with Caddy for TLS termination (`caddy reverse-proxy --to localhost:8080 --domain duemap.example.com`).

Cheaper at the bottom end (~$15–30/mo for the VM), but you own patching, SQL
backups, and HA. Container Apps wins past month two for most teams.
