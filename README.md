# 🔍 Synthetic Search MCP Server

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512bd4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![MCP](https://img.shields.io/badge/MCP-Protocol-green)](https://modelcontextprotocol.io/)

A zero-data-retention web search MCP server powered by [Synthetic.new](https://synthetic.new) for Claude Code, OpenCode, and other MCP-compatible clients.

## ✨ Features

- **Zero data retention** - searches aren't stored or logged
- **Fast, reliable web search** for AI coding agents
- **Multiple deployment options** - `dotnet run`, Docker, or .NET global tool
- **Built with .NET 10** for performance and modern C# patterns
- **Fully open source** (MIT License)

## 🚀 Quick Start

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- API key from [synthetic.new](https://synthetic.new)

### 1. Clone and Build

```bash
git clone https://github.com/your-username/synthetic-search-mcp.git
cd synthetic-search-mcp
dotnet build
```

### 2. Configure Claude Desktop

Add to your Claude Desktop configuration:

**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
**Windows**: `%APPDATA%/Claude/claude_desktop_config.json`
**Linux**: `~/.config/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "synthetic-search": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/synthetic-search-mcp"],
      "env": {
        "SYNTHETIC_API_KEY": "your-api-key-here"
      }
    }
  }
}
```

### 3. Restart Claude

The `synthetic_search` tool will now be available in Claude Code.

## 📦 Installation Options

### Option 1: Direct `dotnet run` (Development)

```json
{
  "mcpServers": {
    "synthetic-search": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/synthetic-search-mcp"],
      "env": {
        "SYNTHETIC_API_KEY": "your-api-key-here"
      }
    }
  }
}
```

### Option 2: Published Binary

```bash
dotnet publish -c Release -o /path/to/publish
```

```json
{
  "mcpServers": {
    "synthetic-search": {
      "command": "/path/to/publish/synthetic-search-mcp",
      "env": {
        "SYNTHETIC_API_KEY": "your-api-key-here"
      }
    }
  }
}
```

### Option 3: Docker

```bash
docker build -t synthetic-search-mcp .
```

```json
{
  "mcpServers": {
    "synthetic-search": {
      "command": "docker",
      "args": ["run", "-i", "--rm", "-e", "SYNTHETIC_API_KEY", "synthetic-search-mcp"],
      "env": {
        "SYNTHETIC_API_KEY": "your-api-key-here"
      }
    }
  }
}
```

### Option 4: .NET Global Tool (Future)

```bash
dotnet tool install -g SyntheticSearch.McpServer
```

```json
{
  "mcpServers": {
    "synthetic-search": {
      "command": "synthetic-search-mcp",
      "env": {
        "SYNTHETIC_API_KEY": "your-api-key-here"
      }
    }
  }
}
```

## 🔧 Tool Reference

### `synthetic_search`

Search the web using Synthetic.new API.

**Input:**
```json
{
  "query": "string (required) - The search query to execute"
}
```

**Output:**
```json
{
  "query": "the original query",
  "results": [
    {
      "title": "Result title",
      "url": "https://example.com",
      "snippet": "Search result snippet..."
    }
  ],
  "resultCount": 10
}
```

**Example Usage in Claude:**
```
Search for the latest .NET 10 features using synthetic_search.
```

## 🛠️ Development

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test tests/SyntheticSearchMcp.Tests/SyntheticSearchMcp.Tests.csproj
```

## 🔁 CI/CD and Release

This repository includes GitHub Actions workflows for production delivery:

- **CI** (`.github/workflows/ci.yml`)
  - Runs on pull requests and pushes to `main`
  - Restores dependencies
  - Builds the project in `Release`
  - Runs unit tests
  - Builds Docker image and runs MCP handshake smoke tests

- **CD** (`.github/workflows/cd-images.yml`)
  - Runs on stable semver tag pushes (`vX.Y.Z`) or manual workflow dispatch
  - Publishes multi-arch Docker images (`linux/amd64`, `linux/arm64`) to:
    - `ghcr.io/rudironsoni/synthetic-search-mcp`
    - `docker.io/ronsonirudi/synthetic-search-mcp`
  - Publishes exactly two tags per release:
    - `latest`
    - `vX.Y.Z` (the git tag)

### Release Process

1. Merge changes into `main`
2. Create and push a version tag:

```bash
git tag v1.0.0
git push origin v1.0.0
```

3. GitHub Actions builds and publishes images automatically

### Manual CD Trigger

If needed, you can trigger `CD - Publish Images` manually from the GitHub Actions UI:

- `version_tag`: required semver value, e.g. `v1.0.0`
- `source_ref`: optional git ref to build from (default: `main`)

### Required GitHub Secrets

Configure this repository secret before using the CD workflow:

- `DOCKERHUB_TOKEN`

### Test MCP Protocol Manually

```bash
# Start the server
SYNTHETIC_API_KEY=your-key dotnet run

# In another terminal, send a JSON-RPC request
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | dotnet run --project /path/to/synthetic-search-mcp
```

## 📋 Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `SYNTHETIC_API_KEY` | Yes | Your API key from synthetic.new |
| `DOTNET_ENVIRONMENT` | No | Set to `Development` for verbose logging |

## 🔒 Security

- API keys are read from environment variables only
- No data persistence - searches are not stored
- Logging goes to stderr to avoid interfering with JSON-RPC over stdout

## 📄 License

MIT License - see [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 🙏 Acknowledgments

- [Synthetic.new](https://synthetic.new) for the zero-data-retention search API
- [Model Context Protocol](https://modelcontextprotocol.io/) for the MCP specification
