locals {
  name = "${var.project_name}-${var.environment}"
  # Registry names must be globally unique and alphanumeric only.
  registry_name = "${var.project_name}${var.environment}${random_string.suffix.result}"
  image_name    = "logisticdesk-api"
  database_name = "logisticdesk"

  tags = merge({
    project     = var.project_name
    environment = var.environment
    managed_by  = "terraform"
  }, var.tags)

  connection_string = join(";", [
    "Server=${azurerm_mysql_flexible_server.main.fqdn}",
    "Port=3306",
    "Database=${local.database_name}",
    "User=${var.mysql_admin_login}",
    "Password=${random_password.mysql_admin.result}",
    "SslMode=Required",
  ])
}

resource "random_string" "suffix" {
  length  = 5
  upper   = false
  special = false
}

# Generated here so no database password ever lives in a tfvars file or in Git.
# It is kept in the Terraform state, so the state must be stored securely.
resource "random_password" "mysql_admin" {
  length           = 24
  special          = true
  override_special = "!#%*-_+"
}

# Key used by the API to sign JWT access tokens.
resource "random_password" "jwt_signing_key" {
  length  = 64
  special = false
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.name}"
  location = var.location
  tags     = local.tags
}

# ---------------------------------------------------------------------------
# Observability
# ---------------------------------------------------------------------------

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${local.name}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

# ---------------------------------------------------------------------------
# Container registry and the identity the app uses to pull from it
# ---------------------------------------------------------------------------

resource "azurerm_container_registry" "main" {
  name                = local.registry_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "Basic"
  admin_enabled       = false
  tags                = local.tags
}

resource "azurerm_user_assigned_identity" "api" {
  name                = "id-${local.name}-api"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.tags
}

resource "azurerm_role_assignment" "api_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.api.principal_id
}

# ---------------------------------------------------------------------------
# Database: Azure Database for MySQL flexible server
# ---------------------------------------------------------------------------

resource "azurerm_mysql_flexible_server" "main" {
  name                   = "mysql-${local.name}-${random_string.suffix.result}"
  location               = azurerm_resource_group.main.location
  resource_group_name    = azurerm_resource_group.main.name
  administrator_login    = var.mysql_admin_login
  administrator_password = random_password.mysql_admin.result
  sku_name               = var.mysql_sku_name
  version                = "8.0.21"
  backup_retention_days  = 7

  storage {
    size_gb = 20
  }

  tags = local.tags

  lifecycle {
    # Azure picks an availability zone when none is given; do not fight it.
    ignore_changes = [zone]
  }
}

resource "azurerm_mysql_flexible_database" "main" {
  name                = local.database_name
  resource_group_name = azurerm_resource_group.main.name
  server_name         = azurerm_mysql_flexible_server.main.name
  charset             = "utf8mb4"
  collation           = "utf8mb4_unicode_ci"
}

# The 0.0.0.0 rule allows connections from Azure services only (not the public
# internet). For production, replace it with VNet integration / private access.
resource "azurerm_mysql_flexible_server_firewall_rule" "azure_services" {
  name                = "allow-azure-services"
  resource_group_name = azurerm_resource_group.main.name
  server_name         = azurerm_mysql_flexible_server.main.name
  start_ip_address    = "0.0.0.0"
  end_ip_address      = "0.0.0.0"
}

# ---------------------------------------------------------------------------
# Compute: Azure Container Apps
# ---------------------------------------------------------------------------

resource "azurerm_container_app_environment" "main" {
  name                       = "cae-${local.name}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = local.tags
}

resource "azurerm_container_app" "api" {
  name                         = "ca-${local.name}-api"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.api.id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.api.id
  }

  secret {
    name  = "db-connection-string"
    value = local.connection_string
  }

  secret {
    name  = "jwt-signing-key"
    value = random_password.jwt_signing_key.result
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = var.min_replicas
    max_replicas = var.max_replicas

    container {
      name   = "api"
      image  = "${azurerm_container_registry.main.login_server}/${local.image_name}:${var.image_tag}"
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }
      env {
        name        = "ConnectionStrings__DefaultConnection"
        secret_name = "db-connection-string"
      }
      env {
        name        = "Jwt__SigningKey"
        secret_name = "jwt-signing-key"
      }
      env {
        name  = "Database__MigrateOnStartup"
        value = "true"
      }
      env {
        name  = "Hosting__BehindTlsProxy"
        value = "true"
      }
      env {
        name  = "Swagger__Enabled"
        value = tostring(var.enable_swagger)
      }

      dynamic "env" {
        for_each = { for i, origin in var.cors_allowed_origins : i => origin }
        content {
          name  = "Cors__AllowedOrigins__${env.key}"
          value = env.value
        }
      }

      liveness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health/live"
      }

      readiness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health/ready"
      }
    }
  }

  lifecycle {
    # The CD workflow rolls out new image tags; a later `terraform apply`
    # must not roll the app back to the tag it was created with.
    ignore_changes = [template[0].container[0].image]
  }

  depends_on = [
    azurerm_role_assignment.api_acr_pull,
    azurerm_mysql_flexible_database.main,
    azurerm_mysql_flexible_server_firewall_rule.azure_services,
  ]
}
