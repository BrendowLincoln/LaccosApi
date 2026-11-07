namespace Lacoos.Modules.Guests.Domain;

internal enum GroupCategory
{
    Family,
    Friends,
    Work,
    Others,
}

internal enum InvitationStatus
{
    NotSent,
    Sent,
    Opened,
}

internal enum AttendanceStatus
{
    Pending,
    Confirmed,
    Declined,
}

internal sealed class GuestGroup : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public required string Name { get; set; }

    public GroupCategory Category { get; set; } = GroupCategory.Others;

    public string? Notes { get; set; }
}

internal sealed class Guest : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public Guid? GroupId { get; set; }

    public GuestGroup? Group { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public bool IsPrimaryGuest { get; set; } = true;

    public int MaxCompanions { get; set; }

    public InvitationStatus InvitationStatus { get; set; } = InvitationStatus.NotSent;

    public AttendanceStatus AttendanceStatus { get; set; } = AttendanceStatus.Pending;

    public string? DietaryRestrictions { get; set; }

    public string? AccessibilityNeeds { get; set; }

    public string? Notes { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    /// <summary>Marcar como enviado nunca rebaixa um convite já visualizado.</summary>
    public void MarkInvitationSent()
    {
        if (InvitationStatus == InvitationStatus.NotSent)
        {
            InvitationStatus = InvitationStatus.Sent;
        }
    }
}
