using FileStorageService.API.Configuration;
using FileStorageService.Infrastructure.Configuration;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilogLogging();

builder.Services.AddApiServices();

// Ни AddPersistence, ни миграций: у сервиса нет своей БД — состояние целиком живёт в объектном
// хранилище, а ключ объекта выводится из идентификатора файла (см. MinioFileStorage).
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKeycloakAuthentication(builder.Configuration);

var app = builder.Build();

app.MapDevelopmentEndpoints();

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
