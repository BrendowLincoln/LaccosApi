using Lacoos.SharedKernel.Errors;
using Microsoft.AspNetCore.Http;

namespace Lacoos.SharedKernel.Files;

public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, CancellationToken ct);

    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);
}

public sealed record StoredFile(string Key, string FileName, string ContentType, long Size);

public static class FileUploads
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly (string Extension, string ContentType, byte[] Signature)[] Allowed =
    [
        (".pdf", "application/pdf", "%PDF"u8.ToArray()),
        (".jpg", "image/jpeg", [0xFF, 0xD8, 0xFF]),
        (".jpeg", "image/jpeg", [0xFF, 0xD8, 0xFF]),
        (".png", "image/png", [0x89, 0x50, 0x4E, 0x47]),
    ];

    /// <summary>
    /// Valida tipo e tamanho e grava com uma chave gerada pelo servidor. O conteúdo do arquivo
    /// é conferido, porque nome e content type são informados pelo cliente.
    /// </summary>
    public static async Task<StoredFile> StoreAsync(
        this IFileStorage storage, IFormFile? file, string folder, Guid weddingId, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new BusinessRuleException("Selecione um arquivo.");
        }

        if (file.Length > MaxBytes)
        {
            throw new BusinessRuleException("O arquivo deve ter no máximo 10 MB.");
        }

        var fileName = SafeFileName(file.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var rule = Allowed.FirstOrDefault(a => a.Extension == extension);
        if (rule.Extension is null)
        {
            throw new BusinessRuleException("Tipo de arquivo não permitido. Envie PDF, JPG ou PNG.");
        }

        await using var content = file.OpenReadStream();
        var header = new byte[rule.Signature.Length];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        if (read < header.Length || !header.AsSpan().SequenceEqual(rule.Signature))
        {
            throw new BusinessRuleException("O conteúdo do arquivo não corresponde ao tipo informado.");
        }

        content.Position = 0;
        var key = $"{weddingId:N}/{folder}/{Guid.CreateVersion7():N}{extension}";
        await storage.SaveAsync(key, content, ct);
        return new StoredFile(key, fileName, rule.ContentType, file.Length);
    }

    private static string SafeFileName(string? name)
    {
        var baseName = Path.GetFileName((name ?? string.Empty).Replace('\\', '/'));
        var clean = new string(baseName.Where(c => !char.IsControl(c) && c is not ('"' or '/' or '\\')).ToArray()).Trim();
        return string.IsNullOrEmpty(clean) ? "arquivo" : clean[..Math.Min(clean.Length, 200)];
    }
}
