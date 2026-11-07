using System.Net;
using System.Text.RegularExpressions;
using Lacoos.IntegrationTests.Support;
using Lacoos.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lacoos.IntegrationTests;

/// <summary>Percorre todas as rotas registradas, para uma rota nova não ficar sem proteção.</summary>
public sealed class AccessControlTests(ApiFixture api) : ApiTest(api)
{
    private sealed record Route(string Method, string Pattern, bool IsUpload);

    private List<Route> WeddingRoutes()
    {
        _ = Api.Services;
        var routes = Api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/api/weddings/{weddingId", StringComparison.Ordinal))
            .SelectMany(e => e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Select(method =>
                new Route(method, e.RoutePattern.RawText!, e.RoutePattern.RawText!.EndsWith("/file", StringComparison.Ordinal) || e.RoutePattern.RawText!.EndsWith("/receipts", StringComparison.Ordinal) && e.RoutePattern.RawText!.Contains("installments"))))
            .ToList();

        Assert.True(routes.Count > 60, $"Esperava muitas rotas, encontrei {routes.Count}.");
        return routes;
    }

    private static string Fill(string pattern, Guid weddingId) =>
        Regex.Replace(
            pattern.Replace("{weddingId:guid}", weddingId.ToString()),
            @"\{(?<name>[a-zA-Z]+)(:(?<type>[a-z]+))?\}",
            match => match.Groups["type"].Value == "int" ? "1" : Guid.NewGuid().ToString());

    private static Task<ApiResponse> Call(TestUser user, Route route, Guid weddingId)
    {
        var url = Fill(route.Pattern, weddingId);
        var method = new HttpMethod(route.Method);
        if (route.IsUpload && route.Method != "GET")
        {
            return user.Upload(method, url, Scenario.Pdf, "a.pdf");
        }

        return user.Send(method, url, route.Method is "GET" or "DELETE" ? null : new { });
    }

    [Fact]
    public async Task Every_wedding_route_requires_authentication()
    {
        var (_, _, weddingId) = await NewOwner();
        var anonymous = Api.Anonymous();

        foreach (var route in WeddingRoutes())
        {
            var response = await Call(anonymous, route, weddingId);

            Assert.True(response.Status == HttpStatusCode.Unauthorized, $"{route.Method} {route.Pattern} retornou {response.Code}");
        }
    }

    [Fact]
    public async Task Every_wedding_route_hides_the_wedding_from_non_members()
    {
        var (_, _, weddingId) = await NewOwner();
        var stranger = await Api.NewUser();

        foreach (var route in WeddingRoutes())
        {
            var response = await Call(stranger, route, weddingId);

            Assert.True(response.Status == HttpStatusCode.NotFound, $"{route.Method} {route.Pattern} retornou {response.Code}");
            Assert.Equal("Casamento não encontrado.", response.Json.Str("detail"));
        }
    }

    [Fact]
    public async Task Every_write_route_is_forbidden_for_viewers()
    {
        var (owner, _, weddingId) = await NewOwner();
        var (viewer, _) = await Api.AddMember(owner, weddingId, WeddingRole.Viewer);

        foreach (var route in WeddingRoutes().Where(r => r.Method != "GET"))
        {
            var response = await Call(viewer, route, weddingId);

            Assert.True(response.Status == HttpStatusCode.Forbidden, $"{route.Method} {route.Pattern} retornou {response.Code}");
        }
    }

    [Fact]
    public async Task Every_read_route_is_allowed_for_viewers()
    {
        var (owner, _, weddingId) = await NewOwner();
        var (viewer, _) = await Api.AddMember(owner, weddingId, WeddingRole.Viewer);

        foreach (var route in WeddingRoutes().Where(r => r.Method == "GET"))
        {
            var response = await Call(viewer, route, weddingId);

            // IDs aleatórios geram 404 do registro, nunca do casamento nem 403.
            Assert.True(response.Status is HttpStatusCode.OK or HttpStatusCode.NotFound, $"{route.Method} {route.Pattern} retornou {response.Code}");
            if (response.Status == HttpStatusCode.NotFound)
            {
                Assert.NotEqual("Casamento não encontrado.", response.Json.Str("detail"));
            }
        }
    }

    [Fact]
    public async Task Member_management_routes_are_forbidden_for_organizers()
    {
        var (owner, w, weddingId) = await NewOwner();
        var (organizer, _) = await Api.AddMember(owner, weddingId, WeddingRole.Organizer);
        var id = Guid.NewGuid();

        var responses = new[]
        {
            await organizer.Post($"{w}/members", new { name = "X", email = "x@example.com", role = "viewer" }),
            await organizer.Post($"{w}/members/{id}/resend-invite"),
            await organizer.Patch($"{w}/members/{id}", new { role = "owner" }),
            await organizer.Delete($"{w}/members/{id}"),
        };

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Forbidden, response.Status));
    }
}
