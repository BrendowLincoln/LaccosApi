namespace Lacoos.Modules.Identity.Application;

internal sealed class AuthOptions
{
    public const string Section = "Auth";

    public int SessionDays { get; set; } = 14;

    /// <summary>Exige HTTPS nos cookies. Só deve ser false em desenvolvimento.</summary>
    public bool SecureCookies { get; set; } = true;

    public int PasswordResetMinutes { get; set; } = 60;
}
