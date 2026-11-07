namespace Lacoos.Modules.Identity.Contracts;

public sealed record UserInfo(Guid Id, string Name, string Email);

/// <summary>Consulta pública de usuários, usada pelo módulo Weddings.</summary>
public interface IUserDirectory
{
    Task<UserInfo?> FindAsync(Guid userId, CancellationToken ct);

    Task<UserInfo?> FindByEmailAsync(string email, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, UserInfo>> GetManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}
