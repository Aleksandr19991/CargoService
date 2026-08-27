# Build context is the repo root (see docker-compose.yml) — needed because
# NotificationService.Application/Infrastructure reference ../../../shared/CargoService.Contracts,
# which lives outside services/notification-service and wouldn't otherwise be
# reachable from a context scoped to this service's own folder.

# Dev stage: source is bind-mounted at runtime by docker-compose.override.yml,
# dotnet watch rebuilds and restarts the app on file changes (hot reload).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /src
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "watch", "--project", "services/notification-service/NotificationService/NotificationService.API.csproj", "run", "--urls", "http://+:8080", "--non-interactive"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY services/notification-service/NotificationService/NotificationService.API.csproj services/notification-service/NotificationService/
COPY services/notification-service/NotificationService.Application/NotificationService.Application.csproj services/notification-service/NotificationService.Application/
COPY services/notification-service/NotificationService.Domain/NotificationService.Domain.csproj services/notification-service/NotificationService.Domain/
COPY services/notification-service/NotificationService.Infrastructure/NotificationService.Infrastructure.csproj services/notification-service/NotificationService.Infrastructure/
COPY services/notification-service/NotificationService.Persistence/NotificationService.Persistence.csproj services/notification-service/NotificationService.Persistence/
COPY shared/CargoService.Contracts/CargoService.Contracts.csproj shared/CargoService.Contracts/

RUN dotnet restore services/notification-service/NotificationService/NotificationService.API.csproj

COPY services/notification-service/ services/notification-service/
COPY shared/CargoService.Contracts/ shared/CargoService.Contracts/
RUN dotnet publish services/notification-service/NotificationService/NotificationService.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "NotificationService.API.dll"]
