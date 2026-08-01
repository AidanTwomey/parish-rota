terraform {
  required_version = ">= 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # Partial config — the storage account holding state is created by
  # bootstrap.sh and supplied via `-backend-config` (see .github/workflows/infra.yml).
  backend "azurerm" {}
}

provider "azurerm" {
  features {}
  # subscription_id comes from ARM_SUBSCRIPTION_ID (required by azurerm 4.x).
}
