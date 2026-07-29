# Build context is the repo root (see docker-compose.yml) — needed because
# PricingService.Application/Infrastructure reference ../../../shared/CargoService.Contracts,
# which lives outside services/pricing-service and wouldn't otherwise be
# reachable from a context scoped to this service's own folder.

# Dev stage: source is bind-mounted at runtime by docker-compose.override.yml,
# dotnet watch rebuilds and restarts the app on file changes (hot reload).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /src
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "watch", "--project", "services/pricing-service/PricingService/PricingService.API.csproj", "run", "--urls", "http://+:8080", "--non-interactive"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY services/pricing-service/PricingService/PricingService.API.csproj services/pricing-service/PricingService/
COPY services/pricing-service/PricingService.Application/PricingService.Application.csproj services/pricing-service/PricingService.Application/
COPY services/pricing-service/PricingService.Domain/PricingService.Domain.csproj services/pricing-service/PricingService.Domain/
COPY services/pricing-service/PricingService.Infrastructure/PricingService.Infrastructure.csproj services/pricing-service/PricingService.Infrastructure/
COPY services/pricing-service/PricingService.Persistence/PricingService.Persistence.csproj services/pricing-service/PricingService.Persistence/
COPY shared/CargoService.Contracts/CargoService.Contracts.csproj shared/CargoService.Contracts/

RUN dotnet restore services/pricing-service/PricingService/PricingService.API.csproj

COPY services/pricing-service/ services/pricing-service/
COPY shared/CargoService.Contracts/ shared/CargoService.Contracts/
RUN dotnet publish services/pricing-service/PricingService/PricingService.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PricingService.API.dll"]
