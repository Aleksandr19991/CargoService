# Dev stage: source is bind-mounted at runtime by docker-compose.override.yml,
# dotnet watch rebuilds and restarts the app on file changes (hot reload).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /src
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "watch", "--project", "IdentityService/IdentityService.API.csproj", "run", "--urls", "http://+:8080", "--non-interactive"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY IdentityService/IdentityService.API.csproj IdentityService/
COPY IdentityService.Application/IdentityService.Application.csproj IdentityService.Application/
COPY IdentityService.Domain/IdentityService.Domain.csproj IdentityService.Domain/
COPY IdentityService.Persistence/IdentityService.Persistence.csproj IdentityService.Persistence/

RUN dotnet restore IdentityService/IdentityService.API.csproj

COPY . .
RUN dotnet publish IdentityService/IdentityService.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "IdentityService.API.dll"]
