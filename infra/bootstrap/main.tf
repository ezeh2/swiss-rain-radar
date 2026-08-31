resource "random_string" "suffix" {
  length  = 8
  upper   = false
  special = false
}

resource "azurerm_resource_group" "state" {
  name     = "rg-swiss-rain-radar-tfstate"
  location = var.location
}

resource "azurerm_resource_group" "application" {
  name     = var.application_resource_group_name
  location = var.location
}

resource "azurerm_storage_account" "state" {
  name                            = "srrtf${random_string.suffix.result}"
  resource_group_name             = azurerm_resource_group.state.name
  location                        = azurerm_resource_group.state.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = false
}

resource "azurerm_storage_container" "state" {
  name                  = "tfstate"
  storage_account_id    = azurerm_storage_account.state.id
  container_access_type = "private"
}

resource "azurerm_user_assigned_identity" "github" {
  name                = "id-github-swiss-rain-radar"
  resource_group_name = azurerm_resource_group.state.name
  location            = azurerm_resource_group.state.location
}

resource "azurerm_federated_identity_credential" "main" {
  name                = "github-main"
  resource_group_name = azurerm_resource_group.state.name
  parent_id           = azurerm_user_assigned_identity.github.id
  audience            = ["api://AzureADTokenExchange"]
  issuer              = "https://token.actions.githubusercontent.com"
  subject             = "repo:${var.github_owner}/${var.github_repository}:ref:refs/heads/main"
}

resource "azurerm_federated_identity_credential" "pull_request" {
  name                = "github-pull-request"
  resource_group_name = azurerm_resource_group.state.name
  parent_id           = azurerm_user_assigned_identity.github.id
  audience            = ["api://AzureADTokenExchange"]
  issuer              = "https://token.actions.githubusercontent.com"
  subject             = "repo:${var.github_owner}/${var.github_repository}:pull_request"
}

resource "azurerm_role_assignment" "state_blob_contributor" {
  scope                = azurerm_storage_account.state.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.github.principal_id
}

resource "azurerm_role_assignment" "application_contributor" {
  scope                = azurerm_resource_group.application.id
  role_definition_name = "Contributor"
  principal_id         = azurerm_user_assigned_identity.github.principal_id
}

resource "azurerm_role_assignment" "application_rbac_administrator" {
  scope                = azurerm_resource_group.application.id
  role_definition_name = "Role Based Access Control Administrator"
  principal_id         = azurerm_user_assigned_identity.github.principal_id
}

