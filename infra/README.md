# Azure infrastructure

Terraform in [`terraform/`](terraform) provisions everything the API needs on Azure:

| Resource | Purpose |
| --- | --- |
| Resource group | Holds everything below |
| Azure Container Registry (Basic) | Stores the API Docker image |
| User-assigned managed identity + `AcrPull` role | Lets the app pull images without registry passwords |
| Azure Database for MySQL flexible server (B1ms) + `logisticdesk` database | Application database, SSL required |
| Log Analytics workspace | Container logs |
| Container Apps environment + container app | Runs the API with HTTPS ingress, health probes and scale-to-zero; DB connection string and JWT signing key are app secrets |

See [docs/architecture.md](../docs/architecture.md) for a diagram.

> Cost note: B1ms MySQL and Container Apps with `min_replicas = 0` are among the cheapest options, but MySQL is billed while it runs. Stop the server (`az mysql flexible-server stop`) or run `terraform destroy` when you are not demoing.

## Prerequisites

* An Azure subscription and the [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
* Terraform >= 1.6
* Docker

## 1. Provision

```bash
az login
export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv)

cd infra/terraform
cp terraform.tfvars.example terraform.tfvars   # adjust values
terraform init

# First create the registry so the initial image can be pushed into it.
terraform apply -target=azurerm_container_registry.main -target=azurerm_resource_group.main
```

## 2. Push the first image

```bash
ACR=$(terraform output -raw container_registry_name)
az acr login --name "$ACR"
docker build -t "$ACR.azurecr.io/logisticdesk-api:latest" ../..
docker push "$ACR.azurecr.io/logisticdesk-api:latest"
```

## 3. Create the rest

```bash
terraform apply
terraform output api_url        # https://<app>.<region>.azurecontainerapps.io
curl "$(terraform output -raw api_url)/health/ready"
```

Swagger UI is at `<api_url>/swagger` while `enable_swagger = true`.

## 4. Enable continuous deployment (GitHub Actions)

The [CD workflow](../.github/workflows/cd.yml) deploys `main` after CI passes. It logs in to Azure with OpenID Connect, so no client secret is stored in GitHub.

```bash
# App registration + service principal for GitHub Actions
APP_ID=$(az ad app create --display-name logisticdesk-github --query appId -o tsv)
az ad sp create --id "$APP_ID"

# Trust tokens issued for this repository's "production" environment
az ad app federated-credential create --id "$APP_ID" --parameters '{
  "name": "github-production",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<github-user>/<repo-name>:environment:production",
  "audiences": ["api://AzureADTokenExchange"]
}'

# Allow it to push images and update the container app (resource group scope only)
RG_ID=$(az group show --name "$(terraform output -raw resource_group_name)" --query id -o tsv)
az role assignment create --assignee "$APP_ID" --role Contributor --scope "$RG_ID"
az role assignment create --assignee "$APP_ID" --role AcrPush --scope "$RG_ID"
```

Then in GitHub: **Settings → Environments → New environment** `production`, and **Settings → Secrets and variables → Actions → Variables**:

| Variable | Value |
| --- | --- |
| `AZURE_CLIENT_ID` | `$APP_ID` from above |
| `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` |
| `AZURE_SUBSCRIPTION_ID` | `az account show --query id -o tsv` |
| `AZURE_RESOURCE_GROUP` | `terraform output -raw resource_group_name` |
| `AZURE_ACR_NAME` | `terraform output -raw container_registry_name` |
| `AZURE_CONTAINER_APP` | `terraform output -raw container_app_name` |

Until `AZURE_CLIENT_ID` is set the deploy job is skipped, so the workflow stays green on forks.

## Remote state (optional)

Local state contains the generated database password. For anything beyond a personal demo, keep state in a private Azure Storage container: uncomment `backend "azurerm" {}` in `versions.tf`, create `backend.hcl` (git-ignored):

```hcl
resource_group_name  = "rg-tfstate"
storage_account_name = "<unique name>"
container_name       = "tfstate"
key                  = "logisticdesk.tfstate"
```

and run `terraform init -backend-config=backend.hcl`.

## Tear down

```bash
terraform destroy
```
