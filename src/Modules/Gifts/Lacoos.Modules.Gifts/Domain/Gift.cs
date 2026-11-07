namespace Lacoos.Modules.Gifts.Domain;

internal enum GiftStatus
{
    Active,
    Cancelled,
}

internal enum GiftDisplayStatus
{
    Available,
    Reserved,
    Received,
    Cancelled,
}

internal enum ReservationStatus
{
    Active,
    Cancelled,
}

internal sealed class GiftCategory : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public required string Name { get; set; }
}

internal sealed class Gift : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public Guid CategoryId { get; set; }

    public GiftCategory? Category { get; set; }

    public decimal EstimatedValue { get; set; }

    public int DesiredQuantity { get; set; } = 1;

    public string? ExternalLink { get; set; }

    public string? ImageUrl { get; set; }

    public GiftStatus Status { get; set; } = GiftStatus.Active;

    public List<GiftReservation> Reservations { get; set; } = [];

    public List<GiftReceipt> Receipts { get; set; } = [];

    /// <summary>Unidades ainda presas à reserva: o reservado menos o que já foi recebido por ela.</summary>
    public int RemainingOf(GiftReservation reservation) =>
        reservation.Status != ReservationStatus.Active
            ? 0
            : Math.Max(reservation.Quantity - Receipts.Where(r => r.ReservationId == reservation.Id).Sum(r => r.Quantity), 0);

    public int ReservedUnits => Reservations.Sum(RemainingOf);

    public int ReceivedUnits => Receipts.Sum(r => r.Quantity);

    public int AvailableUnits => Math.Max(DesiredQuantity - ReservedUnits - ReceivedUnits, 0);

    public GiftDisplayStatus DisplayStatus
    {
        get
        {
            if (Status == GiftStatus.Cancelled)
            {
                return GiftDisplayStatus.Cancelled;
            }

            if (AvailableUnits > 0)
            {
                return GiftDisplayStatus.Available;
            }

            return ReservedUnits > 0 ? GiftDisplayStatus.Reserved : GiftDisplayStatus.Received;
        }
    }
}

internal sealed class GiftReservation : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public Guid GiftId { get; set; }

    public int Quantity { get; set; }

    public Guid? GuestId { get; set; }

    public string? PersonName { get; set; }

    public DateOnly ReservedAt { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
}

internal sealed class GiftReceipt : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public Guid GiftId { get; set; }

    public int Quantity { get; set; }

    public Guid? GuestId { get; set; }

    public string? PersonName { get; set; }

    public DateOnly ReceivedAt { get; set; }

    public Guid? ReservationId { get; set; }
}
