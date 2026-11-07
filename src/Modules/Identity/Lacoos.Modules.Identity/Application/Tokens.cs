using System.Security.Cryptography;
using System.Text;

namespace Lacoos.Modules.Identity.Application;

internal static class Tokens
{
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Só o hash vai para o banco; um vazamento não expõe tokens utilizáveis.</summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
