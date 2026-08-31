output "azure_client_id" {
  value = azurerm_user_assigned_identity.github.client_id
}

output "azure_tenant_id" {
  value = azurerm_user_assigned_identity.github.tenant_id
}

output "azure_subscription_id" {
  value = var.subscription_id
}

output "tfstate_resource_group" {
  value = azurerm_resource_group.state.name
}

output "tfstate_storage_account" {
  value = azurerm_storage_account.state.name
}

output "tfstate_container" {
  value = azurerm_storage_container.state.name
}

