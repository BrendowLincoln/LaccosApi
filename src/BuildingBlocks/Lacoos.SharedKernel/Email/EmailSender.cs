namespace Lacoos.SharedKernel.Email;

public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

public sealed class AppLinks
{
    public const string Section = "App";

    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";

    public string Build(string path) => $"{FrontendBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
}
