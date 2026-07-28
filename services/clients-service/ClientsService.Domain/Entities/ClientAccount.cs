namespace ClientsService.Domain.Entities;

public class ClientAccount
{
    public Guid Id { get; set; }

    // References User.Id in identity-service — no FK/navigation, "database per service" means
    // no cross-service joins; kept in sync via the UserRegistered consumer instead.
    public Guid UserId { get; set; }

    public ICollection<Counterparty> Counterparties { get; set; } = new List<Counterparty>();
}
