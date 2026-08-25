# Build context is the repo root (see docker-compose.yml) — needed because
# CargoService.Application/Infrastructure reference ../../../shared/CargoService.Contracts,
# which lives outside services/cargo-service and wouldn't otherwise be
# reachable from a context scoped to this service's own folder.

# Dev stage: source is bind-mounted at runtime by docker-compose.override.yml,
# dotnet watch rebuilds and restarts the app on file changes (hot reload).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /src
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "watch", "--project", "services/cargo-service/CargoService/CargoService.API.csproj", "run", "--urls", "http://+:8080", "--non-interactive"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY services/cargo-service/CargoService/CargoService.API.csproj services/cargo-service/CargoService/
COPY services/cargo-service/CargoService.Application/CargoService.Application.csproj services/cargo-service/CargoService.Application/
COPY services/cargo-service/CargoService.Domain/CargoService.Domain.csproj services/cargo-service/CargoService.Domain/
COPY services/cargo-service/CargoService.Infrastructure/CargoService.Infrastructure.csproj services/cargo-service/CargoService.Infrastructure/
COPY services/cargo-service/CargoService.Persistence/CargoService.Persistence.csproj services/cargo-service/CargoService.Persistence/
COPY shared/CargoService.Contracts/CargoService.Contracts.csproj shared/CargoService.Contracts/

RUN dotnet restore services/cargo-service/CargoService/CargoService.API.csproj

COPY services/cargo-service/ services/cargo-service/
COPY shared/CargoService.Contracts/ shared/CargoService.Contracts/
RUN dotnet publish services/cargo-service/CargoService/CargoService.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CargoService.API.dll"]
