using Lacoos.SharedKernel.Errors;
using Microsoft.AspNetCore.Http;

namespace Lacoos.SharedKernel.Files;

public static class FileDownloads
{
    /// <summary>Sempre como anexo, para o navegador não executar o conteúdo enviado.</summary>
    public static async Task<IResult> DownloadAsync(
        this IFileStorage storage, string key, string fileName, string contentType, CancellationToken ct)
    {
        var stream = await storage.OpenReadAsync(key, ct)
            ?? throw new NotFoundException("Arquivo não encontrado.");
        return Results.File(stream, contentType, fileName);
    }
}
