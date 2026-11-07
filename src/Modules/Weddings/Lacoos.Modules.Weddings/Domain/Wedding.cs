namespace Lacoos.Modules.Weddings.Domain;

internal enum WeddingStatus
{
    Planning,
    Confirmed,
    Done,
    Cancelled,
}

internal enum MemberStatus
{
    Active,
    Invited,
}

internal sealed class Wedding
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public DateOnly WeddingDate { get; set; }

    public int EstimatedGuestCount { get; set; }

    public WeddingStatus Status { get; set; } = WeddingStatus.Planning;

    public string? Partner1 { get; set; }

    public string? Partner2 { get; set; }

    public TimeOnly? Time { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Venue { get; set; }

    public string? Notes { get; set; }

    public string? CoverImage { get; set; }
}

internal sealed class WeddingMember
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    /// <summary>Nulo enquanto o convite não é aceito.</summary>
    public Guid? UserId { get; set; }

    public required string Name { get; set; }

    public required string Email { get; set; }

    public required string NormalizedEmail { get; set; }

    public WeddingRole Role { get; set; }

    public MemberStatus Status { get; set; }

    /// <summary>Data do convite enquanto pendente; data do aceite depois.</summary>
    public DateOnly JoinedAt { get; set; }

    public string? Avatar { get; set; }

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

internal sealed class MemberInvitation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MemberId { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
