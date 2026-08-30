# Build context is the repo root (see docker-compose.yml) — needed because
# DocumentService.Application/Infrastructure reference ../../../shared/CargoService.Contracts,
# which lives outside services/document-service and wouldn't otherwise be
# reachable from a context scoped to this service's own folder.

# Dev stage: source is bind-mounted at runtime by docker-compose.override.yml,
# dotnet watch rebuilds and restarts the app on file changes (hot reload).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /src
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "watch", "--project", "services/document-service/DocumentService/DocumentService.API.csproj", "run", "--urls", "http://+:8080", "--non-interactive"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY services/document-service/DocumentService/DocumentService.API.csproj services/document-service/DocumentService/
COPY services/document-service/DocumentService.Application/DocumentService.Application.csproj services/document-service/DocumentService.Application/
COPY services/document-service/DocumentService.Domain/DocumentService.Domain.csproj services/document-service/DocumentService.Domain/
COPY services/document-service/DocumentService.Infrastructure/DocumentService.Infrastructure.csproj services/document-service/DocumentService.Infrastructure/
COPY services/document-service/DocumentService.Persistence/DocumentService.Persistence.csproj services/document-service/DocumentService.Persistence/
COPY shared/CargoService.Contracts/CargoService.Contracts.csproj shared/CargoService.Contracts/

RUN dotnet restore services/document-service/DocumentService/DocumentService.API.csproj

COPY services/document-service/ services/document-service/
COPY shared/CargoService.Contracts/ shared/CargoService.Contracts/
RUN dotnet publish services/document-service/DocumentService/DocumentService.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Шрифт документов вшит в сборку (см. DocumentFonts), поэтому пакеты шрифтов в образ не
# ставятся: вид печатного документа не должен зависеть от базового образа.
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "DocumentService.API.dll"]
