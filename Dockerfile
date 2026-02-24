# Multi-stage build for Synthetic Search MCP Server
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY synthetic-search-mcp.csproj .
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet publish -c Release -o /app/publish \
  --no-restore \
  --self-contained false \
  /p:UseSharedCompilation=false

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Create non-root user for security
RUN useradd -m -s /bin/bash mcp
USER mcp

# Set entrypoint
ENTRYPOINT ["dotnet", "synthetic-search-mcp.dll"]
