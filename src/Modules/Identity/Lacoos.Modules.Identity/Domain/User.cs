namespace Lacoos.Modules.Identity.Domain;

internal sealed class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public required string Email { get; set; }

    /// <summary>E-mail em minúsculas, usado para unicidade e busca.</summary>
    public required string NormalizedEmail { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

internal sealed class Session
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public required string TokenHash { get; set; }

    public required string CsrfToken { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}

internal sealed class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}
