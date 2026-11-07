namespace Lacoos.Modules.Checklist.Domain;

internal enum ChecklistPriority
{
    Low,
    Medium,
    High,
}

internal enum ChecklistStatus
{
    Pending,
    InProgress,
    Done,
}

internal sealed class ChecklistCategory : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public bool Active { get; set; } = true;
}

internal sealed class ChecklistItem : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public Guid? CategoryId { get; set; }

    public ChecklistCategory? Category { get; set; }

    public Guid? AssignedToMemberId { get; set; }

    public DateOnly? DueDate { get; set; }

    public ChecklistPriority Priority { get; set; } = ChecklistPriority.Medium;

    public ChecklistStatus Status { get; set; } = ChecklistStatus.Pending;
}
