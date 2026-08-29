# Build context is the repo root (see docker-compose.yml) — needed because
# AiInspectionService.Application/Infrastructure reference ../../../shared/CargoService.Contracts,
# which lives outside services/ai-inspection-service and wouldn't otherwise be
# reachable from a context scoped to this service's own folder.

# Dev stage: source is bind-mounted at runtime by docker-compose.override.yml,
# dotnet watch rebuilds and restarts the app on file changes (hot reload).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /src
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "watch", "--project", "services/ai-inspection-service/AiInspectionService/AiInspectionService.API.csproj", "run", "--urls", "http://+:8080", "--non-interactive"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY services/ai-inspection-service/AiInspectionService/AiInspectionService.API.csproj services/ai-inspection-service/AiInspectionService/
COPY services/ai-inspection-service/AiInspectionService.Application/AiInspectionService.Application.csproj services/ai-inspection-service/AiInspectionService.Application/
COPY services/ai-inspection-service/AiInspectionService.Domain/AiInspectionService.Domain.csproj services/ai-inspection-service/AiInspectionService.Domain/
COPY services/ai-inspection-service/AiInspectionService.Infrastructure/AiInspectionService.Infrastructure.csproj services/ai-inspection-service/AiInspectionService.Infrastructure/
COPY services/ai-inspection-service/AiInspectionService.Persistence/AiInspectionService.Persistence.csproj services/ai-inspection-service/AiInspectionService.Persistence/
COPY shared/CargoService.Contracts/CargoService.Contracts.csproj shared/CargoService.Contracts/

RUN dotnet restore services/ai-inspection-service/AiInspectionService/AiInspectionService.API.csproj

COPY services/ai-inspection-service/ services/ai-inspection-service/
COPY shared/CargoService.Contracts/ shared/CargoService.Contracts/
RUN dotnet publish services/ai-inspection-service/AiInspectionService/AiInspectionService.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Файл модели и размеченный набор в образ не кладутся: это большие бинарные артефакты со
# своим циклом версионирования. Подключаются томом в /models, пути задаются через
# AiModel__Path и AiModel__EvaluationSetPath; без файла сервис работает на заглушке модели.
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "AiInspectionService.API.dll"]
