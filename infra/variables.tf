variable "project" {
  description = "Short lowercase name used as the prefix for every resource."
  type        = string
  default     = "parishrota"

  validation {
    condition     = can(regex("^[a-z0-9]{3,12}$", var.project))
    error_message = "project must be 3-12 lowercase alphanumeric characters (it feeds storage account names)."
  }
}

variable "environment" {
  description = "Deployment environment, used in resource names and tags."
  type        = string
  default     = "prod"
}

variable "location" {
  description = "Azure region. UK South keeps parish data in-country and close to users."
  type        = string
  default     = "uksouth"
}

variable "cosmos_free_tier" {
  description = "Enable the Cosmos DB free tier (1000 RU/s + 25 GB). Only one account per subscription may use it."
  type        = bool
  default     = true
}

variable "whatsapp_phone_number_id" {
  description = "Meta WhatsApp Cloud API phone number ID."
  type        = string
  default     = ""
}

variable "whatsapp_access_token" {
  description = "Meta WhatsApp Cloud API access token."
  type        = string
  default     = ""
  sensitive   = true
}

variable "whatsapp_app_secret" {
  description = "Meta app secret. Keys the X-Hub-Signature-256 HMAC on inbound webhooks; without it every POST is rejected."
  type        = string
  default     = ""
  sensitive   = true
}

variable "whatsapp_verify_token" {
  description = "Shared secret Meta echoes back when verifying the webhook subscription."
  type        = string
  default     = ""
  sensitive   = true
}

variable "anthropic_api_key" {
  description = "Anthropic API key used for intent parsing (Claude Haiku 4.5)."
  type        = string
  default     = ""
  sensitive   = true
}
