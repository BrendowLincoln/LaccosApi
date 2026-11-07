namespace Lacoos.SharedKernel.Tenancy;

public enum WeddingRole
{
    Viewer = 0,
    Organizer = 1,
    Owner = 2,
}

/// <summary>Casamento da requisição atual. Sem valor, nenhuma tabela de negócio retorna dados.</summary>
public interface IWeddingContext
{
    Guid? WeddingId { get; }

    WeddingRole? Role { get; }
}

public sealed class WeddingContext : IWeddingContext
{
    public Guid? WeddingId { get; private set; }

    public WeddingRole? Role { get; private set; }

    public void Set(Guid weddingId, WeddingRole? role)
    {
        WeddingId = weddingId;
        Role = role;
    }
}

/// <summary>Implementado pelo módulo Weddings: papel de um usuário ativo no casamento.</summary>
public interface IWeddingMembershipResolver
{
    Task<WeddingRole?> GetActiveRoleAsync(Guid weddingId, Guid userId, CancellationToken ct);
}

public interface IWeddingScoped
{
    Guid WeddingId { get; set; }
}
