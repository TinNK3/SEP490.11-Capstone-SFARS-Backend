# ============================================
# SFARS API - Multi-stage Dockerfile
# ============================================
# Build:   docker build -t sfars-api .
# Run:     docker run -p 8080:8080 --env-file .env sfars-api
# ============================================

# ---- Stage 1: Build & Publish ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first (NuGet restore layer cache)
COPY SFARS.sln .
COPY SFARS.Domain/SFARS.Domain.csproj             SFARS.Domain/
COPY SFARS.Application/SFARS.Application.csproj   SFARS.Application/
COPY SFARS.Infrastructure/SFARS.Infrastructure.csproj SFARS.Infrastructure/
COPY SFARS.API/SFARS.API.csproj                   SFARS.API/

# Restore NuGet packages (cached unless csproj files change)
RUN dotnet restore SFARS.API/SFARS.API.csproj

# Copy remaining source code
COPY . .

# Use appsettings.example.json as base config for the image.
# Real secrets are injected via environment variables at runtime.
RUN cp SFARS.API/appsettings.example.json SFARS.API/appsettings.json

# Publish Release build
RUN dotnet publish SFARS.API/SFARS.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---- Stage 2: Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install native dependencies required by ONNX Runtime
RUN apt-get update && \
    apt-get install -y --no-install-recommends libgomp1 && \
    rm -rf /var/lib/apt/lists/*

# Copy published output from build stage
COPY --from=build /app/publish .

# ASP.NET 9 defaults to port 8080
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "SFARS.API.dll"]
