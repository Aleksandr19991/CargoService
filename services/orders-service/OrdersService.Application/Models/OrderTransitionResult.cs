namespace OrdersService.Application.Models;

public enum OrderTransitionResult
{
    NotFound,
    Conflict,
    Success,

    // Requested transition matches the order's current status already — treated as a successful
    // no-op (idempotent confirm/cancel) rather than a conflict.
    NoChange,
}
