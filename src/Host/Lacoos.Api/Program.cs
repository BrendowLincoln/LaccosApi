using System.Text.Json;
using System.Text.Json.Serialization;
using Lacoos.Api.Infrastructure;
using Lacoos.Modules.Budget;
using Lacoos.Modules.Checklist;
using Lacoos.Modules.Dashboard;
using Lacoos.Modules.Gifts;
using Lacoos.Modules.Guests;
using Lacoos.Modules.Identity;
using Lacoos.Modules.Payments;
using Lacoos.Modules.Suppliers;
using Lacoos.Modules.Weddings;
using Lacoos.SharedKernel.Email;
using Lacoos.SharedKernel.Files;
using Lacoos.SharedKernel.Modules;
using Lacoos.SharedKernel.Tenancy;
using Lacoos.SharedKernel.Time;
using Lacoos.SharedKernel.Web;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

const string CorsPolicy = "frontend";
const long MaxRequestBytes = FileUploads.MaxBytes + (1024 * 1024);

IModule[] modules =
[
    new IdentityModule(),
    new WeddingsModule(),
    new DashboardModule(),
    new ChecklistModule(),
    new BudgetModule(),
    new GuestsModule(),
    new SuppliersModule(),
    new PaymentsModule(),
    new GiftsModule(),
];

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = MaxRequestBytes);
builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = MaxRequestBytes);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<WeddingContext>();
builder.Services.AddScoped<IWeddingContext>(services => services.GetRequiredService<WeddingContext>());
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<IClock, SaoPauloClock>();
builder.Services.AddSingleton(services =>
    services.GetRequiredService<IConfiguration>().GetSection(AppLinks.Section).Get<AppLinks>() ?? new AppLinks());
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.Section));
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection(FileStorageOptions.Section));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
    options.SerializerOptions.Converters.Add(new TimeOnlyHourMinuteConverter());
});

builder.Services.AddCors(cors => cors.AddPolicy(CorsPolicy, policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres");

foreach (var module in modules)
{
    module.Register(builder.Services, builder.Configuration);
}

var app = builder.Build();

if (args.Contains("migrate"))
{
    await StartupTasks.MigrateAsync(app.Services, CancellationToken.None);
    return;
}

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await StartupTasks.MigrateAsync(app.Services, CancellationToken.None);
}

if (app.Configuration.GetValue("Seed:Enabled", false))
{
    await StartupTasks.SeedAsync(app.Services, app.Configuration, CancellationToken.None);
}

app.Use((http, next) =>
{
    // Impede o navegador de reinterpretar arquivos enviados como outro tipo.
    http.Response.Headers.XContentTypeOptions = "nosniff";
    return next(http);
});
app.UseExceptionHandler();
app.UseCors(CorsPolicy);
app.UseAuthentication();
foreach (var module in modules)
{
    module.ConfigurePipeline(app);
}

app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseLacoosSwaggerUi();
}

app.MapHealthChecks("/health");

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

await app.RunAsync();

public partial class Program;
