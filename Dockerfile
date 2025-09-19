# Multi-stage build for AOT compilation
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Install native AOT prerequisites
RUN apt-get update && apt-get install -y \
    clang \
    zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy project files
COPY *.csproj ./
COPY *.sln ./
RUN dotnet restore

# Copy source code
COPY . .

# Build and publish with AOT
RUN dotnet publish -c Release -r linux-x64 --self-contained true -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0 AS runtime
WORKDIR /app

# Copy the published AOT binary
COPY --from=build /app/publish ./

# Make the binary executable
RUN chmod +x ddns-hetzner

# Set the entry point to the AOT compiled binary
ENTRYPOINT ["./ddns-hetzner"]
