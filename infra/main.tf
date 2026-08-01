locals {
  name = "${var.project}-${var.environment}"

  tags = {
    project     = var.project
    environment = var.environment
    managedBy   = "terraform"
  }
}

# Storage and Cosmos account names must be globally unique.
resource "random_string" "suffix" {
  length  = 6
  lower   = true
  upper   = false
  numeric = true
  special = false
}

resource "azurerm_resource_group" "this" {
  name     = "rg-${local.name}"
  location = var.location
  tags     = local.tags
}

# ---------------------------------------------------------------------------
# Functions runtime storage (AzureWebJobsStorage)
# ---------------------------------------------------------------------------

resource "azurerm_storage_account" "functions" {
  name                            = "st${var.project}${random_string.suffix.result}"
  resource_group_name             = azurerm_resource_group.this.name
  location                        = azurerm_resource_group.this.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  https_traffic_only_enabled      = true
  allow_nested_items_to_be_public = false
  tags                            = local.tags
}

# ---------------------------------------------------------------------------
# Observability
# ---------------------------------------------------------------------------

resource "azurerm_log_analytics_workspace" "this" {
  name                = "log-${local.name}"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

resource "azurerm_application_insights" "this" {
  name                = "appi-${local.name}"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  workspace_id        = azurerm_log_analytics_workspace.this.id
  application_type    = "web"
  tags                = local.tags
}

# ---------------------------------------------------------------------------
# Cosmos DB — free tier, partitioned by parishId (ADR 0002, ADR 0006)
# ---------------------------------------------------------------------------

resource "azurerm_cosmosdb_account" "this" {
  name                = "cosmos-${var.project}-${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  offer_type          = "Standard"
  kind                = "GlobalDocumentDB"
  free_tier_enabled   = var.cosmos_free_tier
  tags                = local.tags

  consistency_policy {
    consistency_level = "Session"
  }

  geo_location {
    location          = azurerm_resource_group.this.location
    failover_priority = 0
  }
}

resource "azurerm_cosmosdb_sql_database" "this" {
  name                = "parishrota"
  resource_group_name = azurerm_resource_group.this.name
  account_name        = azurerm_cosmosdb_account.this.name

  # 400 RU/s shared across containers, inside the 1000 RU/s free allowance.
  throughput = 400
}

# One container per aggregate, all partitioned by parishId so tenancy is
# enforced by the storage layout itself.
resource "azurerm_cosmosdb_sql_container" "readers" {
  name                  = "readers"
  resource_group_name   = azurerm_resource_group.this.name
  account_name          = azurerm_cosmosdb_account.this.name
  database_name         = azurerm_cosmosdb_sql_database.this.name
  partition_key_paths   = ["/parishId"]
  partition_key_version = 2
}

resource "azurerm_cosmosdb_sql_container" "rotas" {
  name                  = "rotas"
  resource_group_name   = azurerm_resource_group.this.name
  account_name          = azurerm_cosmosdb_account.this.name
  database_name         = azurerm_cosmosdb_sql_database.this.name
  partition_key_paths   = ["/parishId"]
  partition_key_version = 2
}

# Inbound WhatsApp messages and outbound sends, for replay and debugging.
resource "azurerm_cosmosdb_sql_container" "conversations" {
  name                  = "conversations"
  resource_group_name   = azurerm_resource_group.this.name
  account_name          = azurerm_cosmosdb_account.this.name
  database_name         = azurerm_cosmosdb_sql_database.this.name
  partition_key_paths   = ["/parishId"]
  partition_key_version = 2
}

# ---------------------------------------------------------------------------
# Function App — Linux Consumption (Y1), .NET 10 isolated
# ---------------------------------------------------------------------------

resource "azurerm_service_plan" "this" {
  name                = "asp-${local.name}"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  os_type             = "Linux"
  sku_name            = "Y1" # Consumption: scale to zero, 1M free executions/month
  tags                = local.tags
}

resource "azurerm_linux_function_app" "this" {
  name                = "func-${local.name}-${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  service_plan_id     = azurerm_service_plan.this.id

  storage_account_name       = azurerm_storage_account.functions.name
  storage_account_access_key = azurerm_storage_account.functions.primary_access_key

  https_only = true
  tags       = local.tags

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_insights_connection_string = azurerm_application_insights.this.connection_string
    ftps_state                             = "Disabled"
    minimum_tls_version                    = "1.2"

    application_stack {
      dotnet_version              = "10.0"
      use_dotnet_isolated_runtime = true
    }
  }

  app_settings = {
    FUNCTIONS_WORKER_RUNTIME = "dotnet-isolated"

    COSMOS_CONNECTION_STRING = azurerm_cosmosdb_account.this.primary_sql_connection_string
    COSMOS_DATABASE_NAME     = azurerm_cosmosdb_sql_database.this.name

    WHATSAPP_PHONE_NUMBER_ID = var.whatsapp_phone_number_id
    WHATSAPP_ACCESS_TOKEN    = var.whatsapp_access_token
    WHATSAPP_VERIFY_TOKEN    = var.whatsapp_verify_token

    ANTHROPIC_API_KEY = var.anthropic_api_key
  }

  lifecycle {
    # The deploy workflow pushes the package; don't let Terraform revert it.
    ignore_changes = [
      app_settings["WEBSITE_RUN_FROM_PACKAGE"],
    ]
  }
}
