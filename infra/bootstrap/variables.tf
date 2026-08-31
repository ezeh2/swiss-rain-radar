variable "subscription_id" {
  type        = string
  description = "Azure subscription used by the project."
}

variable "location" {
  type        = string
  description = "Azure region for bootstrap resources."
  default     = "switzerlandnorth"
}

variable "github_owner" {
  type        = string
  description = "GitHub account owning the repository."
  default     = "ezeh2"
}

variable "github_repository" {
  type        = string
  description = "GitHub repository name."
  default     = "swiss-rain-radar"
}

variable "application_resource_group_name" {
  type        = string
  description = "Resource group subsequently populated by the main Terraform module."
  default     = "rg-swiss-rain-radar"
}

