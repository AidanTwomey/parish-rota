# .NET 10 SDK on Ubuntu 24.04 (noble).
# Use the `10.0` tag, not `1-10.0` — the latter is currently pinned to an RC SDK
# (10.0.100-rc.2), while `10.0` carries GA (10.0.3xx).
FROM mcr.microsoft.com/devcontainers/dotnet:10.0

# Azure Functions Core Tools v4, from the Microsoft package feed for noble.
RUN curl -fsSL https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb \
        -o /tmp/packages-microsoft-prod.deb \
    && dpkg -i /tmp/packages-microsoft-prod.deb \
    && rm /tmp/packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends azure-functions-core-tools-4 \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*
