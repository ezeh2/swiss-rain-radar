variable "subscription_id" {
  description = "Azure subscription in which the application is deployed."
  type        = string
}

variable "location" {
  description = "Azure region for all resources."
  type        = string
  default     = "switzerlandnorth"
}

variable "resource_group_name" {
  description = "Resource group for the application."
  type        = string
  default     = "rg-swiss-rain-radar"
}

variable "app_name" {
  description = "Globally unique prefix used for the web app."
  type        = string
  default     = "swiss-rain-radar"
}

variable "tags" {
  description = "Tags applied to resources."
  type        = map(string)
  default = {
    application = "swiss-rain-radar"
    managed-by  = "terraform"
  }
}

