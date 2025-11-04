# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/FmsMonitor.Api/FmsMonitor.Api.csproj", "src/FmsMonitor.Api/"]
RUN dotnet restore "src/FmsMonitor.Api/FmsMonitor.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/FmsMonitor.Api"
RUN dotnet build "FmsMonitor.Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "FmsMonitor.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=publish /app/publish .

# Create logs directory
RUN mkdir -p /app/logs

# Expose ports
EXPOSE 80
EXPOSE 443

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/health || exit 1

ENTRYPOINT ["dotnet", "FmsMonitor.Api.dll"]
