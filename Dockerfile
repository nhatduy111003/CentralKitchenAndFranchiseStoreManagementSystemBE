# =========================
# Build stage
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Force-disable any fallback folders (Windows/VS paths) in Linux container
ENV NUGET_FALLBACK_PACKAGES=""
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Copy csproj files first (for better caching)
COPY CentralKitchenAndFranchise.API/CentralKitchenAndFranchise.API.csproj CentralKitchenAndFranchise.API/
COPY CentralKitchenAndFranchise.BLL/CentralKitchenAndFranchise.BLL.csproj CentralKitchenAndFranchise.BLL/
COPY CentralKitchenAndFranchise.DAL/CentralKitchenAndFranchise.DAL.csproj CentralKitchenAndFranchise.DAL/
COPY CentralKitchenAndFranchise.DTO/CentralKitchenAndFranchise.DTO.csproj CentralKitchenAndFranchise.DTO/

# Restore
RUN dotnet restore CentralKitchenAndFranchise.API/CentralKitchenAndFranchise.API.csproj

# Copy everything else
COPY . .

# Publish API
RUN dotnet publish CentralKitchenAndFranchise.API/CentralKitchenAndFranchise.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


# =========================
# Runtime stage
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Render provides PORT env variable
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CentralKitchenAndFranchise.API.dll"]
