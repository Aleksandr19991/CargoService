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
