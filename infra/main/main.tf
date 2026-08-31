resource "random_string" "suffix" {
  length  = 6
  upper   = false
  special = false
}

locals {
  unique_name          = "${var.app_name}-${random_string.suffix.result}"
  storage_account_name = substr(replace("srr${random_string.suffix.result}", "-", ""), 0, 24)
}

data "azurerm_resource_group" "main" {
  name = var.resource_group_name
}

resource "azurerm_service_plan" "main" {
  name                = "asp-${local.unique_name}"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = data.azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = "B1"
  worker_count        = 1
  tags                = var.tags
}

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${local.unique_name}"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = data.azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = var.tags
}

resource "azurerm_application_insights" "main" {
  name                = "appi-${local.unique_name}"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = data.azurerm_resource_group.main.location
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = var.tags
}

resource "azurerm_storage_account" "main" {
  name                            = local.storage_account_name
  resource_group_name             = data.azurerm_resource_group.main.name
  location                        = data.azurerm_resource_group.main.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  account_kind                    = "StorageV2"
  access_tier                     = "Hot"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = false
  public_network_access_enabled   = true
  tags                            = var.tags

  blob_properties {
    delete_retention_policy {
      days = 7
    }
  }
}

resource "azurerm_storage_container" "raw" {
  name                  = "raw"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_storage_container" "maps" {
  name                  = "maps"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_storage_management_policy" "retention" {
  storage_account_id = azurerm_storage_account.main.id

  rule {
    name    = "delete-raw-after-14-days"
    enabled = true

    filters {
      prefix_match = ["raw/"]
      blob_types   = ["blockBlob"]
    }

    actions {
      base_blob {
        delete_after_days_since_modification_greater_than = 14
      }
    }
  }

  rule {
    name    = "delete-map-history-after-15-days"
    enabled = true

    filters {
      prefix_match = ["maps/history/"]
      blob_types   = ["blockBlob"]
    }

    actions {
      base_blob {
        delete_after_days_since_modification_greater_than = 15
      }
    }
  }
}

resource "azurerm_linux_web_app" "main" {
  name                = local.unique_name
  resource_group_name = data.azurerm_resource_group.main.name
  location            = data.azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.main.id
  https_only          = true
  tags                = var.tags

  identity {
    type = "SystemAssigned"
  }

  site_config {
    always_on                         = false
    ftps_state                        = "Disabled"
    health_check_path                 = "/healthz"
    health_check_eviction_time_in_min = 5
    minimum_tls_version               = "1.2"
    http2_enabled                     = true

    application_stack {
      dotnet_version = "10.0"
    }
  }

  app_settings = {
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.main.connection_string
    "Storage__AccountUri"                   = azurerm_storage_account.main.primary_blob_endpoint
    "Radar__BackfillOnStartup"              = "true"
    "WEBSITE_RUN_FROM_PACKAGE"              = "1"
  }
}

resource "azurerm_role_assignment" "web_blob_data_contributor" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_web_app.main.identity[0].principal_id
}
