using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Lacoos.SharedKernel.Email;
using Lacoos.SharedKernel.Time;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Lacoos.IntegrationTests.Support;

public sealed class TestClock : IClock
{
    public DateTimeOffset? FixedAt { get; set; }

    public DateTimeOffset UtcNow => FixedAt ?? DateTimeOffset.UtcNow;

    public DateOnly Today => SaoPauloClock.ToSaoPauloDate(UtcNow);

    /// <summary>Fixa o relógio ao meio-dia de São Paulo na data informada.</summary>
    public void SetToday(DateOnly date) =>
        FixedAt = new DateTimeOffset(date.ToDateTime(new TimeOnly(15, 0)), TimeSpan.Zero);
}

public sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public bool ShouldFail { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (ShouldFail)
        {
            throw new InvalidOperationException("SMTP indisponível");
        }

        Sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public string LastTokenFor(string email)
    {
        var message = Sent.Last(m => m.To.Equals(email, StringComparison.OrdinalIgnoreCase));
        return Regex.Match(message.TextBody, "token=([A-Za-z0-9_-]+)").Groups[1].Value;
    }
}

public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string PostgresImage = "postgres:17.6-alpine";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder(PostgresImage).Build();
    private readonly string _uploads = Path.Combine(Path.GetTempPath(), $"lacoos-tests-{Guid.NewGuid():N}");

    public TestClock Clock { get; } = new();

    public CapturingEmailSender Emails { get; } = new();

    public string ConnectionString => _database.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _database.GetConnectionString());
        Environment.SetEnvironmentVariable("Auth__RateLimitPerMinute", "1000000");
        Environment.SetEnvironmentVariable("Auth__SessionDays", "36500");
        Environment.SetEnvironmentVariable("FileStorage__RootPath", _uploads);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
        if (Directory.Exists(_uploads))
        {
            Directory.Delete(_uploads, recursive: true);
        }
    }

    public int CountUploadedFiles() =>
        Directory.Exists(_uploads) ? Directory.GetFiles(_uploads, "*", SearchOption.AllDirectories).Length : 0;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
