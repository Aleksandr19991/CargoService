# Build context is the repo root (see docker-compose.yml) — needed because
# IdentityService.Application references ../../../shared/CargoService.Contracts,
# which lives outside services/identity-service and wouldn't otherwise be
# reachable from a context scoped to this service's own folder.

# Dev stage: source is bind-mounted at runtime by docker-compose.override.yml,
# dotnet watch rebuilds and restarts the app on file changes (hot reload).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /src
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "watch", "--project", "services/identity-service/IdentityService/IdentityService.API.csproj", "run", "--urls", "http://+:8080", "--non-interactive"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY services/identity-service/IdentityService/IdentityService.API.csproj services/identity-service/IdentityService/
COPY services/identity-service/IdentityService.Application/IdentityService.Application.csproj services/identity-service/IdentityService.Application/
COPY services/identity-service/IdentityService.Domain/IdentityService.Domain.csproj services/identity-service/IdentityService.Domain/
COPY services/identity-service/IdentityService.Infrastructure/IdentityService.Infrastructure.csproj services/identity-service/IdentityService.Infrastructure/
COPY services/identity-service/IdentityService.Persistence/IdentityService.Persistence.csproj services/identity-service/IdentityService.Persistence/
COPY shared/CargoService.Contracts/CargoService.Contracts.csproj shared/CargoService.Contracts/

RUN dotnet restore services/identity-service/IdentityService/IdentityService.API.csproj

COPY services/identity-service/ services/identity-service/
COPY shared/CargoService.Contracts/ shared/CargoService.Contracts/
RUN dotnet publish services/identity-service/IdentityService/IdentityService.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "IdentityService.API.dll"]
