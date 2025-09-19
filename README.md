# Hetzner Dynamic DNS (DDNS) Client

A .NET 9 application that automatically updates DNS records in Hetzner DNS when your public IP address changes. Built with AOT (Ahead-of-Time) compilation for optimal performance and Docker support for easy deployment.

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

| Variable    | Description                            | Example                      |
| ----------- | -------------------------------------- | ---------------------------- |
| `IPV4_URL`  | URL to get your public IPv4 address    | `https://ipv4.icanhazip.com` |
| `DOMAIN`    | Your domain name (DNS zone in Hetzner) | `example.com`                |
| `SUBDOMAIN` | The subdomain/hostname to update       | `home`                       |
| `TTL`       | DNS record TTL in seconds              | `7200`                       |
| `TOKEN`     | Your Hetzner DNS API token             | `your_api_token_here`        |
| `INTERVAL`  | Update check interval in minutes       | `10`                         |

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
