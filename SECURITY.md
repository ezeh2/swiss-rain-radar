# Security policy

Please do not publish credentials, Azure subscription details, storage keys, Terraform state, or private radar data in an issue.

Report a vulnerability through GitHub's private vulnerability reporting feature when it is enabled for this repository. Otherwise contact the repository owner privately.

The application deliberately uses Azure Managed Identity for data access and GitHub Actions OIDC for deployment. Shared-key access on the application storage account is disabled by Terraform.

