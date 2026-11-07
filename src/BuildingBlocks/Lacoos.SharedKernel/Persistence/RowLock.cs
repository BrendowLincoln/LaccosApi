using Microsoft.EntityFrameworkCore;

namespace Lacoos.SharedKernel.Persistence;

public static class RowLock
{
    /// <summary>
    /// Bloqueia uma linha até o fim da transação atual, para que validações de limite
    /// (baixas, reservas, último owner) não sejam furadas por requisições simultâneas.
    /// </summary>
    public static Task LockRowAsync(this DbContext context, string qualifiedTable, Guid id, CancellationToken ct)
    {
#pragma warning disable EF1002 // O nome da tabela vem de constantes do código; o ID segue como parâmetro.
        return context.Database.ExecuteSqlRawAsync($"SELECT 1 FROM {qualifiedTable} WHERE id = {{0}} FOR UPDATE", [id], ct);
#pragma warning restore EF1002
    }
}
