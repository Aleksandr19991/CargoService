using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiInspectionService.Application.Models;
using AiInspectionService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AiInspectionService.IntegrationTests;

public class InspectionsControllerTests(InspectionApiFactory factory) : IClassFixture<InspectionApiFactory>
{
    // API отдаёт enum'ы строками (JsonStringEnumConverter в AddApiServices) — читаем ответы теми
    // же правилами, что и настоящий клиент.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task CreateInspection_WithoutToken_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/inspections",
            new { shipmentId = Guid.NewGuid(), photoFileIds = new[] { Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateInspection_AsClient_ReturnsForbidden()
    {
        // Клиент вердикт модели видит через акт приёмки в cargo-service, а не запускает проверки.
        var response = await CreateClient("Client").PostAsJsonAsync(
            "/api/inspections",
            new { shipmentId = Guid.NewGuid(), photoFileIds = new[] { Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateInspection_QueuesJobAndReturnsLocation()
    {
        var shipmentId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        var response = await CreateClient().PostAsJsonAsync(
            "/api/inspections",
            new { shipmentId, photoFileIds = new[] { photoId, photoId } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var job = await response.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(job);
        Assert.Equal(InspectionJobStatus.Queued, job.Status);

        // Повторённый снимок схлопнут при постановке — иначе уникальность (JobId, FileId)
        // сработала бы уже после инференса.
        Assert.Equal([photoId], job.PhotoFileIds);

        await using var context = factory.CreateDbContext();
        var stored = await context.InspectionJobs.SingleAsync(entity => entity.Id == job.Id);
        Assert.Equal(shipmentId, stored.ShipmentId);
        Assert.Equal(InspectionJobStatus.Queued, stored.Status);
    }

    [Theory]
    [InlineData("""{"shipmentId":"00000000-0000-0000-0000-000000000000","photoFileIds":["11111111-1111-1111-1111-111111111111"]}""")]
    [InlineData("""{"shipmentId":"11111111-1111-1111-1111-111111111111","photoFileIds":[]}""")]
    [InlineData("""{"shipmentId":"11111111-1111-1111-1111-111111111111","photoFileIds":["00000000-0000-0000-0000-000000000000"]}""")]
    public async Task CreateInspection_RejectsInvalidBody(string body)
    {
        var response = await CreateClient().PostAsync(
            "/api/inspections",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetInspection_ReturnsJobWithVerdicts()
    {
        var seeded = await factory.SeedJobAsync();

        var job = await CreateClient().GetFromJsonAsync<JobDto>($"/api/inspections/{seeded.Id}", JsonOptions);

        Assert.NotNull(job);
        Assert.Equal(InspectionJobStatus.Completed, job.Status);
        var verdict = Assert.Single(job.Results);
        Assert.True(verdict.DamageDetected);
        Assert.Equal("test-model", verdict.ModelVersion);
    }

    [Fact]
    public async Task GetInspection_DoesNotExposeRawModelOutput()
    {
        var seeded = await factory.SeedJobAsync();

        var payload = await CreateClient().GetStringAsync($"/api/inspections/{seeded.Id}");

        Assert.DoesNotContain("rawResponse", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("packagingIntegrityScore", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetInspection_ReturnsNotFoundForUnknownJob()
    {
        var response = await CreateClient().GetAsync($"/api/inspections/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_AsWarehouseOperator_ReturnsForbidden()
    {
        // Оценка модели и выбор порога — не работа склада.
        var response = await CreateClient("WarehouseOperator").PostAsync("/api/inspections/evaluate", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_WithoutEvaluationSet_ReturnsConflict()
    {
        factory.EvaluationSet.Clear();

        var response = await CreateClient("Manager").PostAsync("/api/inspections/evaluate", null);

        // Набора нет — это неготовность окружения, а не сбой и не «не найдено».
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_ReturnsConfusionMatrixPerThreshold()
    {
        factory.EvaluationSet.Clear();
        factory.EvaluationSet.AddRange(
        [
            Sample("damaged-1", predictDamage: true, isDamaged: true),
            Sample("damaged-2", predictDamage: false, isDamaged: true),
            Sample("intact-1", predictDamage: false, isDamaged: false),
            Sample("intact-2", predictDamage: true, isDamaged: false),
        ]);

        var report = await CreateClient("Manager")
            .PostAsync("/api/inspections/evaluate", null)
            .ContinueWith(task => task.Result.Content.ReadFromJsonAsync<EvaluationDto>(JsonOptions))
            .Unwrap();

        Assert.NotNull(report);
        Assert.Equal(4, report.SampleCount);
        Assert.Equal(2, report.DamagedSampleCount);
        Assert.Equal("test-model", report.ModelVersion);
        Assert.Equal(0.7, report.CurrentThreshold);

        // Заглушка модели уверена на 0.9, поэтому до порога 0.9 картина одна и та же:
        // одно верное обнаружение, одна ложная тревога, один пропуск, один спокойный снимок.
        var atSeventy = report.Thresholds.Single(matrix => matrix.Threshold == 0.7);
        Assert.Equal(1, atSeventy.TruePositives);
        Assert.Equal(1, atSeventy.FalsePositives);
        Assert.Equal(1, atSeventy.TrueNegatives);
        Assert.Equal(1, atSeventy.FalseNegatives);

        // На 0.95 уверенности уже не хватает — обнаружений не остаётся вовсе.
        var atNinetyFive = report.Thresholds.Single(matrix => matrix.Threshold == 0.95);
        Assert.Equal(0, atNinetyFive.TruePositives);
        Assert.Equal(0, atNinetyFive.FalsePositives);
        Assert.Equal(2, atNinetyFive.TrueNegatives);
        Assert.Equal(2, atNinetyFive.FalseNegatives);
    }

    private static LabelledSample Sample(string name, bool predictDamage, bool isDamaged) => new()
    {
        Name = name,
        // Заглушка модели в фабрике считает снимок повреждённым по первому байту.
        ImageBytes = Encoding.UTF8.GetBytes(predictDamage ? $"d{name}" : $"i{name}"),
        IsDamaged = isDamaged,
    };

    private HttpClient CreateClient(string roles = "WarehouseOperator")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    private sealed record JobDto(
        Guid Id,
        Guid ShipmentId,
        List<Guid> PhotoFileIds,
        InspectionJobStatus Status,
        List<ResultDto> Results);

    private sealed record ResultDto(Guid FileId, bool DamageDetected, double Confidence, string ModelVersion);

    private sealed record EvaluationDto(
        string ModelVersion,
        int SampleCount,
        int DamagedSampleCount,
        double RecommendedThreshold,
        double CurrentThreshold,
        List<MatrixDto> Thresholds);

    private sealed record MatrixDto(
        double Threshold,
        int TruePositives,
        int FalsePositives,
        int TrueNegatives,
        int FalseNegatives);
}
