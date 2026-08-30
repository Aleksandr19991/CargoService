using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using PaymentService.API.Models.Requests;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Models;

namespace PaymentService.API.Controllers;

/// <summary>
/// Приём уведомлений платёжного провайдера (spec.md §3.2).
/// <para>
/// Эндпоинт анонимный — иначе и быть не может: стучится сюда чужой сервер, а не пользователь
/// платформы, и токена Keycloak у него нет. Защитой служит не аутентификация запроса, а то, что
/// его телу не верят: статус платежа перечитывается у провайдера (см.
/// <c>PaymentWebhooksService</c>), поэтому знание идентификатора платежа само по себе не даёт
/// объявить заявку оплаченной. Аутентификация Keycloak появится здесь вместе с первым
/// эндпоинтом для людей — своего API у сервиса пока нет.
/// </para>
/// </summary>
[Route("api/payments")]
[ApiController]
public class PaymentsController(IPaymentWebhooksService webhooks, IMapper mapper) : ControllerBase
{
    /// <summary>
    /// Уведомление об исходе платежа.
    /// <para>
    /// Коды ответа выбираются под то, как провайдеры обращаются с webhook: любой не-2xx означает
    /// «пришли ещё раз», и повторять имеет смысл только то, что может получиться позже. Поэтому
    /// неизвестный платёж и уже применённое уведомление получают <c>200</c> (повтор их не
    /// изменит), а недоступность провайдера при перепроверке — <c>503</c>.
    /// </para>
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook(
        [FromBody] PaymentWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var notification = mapper.Map<PaymentWebhookNotification>(request);

        var outcome = await webhooks.HandleAsync(notification, cancellationToken);

        return outcome == WebhookOutcome.TemporaryFailure
            ? StatusCode(StatusCodes.Status503ServiceUnavailable)
            : Ok();
    }
}
