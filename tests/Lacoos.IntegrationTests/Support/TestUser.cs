using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lacoos.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Lacoos.IntegrationTests.Support;

public sealed record ApiResponse(HttpStatusCode Status, JsonElement Json, string Text, HttpResponseMessage Raw)
{
    public int Code => (int)Status;

    /// <summary>Lê um caminho como "totals.paid" ou "installments.0.amount".</summary>
    public JsonElement this[string path] => Json.At(path);
}

public static class JsonPaths
{
    public static JsonElement At(this JsonElement element, string path)
    {
        foreach (var part in path.Split('.'))
        {
            element = int.TryParse(part, out var index) ? element[index] : element.GetProperty(part);
        }

        return element;
    }

    public static decimal Dec(this JsonElement element, string path) => element.At(path).GetDecimal();

    public static string Str(this JsonElement element, string path) => element.At(path).GetString()!;

    public static int Int(this JsonElement element, string path) => element.At(path).GetInt32();

    public static Guid Id(this JsonElement element, string path = "id") => element.At(path).GetGuid();

    public static int Count(this JsonElement element) => element.GetArrayLength();
}

/// <summary>Cliente HTTP com cookies e CSRF, como o navegador de uma pessoa logada.</summary>
public sealed class TestUser(HttpClient client, string email, string password)
{
    public HttpClient Client { get; } = client;

    public string Email { get; set; } = email;

    public string Password { get; set; } = password;

    public Guid Id { get; set; }

    public Task<ApiResponse> Get(string url) => Send(HttpMethod.Get, url, null);

    public Task<ApiResponse> Post(string url, object? body = null) => Send(HttpMethod.Post, url, body ?? new { });

    public Task<ApiResponse> Put(string url, object? body = null) => Send(HttpMethod.Put, url, body ?? new { });

    public Task<ApiResponse> Patch(string url, object? body = null) => Send(HttpMethod.Patch, url, body ?? new { });

    public Task<ApiResponse> Delete(string url) => Send(HttpMethod.Delete, url, null);

    public async Task<ApiResponse> Upload(HttpMethod method, string url, byte[] content, string fileName, string contentType = "application/octet-stream", IReadOnlyDictionary<string, string>? fields = null)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        foreach (var (name, value) in fields ?? new Dictionary<string, string>())
        {
            form.Add(new StringContent(value), name);
        }

        using var request = new HttpRequestMessage(method, url) { Content = form };
        return await Read(await Client.SendAsync(request));
    }

    public async Task<ApiResponse> Send(HttpMethod method, string url, object? body)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonSerializerOptions.Web);
        }

        var response = await Read(await Client.SendAsync(request));
        CaptureCsrf(response.Raw);
        return response;
    }

    public void ForgetCsrf() => Client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

    private void CaptureCsrf(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return;
        }

        foreach (var cookie in cookies.Where(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal)))
        {
            var value = cookie["XSRF-TOKEN=".Length..].Split(';')[0];
            ForgetCsrf();
            if (value.Length > 0)
            {
                Client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", value);
            }
        }
    }

    private static async Task<ApiResponse> Read(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        var isJson = response.Content.Headers.ContentType?.MediaType?.Contains("json") == true && text.Length > 0;
        var json = isJson ? JsonDocument.Parse(text).RootElement.Clone() : default;
        return new ApiResponse(response.StatusCode, json, text, response);
    }
}

public static class Scenario
{
    public const string DefaultPassword = "senha-segura-123";

    public static TestUser Anonymous(this ApiFixture fixture) =>
        new(fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true }), string.Empty, string.Empty);

    public static async Task<TestUser> NewUser(this ApiFixture fixture, string? name = null)
    {
        var user = fixture.Anonymous();
        user.Email = $"{Guid.NewGuid():N}@example.com";
        user.Password = DefaultPassword;
        var response = await user.Post("/api/auth/register", new
        {
            name = name ?? "Pessoa de Teste",
            email = user.Email,
            password = user.Password,
            passwordConfirmation = user.Password,
        });
        Xunit.Assert.Equal(HttpStatusCode.Created, response.Status);
        user.Id = response.Json.Id();
        return user;
    }

    public static async Task<Guid> NewWedding(this TestUser user, DateOnly? date = null)
    {
        var response = await user.Post("/api/weddings", new
        {
            name = "Casamento de Teste",
            weddingDate = date ?? new DateOnly(2030, 1, 1),
            estimatedGuestCount = 100,
        });
        Xunit.Assert.Equal(HttpStatusCode.Created, response.Status);
        return response.Json.Id();
    }

    /// <summary>Convida e aceita, deixando um membro ativo com o papel informado.</summary>
    public static async Task<(TestUser User, Guid MemberId)> AddMember(this ApiFixture fixture, TestUser owner, Guid weddingId, WeddingRole role)
    {
        var user = await fixture.NewUser();
        var invite = await owner.Post($"/api/weddings/{weddingId}/members", new { name = "Membro", email = user.Email, role = role.ToString().ToLowerInvariant() });
        Xunit.Assert.Equal(HttpStatusCode.Created, invite.Status);

        var accept = await user.Post($"/api/invitations/{fixture.Emails.LastTokenFor(user.Email)}/accept");
        Xunit.Assert.Equal(HttpStatusCode.OK, accept.Status);
        return (user, invite.Json.Id("member.id"));
    }

    public static string W(Guid weddingId) => $"/api/weddings/{weddingId}";

    public static readonly byte[] Pdf = "%PDF-1.7 conteudo de teste"u8.ToArray();

    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
}
