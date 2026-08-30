using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Models;

namespace PaymentService.Infrastructure.Payments;

/// <summary>
/// Эквайринг через REST API провайдера (контракт ЮKassa: <c>POST /payments</c>,
/// <c>GET /payments/{id}</c>, <c>POST /refunds</c> с Basic-аутентификацией
/// <c>shopId:secretKey</c> и обязательным заголовком <c>Idempotence-Key</c>).
/// <para>
/// Платёж создаётся с <c>capture: true</c> — одностадийная схема: как только клиент подтвердил
/// оплату, деньги списываются сразу. Двухстадийная (холд с последующим списанием) была бы
/// уместна, если бы сумма уточнялась после приёмки груза, но платформа берёт оплату по уже
/// рассчитанной стоимости подтверждённой заявки, и лишняя стадия означала бы только лишний
/// шанс не дожать холд до списания.
/// </para>
/// </summary>
public class YooKassaPaymentProviderClient(
    HttpClient httpClient,
    PaymentProviderOptions options,
    ILogger<YooKassaPaymentProviderClient> logger) : IPaymentProviderClient
{
    private const int MaxFailureReasonLength = 500;

    /// <summary>
    /// Всё API провайдера — snake_case, поэтому политика именования заменяет полтора десятка
    /// атрибутов <c>[JsonPropertyName]</c> на моделях ниже.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<ProviderPaymentResult> CreatePaymentAsync(
        ProviderPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            amount = new { value = FormatAmount(request.Amount), currency = request.Currency },
            capture = true,
            description = request.Description,
            confirmation = new
            {
                type = "redirect",
                returnUrl = request.ReturnUrl ?? options.ReturnUrl,
            },
            // Заявка едет в metadata, чтобы webhook провайдера можно было сопоставить с ней даже
            // если наша запись платежа почему-то не нашлась по идентификатору транзакции.
            metadata = new { orderId = request.OrderId },
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "payments")
        {
            Content = JsonContent.Create(payload, options: SerializerOptions),
        };
        Authorize(httpRequest, request.IdempotenceKey);

        return await SendPaymentRequestAsync(
            httpRequest,
            $"create payment for order {request.OrderId}",
            cancellationToken);
    }

    public async Task<ProviderPaymentResult> GetPaymentAsync(
        string providerPaymentId,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"payments/{providerPaymentId}");
        // Idempotence-Key обязателен только на создающих запросах; на чтении провайдер его игнорирует.
        Authorize(httpRequest, idempotenceKey: null);

        return await SendPaymentRequestAsync(
            httpRequest,
            $"get payment {providerPaymentId}",
            cancellationToken);
    }

    public async Task<ProviderRefundResult> RefundAsync(
        ProviderRefundRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            paymentId = request.ProviderPaymentId,
            amount = new { value = FormatAmount(request.Amount), currency = request.Currency },
            description = request.Description,
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "refunds")
        {
            Content = JsonContent.Create(payload, options: SerializerOptions),
        };
        Authorize(httpRequest, request.IdempotenceKey);

        var operation = $"refund payment {request.ProviderPaymentId}";

        try
        {
            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return ProviderRefundResult.Failure(await DescribeFailureAsync(response, operation, cancellationToken));

            var refund = await response.Content.ReadFromJsonAsync<ProviderRefundPayload>(SerializerOptions, cancellationToken);
            if (refund?.Id is null)
                return ProviderRefundResult.Failure($"Payment provider returned an empty body on {operation}.");

            logger.LogInformation(
                "Refund {RefundId} for provider payment {PaymentId} is {Status}",
                refund.Id,
                request.ProviderPaymentId,
                refund.Status);

            return ProviderRefundResult.Success(refund.Id, MapStatus(refund.Status), ParseAmount(refund.Amount));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Остановка сервиса, а не отказ провайдера: гасить её результатом-отказом значило бы
            // записать клиенту несостоявшийся возврат при обычном рестарте.
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Payment provider call failed: {Operation}", operation);
            return ProviderRefundResult.Failure($"Payment provider: {exception.Message}");
        }
    }

    private async Task<ProviderPaymentResult> SendPaymentRequestAsync(
        HttpRequestMessage httpRequest,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return ProviderPaymentResult.Failure(await DescribeFailureAsync(response, operation, cancellationToken));

            var payment = await response.Content.ReadFromJsonAsync<ProviderPaymentPayload>(SerializerOptions, cancellationToken);
            if (payment?.Id is null)
                return ProviderPaymentResult.Failure($"Payment provider returned an empty body on {operation}.");

            logger.LogInformation(
                "Provider payment {PaymentId} is {Status} ({Operation})",
                payment.Id,
                payment.Status,
                operation);

            return ProviderPaymentResult.Success(
                payment.Id,
                MapStatus(payment.Status),
                ParseAmount(payment.Amount),
                payment.Confirmation?.ConfirmationUrl,
                payment.CancellationDetails?.Reason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Сюда же приходят исчерпанные политики Polly (retry/circuit breaker навешаны на
            // клиента при регистрации) — для вызывающего это такой же временный отказ.
            logger.LogError(exception, "Payment provider call failed: {Operation}", operation);
            return ProviderPaymentResult.Failure($"Payment provider: {exception.Message}");
        }
    }

    private void Authorize(HttpRequestMessage request, Guid? idempotenceKey)
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.ShopId}:{options.SecretKey}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        if (idempotenceKey.HasValue)
            request.Headers.Add("Idempotence-Key", idempotenceKey.Value.ToString());
    }

    private async Task<string> DescribeFailureAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        // Отказ провайдера (нет средств, заблокированный магазин, невалидная сумма) — рабочий
        // исход: тело ответа несёт причину и должно попасть в запись платежа, но целиком оно
        // там не нужно.
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var reason = $"Payment provider {(int)response.StatusCode} on {operation}: {Truncate(body)}";

        logger.LogError("Payment provider rejected request: {Reason}", reason);
        return reason;
    }

    /// <summary>
    /// Сумма уходит и приходит строкой с двумя знаками после точки — форматирование обязано быть
    /// инвариантным: на русской локали <c>decimal.ToString()</c> дал бы «100,00», и провайдер
    /// отверг бы запрос.
    /// </summary>
    private static string FormatAmount(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

    private static decimal ParseAmount(ProviderAmountPayload? amount) =>
        amount?.Value is not null && decimal.TryParse(amount.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;

    /// <summary>
    /// <c>waiting_for_capture</c> при одностадийной схеме не приходит, но отображается в
    /// <see cref="ProviderOperationStatus.Pending"/>, а не роняет разбор: смена схемы на стороне
    /// магазина не должна превращаться в необработанный статус. Неизвестное значение — тоже
    /// Pending: считать деньги полученными по незнакомому слову нельзя.
    /// </summary>
    private static ProviderOperationStatus MapStatus(string? status) => status switch
    {
        "succeeded" => ProviderOperationStatus.Succeeded,
        "canceled" => ProviderOperationStatus.Canceled,
        _ => ProviderOperationStatus.Pending,
    };

    private static string Truncate(string value) =>
        value.Length <= MaxFailureReasonLength ? value : value[..MaxFailureReasonLength];

    private sealed record ProviderPaymentPayload
    {
        public string? Id { get; init; }
        public string? Status { get; init; }
        public bool Paid { get; init; }
        public ProviderAmountPayload? Amount { get; init; }
        public ProviderConfirmationPayload? Confirmation { get; init; }
        public ProviderCancellationDetailsPayload? CancellationDetails { get; init; }
    }

    private sealed record ProviderRefundPayload
    {
        public string? Id { get; init; }
        public string? Status { get; init; }
        public ProviderAmountPayload? Amount { get; init; }
    }

    private sealed record ProviderAmountPayload
    {
        public string? Value { get; init; }
        public string? Currency { get; init; }
    }

    private sealed record ProviderConfirmationPayload
    {
        public string? Type { get; init; }
        public string? ConfirmationUrl { get; init; }
    }

    private sealed record ProviderCancellationDetailsPayload
    {
        public string? Party { get; init; }
        public string? Reason { get; init; }
    }
}
