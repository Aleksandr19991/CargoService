# Build context is the repo root (see docker-compose.yml) — needed because
# ClientsService.Infrastructure references ../../../shared/CargoService.Contracts,
# which lives outside services/clients-service and wouldn't otherwise be
# reachable from a context scoped to this service's own folder.

# Dev stage: source is bind-mounted at runtime by docker-compose.override.yml,
# dotnet watch rebuilds and restarts the app on file changes (hot reload).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /src
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "watch", "--project", "services/clients-service/ClientsService/ClientsService.API.csproj", "run", "--urls", "http://+:8080", "--non-interactive"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY services/clients-service/ClientsService/ClientsService.API.csproj services/clients-service/ClientsService/
COPY services/clients-service/ClientsService.Application/ClientsService.Application.csproj services/clients-service/ClientsService.Application/
COPY services/clients-service/ClientsService.Domain/ClientsService.Domain.csproj services/clients-service/ClientsService.Domain/
COPY services/clients-service/ClientsService.Infrastructure/ClientsService.Infrastructure.csproj services/clients-service/ClientsService.Infrastructure/
COPY services/clients-service/ClientsService.Persistence/ClientsService.Persistence.csproj services/clients-service/ClientsService.Persistence/
COPY shared/CargoService.Contracts/CargoService.Contracts.csproj shared/CargoService.Contracts/

RUN dotnet restore services/clients-service/ClientsService/ClientsService.API.csproj

COPY services/clients-service/ services/clients-service/
COPY shared/CargoService.Contracts/ shared/CargoService.Contracts/
RUN dotnet publish services/clients-service/ClientsService/ClientsService.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ClientsService.API.dll"]
