terraform {
  required_version = ">= 1.6"

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

  # Local state by default. For shared use, store state in an Azure Storage
  # account instead (see infra/README.md) and run:
  #   terraform init -backend-config=backend.hcl
  # backend "azurerm" {}
}

provider "azurerm" {
  features {}
  # The subscription is read from ARM_SUBSCRIPTION_ID (or `az account set`).
}
