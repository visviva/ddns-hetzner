# Use official .NET runtime image
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

# Copy build output
COPY bin/Debug/net9.0/ ./

# Expose ports if needed (optional)
# EXPOSE 80

# Enable IPv6 (container must be run with IPv6 enabled)
# No special config needed in .NET, but Docker host must support IPv6

ENTRYPOINT ["dotnet", "ddns-hetzner.dll"]
