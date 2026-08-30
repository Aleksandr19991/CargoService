using Microsoft.Extensions.DependencyInjection;

namespace PaymentService.Application.Configuration;

public static class ServicesConfiguration
{
    // No Application services yet — сервисы сценариев (выставление счёта по заявке, проведение
    // оплаты, возврат при отмене) появятся в следующих задачах Фазы 9.
    public static void AddApplicationServices(this IServiceCollection services)
    {
    }
}
