output "resource_group_name" {
  value = azurerm_resource_group.main.name
}

output "container_registry_name" {
  description = "Set this as the AZURE_ACR_NAME repository variable for the CD workflow."
  value       = azurerm_container_registry.main.name
}

output "container_registry_login_server" {
  value = azurerm_container_registry.main.login_server
}

output "container_app_name" {
  description = "Set this as the AZURE_CONTAINER_APP repository variable for the CD workflow."
  value       = azurerm_container_app.api.name
}

output "api_url" {
  value = "https://${azurerm_container_app.api.ingress[0].fqdn}"
}

output "mysql_server_fqdn" {
  value = azurerm_mysql_flexible_server.main.fqdn
}
