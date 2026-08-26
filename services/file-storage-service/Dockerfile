# Контекст сборки — корень репозитория (build.context: . в docker-compose.yml). На
# shared/CargoService.Contracts этот сервис, в отличие от остальных, не ссылается — в обмене
# событиями он не участвует, — но Directory.Build.props и Directory.Packages.props лежат в корне
# и нужны любому проекту (без второго не работает централизованное управление версиями пакетов),
# так что более узкий контекст всё равно не подошёл бы.

# Dev stage: source is bind-mounted at runtime by docker-compose.override.yml,
# dotnet watch rebuilds and restarts the app on file changes (hot reload).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /src
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "watch", "--project", "services/file-storage-service/FileStorageService/FileStorageService.API.csproj", "run", "--urls", "http://+:8080", "--non-interactive"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY services/file-storage-service/FileStorageService/FileStorageService.API.csproj services/file-storage-service/FileStorageService/
COPY services/file-storage-service/FileStorageService.Application/FileStorageService.Application.csproj services/file-storage-service/FileStorageService.Application/
COPY services/file-storage-service/FileStorageService.Infrastructure/FileStorageService.Infrastructure.csproj services/file-storage-service/FileStorageService.Infrastructure/

RUN dotnet restore services/file-storage-service/FileStorageService/FileStorageService.API.csproj

COPY services/file-storage-service/ services/file-storage-service/
RUN dotnet publish services/file-storage-service/FileStorageService/FileStorageService.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "FileStorageService.API.dll"]
