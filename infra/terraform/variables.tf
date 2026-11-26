variable "project_name" {
  description = "Short name used as a prefix for every resource."
  type        = string
  default     = "logisticdesk"

  validation {
    condition     = can(regex("^[a-z0-9]{3,16}$", var.project_name))
    error_message = "Use 3-16 lowercase letters or digits (it is part of the registry name)."
  }
}

variable "environment" {
  description = "Deployment environment name, e.g. dev or prod."
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region for all resources."
  type        = string
  default     = "westeurope"
}

variable "image_tag" {
  description = "Tag of the API image in the registry used on the first deployment. Later deployments are done by the CD workflow."
  type        = string
  default     = "latest"
}

variable "mysql_admin_login" {
  description = "Administrator user name for the MySQL flexible server."
  type        = string
  default     = "logisticadmin"
}

variable "mysql_sku_name" {
  description = "MySQL flexible server SKU. B_Standard_B1ms is the cheapest burstable tier."
  type        = string
  default     = "B_Standard_B1ms"
}

variable "cors_allowed_origins" {
  description = "Frontend origins allowed by the API's CORS policy."
  type        = list(string)
  default     = []
}

variable "enable_swagger" {
  description = "Expose Swagger UI in the deployed API (handy for a portfolio demo)."
  type        = bool
  default     = true
}

variable "min_replicas" {
  description = "Minimum container replicas. 0 lets the app scale to zero when idle."
  type        = number
  default     = 0
}

variable "max_replicas" {
  description = "Maximum container replicas."
  type        = number
  default     = 2
}

variable "tags" {
  description = "Extra tags applied to every resource."
  type        = map(string)
  default     = {}
}
