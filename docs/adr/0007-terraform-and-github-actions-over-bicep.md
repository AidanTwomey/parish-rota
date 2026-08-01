---
status: accepted (supersedes the Bicep choice in ADR-0006)
---

# Terraform and GitHub Actions for infrastructure and deployment

ADR 0006 named Bicep as the infrastructure-as-code tool. We are using **Terraform** instead, because it is what the author already knows — on a volunteer-maintained parish project, tool familiarity matters more than Azure-nativeness, and a stalled deployment is a worse outcome than a slightly less idiomatic one. Everything else in ADR 0006 (Azure Functions on the Consumption plan, Cosmos DB free tier) stands.

CI/CD is GitHub Actions authenticating to Azure via **OIDC federated credentials** rather than a stored service principal secret, so there is no long-lived Azure credential in the repository to rotate or leak. Terraform state lives in an Azure Storage account created once by `infra/bootstrap.sh` and accessed with Entra auth rather than a storage access key.

## Consequences

- The `prod` GitHub Environment gates `terraform apply` and the Function App deploy; adding a required reviewer there turns both into manual approvals.
- Because the apply job declares `environment: prod`, its OIDC subject claim differs from the plan job's. A credential is therefore required per subject (`ref:refs/heads/main`, `pull_request`, `environment:prod`) — a missing one shows up as `AADSTS700213`, which names the presented subject and is the fastest way to diagnose it.
- This repository was created after 15 July 2026, so GitHub presents an **immutable** subject claim embedding numeric owner and repository IDs (`repo:OWNER@706509/REPO@1319307839:environment:prod`). Those IDs cannot be removed via sub-claim customization, so credentials must match the immutable form. `bootstrap.sh` reads the IDs from the GitHub API and registers both forms, so it works either side of the cutoff.
- Secrets reach the Function App as plain app settings via `TF_VAR_*`. That is adequate for an MVP but means the values are visible to anyone with portal access; moving them to Key Vault references is the intended follow-up.
