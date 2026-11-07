namespace Lacoos.Modules.Suppliers.Domain;

internal enum NegotiationStatus
{
    Researching,
    Contacted,
    Quoted,
    Negotiating,
    Hired,
    Cancelled,
}

internal enum ServiceStatus
{
    Quoted,
    Contracted,
    Cancelled,
}

internal enum ContractStatus
{
    Pending,
    Signed,
    Cancelled,
}

internal sealed class Supplier : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public required string Name { get; set; }

    public required string Category { get; set; }

    public string? ContactName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Website { get; set; }

    public string? SocialMedia { get; set; }

    public string? Address { get; set; }

    public NegotiationStatus NegotiationStatus { get; set; } = NegotiationStatus.Researching;

    public string? Notes { get; set; }

    public string? CoverImage { get; set; }

    public List<SupplierService> Services { get; set; } = [];

    public List<SupplierContract> Contracts { get; set; } = [];

    public bool IsNegotiating =>
        NegotiationStatus is NegotiationStatus.Contacted or NegotiationStatus.Quoted or NegotiationStatus.Negotiating;
}

internal sealed class SupplierService : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public Guid SupplierId { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public decimal QuotedAmount { get; set; }

    public decimal ContractedAmount { get; set; }

    public ServiceStatus Status { get; set; } = ServiceStatus.Quoted;
}

internal sealed class SupplierContract : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public Guid SupplierId { get; set; }

    public required string ContractNumber { get; set; }

    public DateOnly? SignedAt { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Pending;

    public string? FileKey { get; set; }

    public string? FileName { get; set; }

    public string? FileContentType { get; set; }

    public long? FileSize { get; set; }
}
