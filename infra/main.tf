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

# Flex Consumption runs the app straight from a package in blob storage — there
# is no WEBSITE_RUN_FROM_PACKAGE app setting to point elsewhere.
resource "azurerm_storage_container" "deployments" {
  name                  = "deployments"
  storage_account_id    = azurerm_storage_account.functions.id
  container_access_type = "private"
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
# Function App — Linux Flex Consumption (FC1), .NET 10 isolated
#
# Not Y1 Consumption, despite ADR 0006. Y1 draws on a per-subscription,
# per-region "Dynamic VMs" quota that is 0 on this subscription and is not
# self-service increasable, so `terraform apply` failed at the plan with
# ExtendedCode 70007. Flex Consumption has its own quota pool (default 250
# cores per region, untouched by other plan types), still scales to zero, and
# still bills execution-time only while no always-ready instances are set.
# ---------------------------------------------------------------------------

resource "azurerm_service_plan" "this" {
  name                = "asp-${local.name}"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  os_type             = "Linux"
  sku_name            = "FC1"
  tags                = local.tags
}

resource "azurerm_function_app_flex_consumption" "this" {
  name                = "func-${local.name}-${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  service_plan_id     = azurerm_service_plan.this.id

  storage_container_type      = "blobContainer"
  storage_container_endpoint  = "${azurerm_storage_account.functions.primary_blob_endpoint}${azurerm_storage_container.deployments.name}"
  storage_authentication_type = "StorageAccountConnectionString"
  storage_access_key          = azurerm_storage_account.functions.primary_access_key

  runtime_name    = "dotnet-isolated"
  runtime_version = "10.0"

  # Smallest instance, and a low ceiling: a parish rota should never need to
  # scale, so this caps the damage from a runaway loop or a hostile caller
  # hitting the anonymous webhook.
  instance_memory_in_mb  = 512
  maximum_instance_count = 40

  https_only = true
  tags       = local.tags

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_insights_connection_string = azurerm_application_insights.this.connection_string
    minimum_tls_version                    = "1.2"
  }

  # No FUNCTIONS_WORKER_RUNTIME or WEBSITE_RUN_FROM_PACKAGE here: both are
  # deprecated on Flex Consumption. The runtime comes from runtime_name above,
  # and deployments land in the blob container rather than an app setting.
  app_settings = {
    COSMOS_CONNECTION_STRING = azurerm_cosmosdb_account.this.primary_sql_connection_string
    COSMOS_DATABASE_NAME     = azurerm_cosmosdb_sql_database.this.name

    WHATSAPP_PHONE_NUMBER_ID = var.whatsapp_phone_number_id
    WHATSAPP_ACCESS_TOKEN    = var.whatsapp_access_token
    WHATSAPP_VERIFY_TOKEN    = var.whatsapp_verify_token
    WHATSAPP_APP_SECRET      = var.whatsapp_app_secret

    ANTHROPIC_API_KEY = var.anthropic_api_key
  }
}
