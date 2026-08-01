output "resource_group_name" {
  description = "Resource group holding every Parish Rota resource."
  value       = azurerm_resource_group.this.name
}

output "function_app_name" {
  description = "Function App name — pass this to the deploy workflow."
  value       = azurerm_function_app_flex_consumption.this.name
}

output "function_app_hostname" {
  description = "Default hostname of the Function App."
  value       = azurerm_function_app_flex_consumption.this.default_hostname
}

output "whatsapp_webhook_url" {
  description = "Register this URL as the WhatsApp Cloud API callback."
  value       = "https://${azurerm_function_app_flex_consumption.this.default_hostname}/api/whatsapp"
}

output "cosmos_account_name" {
  description = "Cosmos DB account name (browse documents here via the Azure portal — ADR 0003)."
  value       = azurerm_cosmosdb_account.this.name
}
