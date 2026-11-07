namespace Lacoos.Modules.Gifts.Contracts;

public interface IGiftGuestLinks
{
    /// <summary>
    /// Preserva reservas e recebimentos de um convidado que será excluído:
    /// copia o nome para personName e limpa o guestId.
    /// </summary>
    Task DetachGuestAsync(Guid guestId, string guestName, CancellationToken ct);
}
