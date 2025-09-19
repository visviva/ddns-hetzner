[![Release](https://github.com/visviva/ddns-hetzer/actions/workflows/release.yml/badge.svg)](https://github.com/visviva/ddns-hetzer/actions/workflows/release.yml) [![Build and Publish Docker Image](https://github.com/visviva/ddns-hetzer/actions/workflows/docker-build-publish.yml/badge.svg)](https://github.com/visviva/ddns-hetzer/actions/workflows/docker-build-publish.yml)
# Hetzner Dynamic DNS (DDNS) Client

A .NET 9 application that automatically updates DNS records in Hetzner DNS when your public IP address changes. Built with AOT (Ahead-of-Time) compilation for optimal performance and Docker support for easy deployment.

> [!IMPORTANT]  
> The DNS record must already exist. This app will not create DNS records.

## TL;DR - Quick Start

**Docker Run:**
```bash
docker run -d --name ddns-hetzer --restart unless-stopped \
  -e IPV4_URL=https://ipv4.icanhazip.com \
  -e DOMAIN=domain.com \
  -e SUBDOMAIN=ddns \
  -e TOKEN=your_hetzner_api_token \
  ghcr.io/visviva/ddns-hetzner:latest
```
or

**docker-compose.yaml:**
```yaml
services:
  ddns-hetzner:
    image: ghcr.io/visviva/ddns-hetzner:latest
    container_name: ddns-hetzner
    restart: unless-stopped
    environment:
      IPV4_URL: https://ipv4.icanhazip.com
      DOMAIN: domain.com
      SUBDOMAIN: ddns
      TOKEN: your_hetzner_api_token
      INTERVAL: 10
      HEALTH_PORT: 8080
```

Run with: `docker compose up -d`

In the logs you can see:
```bash
=== CURRENT DATE ===
Starting DDNS update process...
✅ Fetched public IPv4 address: 11.22.33.44

Start fetching Zones...
✅ Fetch Zones

Start fetching Records...
✅ Fetch Records

Start updating Record...
✅ Update Record

✅ DNS record updated successfully

ℹ️  Waiting 10 minutes before next check...
```

Get your Hetzner API token from: [https://dns.hetzner.com/](https://dns.hetzner.com/) → API Tokens

## Features

- ✅ Automatic IP address detection and DNS record updates
- ✅ Native AOT compilation for fast startup and low memory usage
- ✅ Docker support for containerized deployment
- ✅ Configurable update intervals
- ✅ Verbose logging support
- ✅ Cross-platform (Windows, Linux, macOS)

## Prerequisites

- .NET 9 SDK (for building from source)
- Docker (for containerized deployment)
- Hetzner DNS API token

## Configuration

The application uses environment variables for configuration:

| Variable      | Description                              | Example                      |
| ------------- | ---------------------------------------- | ---------------------------- |
| `IPV4_URL`    | URL to get your public IPv4 address      | `https://ipv4.icanhazip.com` |
| `DOMAIN`      | Your domain name (DNS zone in Hetzner)   | `example.com`                |
| `SUBDOMAIN`   | The subdomain/hostname to update         | `home`                       |
| `TTL`         | DNS record TTL in seconds                | `7200`                       |
| `TOKEN`       | Your Hetzner DNS API token               | `your_api_token_here`        |
| `INTERVAL`    | Update check interval in minutes         | `10`                         |
| `HEALTH_PORT` | Port for health check API (0 to disable) | `8080`                       |

## Docker Usage

### Quick Start with Docker Run

```bash
docker run --rm \
  -e IPV4_URL=https://ipv4.icanhazip.com \
  -e DOMAIN=your.domain.com \
  -e SUBDOMAIN=home \
  -e TTL=7200 \
  -e TOKEN=your_hetzner_token \
  -e INTERVAL=10 \
  ddns-hetzner --verbose
```

## Building from Source

### Local Build

```bash
# Restore dependencies
dotnet restore

# Build for development
dotnet build

# Run locally
dotnet run
```

### Docker Build

```bash
# Build the Docker image
docker build -t ddns-hetzner .

# Run the container
docker run -d --name ddns-hetzner \
  --restart unless-stopped \
  -e IPV4_URL=https://ipv4.icanhazip.com \
  -e DOMAIN=your.domain.com \
  -e SUBDOMAIN=home \
  -e TTL=7200 \
  -e TOKEN=your_hetzner_token \
  -e INTERVAL=10 \
  ddns-hetzner
```

## Command Line Options

```
Usage: ddns-hetzner [options]

Options:
  -v, --verbose          Enable verbose output
  --create-env-file      Create a sample .env file and exit
  -h, --help             Show help information
```

## Getting Your Hetzner DNS API Token

1. Log in to [Hetzner DNS Console](https://dns.hetzner.com/)
2. Go to "API Tokens"
3. Create a new API token
4. Copy the token and use it in your configuration

## How It Works

1. The application checks your current public IP address
2. Compares it with the current DNS record in Hetzner DNS
3. If different, updates the DNS record with the new IP
4. Waits for the specified interval before checking again

## Health Check API

The service provides a built-in HTTP health check API for monitoring and orchestration tools:

### Endpoints

- **`GET /health`** - Basic health check (HTTP 200 if healthy, 503 if unhealthy)
- **`GET /health/live`** - Liveness probe (always returns HTTP 200)
- **`GET /health/ready`** - Readiness probe (HTTP 200 if service can perform updates)
- **`GET /status`** - Detailed status information (JSON response)

### Usage Examples

```bash
# Basic health check
curl http://localhost:8080/health

# Detailed status
curl http://localhost:8080/status

# Docker health check
docker run --health-cmd="curl -f http://localhost:8080/health || exit 1" \
  --health-interval=30s \
  --health-timeout=10s \
  --health-retries=3 \
  your-ddns-container
```

### Status Response

The `/status` endpoint returns detailed information:

```json
{
  "healthy": true,
  "uptime": "2.5 hours",
  "startTime": "2025-01-19T10:30:00.0000000Z",
  "lastSuccessfulUpdate": "2025-01-19T12:45:00.0000000Z",
  "lastUpdateAttempt": "2025-01-19T12:45:00.0000000Z",
  "timeSinceLastUpdateMinutes": 15,
  "timeSinceLastAttemptMinutes": 15,
  "currentIp": "192.168.1.100",
  "lastError": ""
}
```

### Docker Compose with Health Checks

```yaml
services:
  ddns-hetzner:
    image: ghcr.io/visviva/ddns-hetzner:latest
    ports:
      - "8080:8080"  # Expose health check port
    environment:
      DOMAIN: domain.com
      SUBDOMAIN: ddns
      TOKEN: your_hetzner_api_token
      HEALTH_PORT: 8080
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
```
