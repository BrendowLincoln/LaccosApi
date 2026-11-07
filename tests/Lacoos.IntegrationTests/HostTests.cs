using System.Net;
using Lacoos.IntegrationTests.Support;
using Lacoos.SharedKernel.Modules;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Lacoos.IntegrationTests;

public sealed class HostTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Health_check_reports_a_working_database()
    {
        var response = await Api.Anonymous().Get("/health");

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Fact]
    public async Task Swagger_ui_uses_the_open_api_document_and_sends_the_csrf_token()
    {
        var anonymous = Api.Anonymous();

        var page = await anonymous.Get("/swagger/index.html");
        var script = await anonymous.Get("/swagger/index.js");

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(HttpStatusCode.OK, script.Status);
        Assert.Contains("/openapi/v1.json", script.Text);
        Assert.Contains("X-XSRF-TOKEN", script.Text);
    }

    [Fact]
    public async Task Swagger_ui_is_not_exposed_outside_development()
    {
        using var production = Api.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = production.CreateClient();

        var page = await client.GetAsync("/swagger/index.html");
        var document = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, document.StatusCode);
    }

    [Fact]
    public async Task Open_api_document_lists_the_modules()
    {
        var document = await Api.Anonymous().Get("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, document.Status);
        Assert.Contains("/dashboard", document.Text);
        Assert.Contains("/api/auth/login", document.Text);
        Assert.Contains("/gifts", document.Text);
    }

    [Fact]
    public async Task All_module_schemas_were_migrated()
    {
        await using var connection = new Npgsql.NpgsqlConnection(Api.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            "SELECT count(DISTINCT table_schema) FROM information_schema.tables WHERE table_name = '__ef_migrations_history'", connection);

        Assert.Equal(8L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Demo_seed_is_idempotent_and_produces_a_usable_wedding()
    {
        Environment.SetEnvironmentVariable("Seed__Enabled", "true");
        Environment.SetEnvironmentVariable("Seed__DemoPassword", "senha-demo-123");
        try
        {
            using var first = Api.WithWebHostBuilder(_ => { });
            using var firstClient = first.CreateClient();
            using var second = Api.WithWebHostBuilder(_ => { });
            var user = new TestUser(second.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true }), "milena@lacoos.local", "senha-demo-123");

            var login = await user.Post("/api/auth/login", new { email = user.Email, password = user.Password });
            var dashboard = await user.Get($"{W(DemoIds.Wedding)}/dashboard");

            Assert.Equal(HttpStatusCode.OK, login.Status);
            Assert.Equal(HttpStatusCode.OK, dashboard.Status);
            Assert.Equal(6, dashboard.Json.Int("checklist.total"));
            Assert.Equal(5, dashboard.Json.Int("guests.total"));
            Assert.Equal(70000, dashboard.Json.Dec("financial.totalBudget"));
            Assert.Equal(23300, dashboard.Json.Dec("financial.committed"));
            Assert.True(dashboard.Json.At("overdueInstallments").Count() > 0);
            Assert.True(dashboard.Json.Dec("financial.paid") > 0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Seed__Enabled", null);
            Environment.SetEnvironmentVariable("Seed__DemoPassword", null);
        }
    }
}
