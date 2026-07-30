# Build context is the repo root (see docker-compose.yml) — needed because
# OrdersService.Application/Infrastructure reference ../../../shared/CargoService.Contracts,
# which lives outside services/orders-service and wouldn't otherwise be
# reachable from a context scoped to this service's own folder.

# Dev stage: source is bind-mounted at runtime by docker-compose.override.yml,
# dotnet watch rebuilds and restarts the app on file changes (hot reload).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /src
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "watch", "--project", "services/orders-service/OrdersService/OrdersService.API.csproj", "run", "--urls", "http://+:8080", "--non-interactive"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY services/orders-service/OrdersService/OrdersService.API.csproj services/orders-service/OrdersService/
COPY services/orders-service/OrdersService.Application/OrdersService.Application.csproj services/orders-service/OrdersService.Application/
COPY services/orders-service/OrdersService.Domain/OrdersService.Domain.csproj services/orders-service/OrdersService.Domain/
COPY services/orders-service/OrdersService.Infrastructure/OrdersService.Infrastructure.csproj services/orders-service/OrdersService.Infrastructure/
COPY services/orders-service/OrdersService.Persistence/OrdersService.Persistence.csproj services/orders-service/OrdersService.Persistence/
COPY shared/CargoService.Contracts/CargoService.Contracts.csproj shared/CargoService.Contracts/

RUN dotnet restore services/orders-service/OrdersService/OrdersService.API.csproj

COPY services/orders-service/ services/orders-service/
COPY shared/CargoService.Contracts/ shared/CargoService.Contracts/
RUN dotnet publish services/orders-service/OrdersService/OrdersService.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "OrdersService.API.dll"]
