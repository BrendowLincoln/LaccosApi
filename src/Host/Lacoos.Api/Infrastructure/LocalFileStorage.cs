using Lacoos.SharedKernel.Files;
using Microsoft.Extensions.Options;

namespace Lacoos.Api.Infrastructure;

internal sealed class FileStorageOptions
{
    public const string Section = "FileStorage";

    public string RootPath { get; set; } = "data/uploads";
}

/// <summary>Grava em disco (volume no Docker). Para trocar por S3, basta outra implementação de IFileStorage.</summary>
internal sealed class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        Stream? stream = File.Exists(path) ? File.OpenRead(path) : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        File.Delete(Resolve(key));
        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Chave de arquivo fora do diretório de uploads.");
        }

        return path;
    }
}
