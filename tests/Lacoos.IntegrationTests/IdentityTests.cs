using System.Net;
using Lacoos.IntegrationTests.Support;
using Xunit;

namespace Lacoos.IntegrationTests;

public sealed class IdentityTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Register_logs_in_and_never_returns_the_password()
    {
        var user = await Api.NewUser("Milena");

        var me = await user.Get("/api/me");

        Assert.Equal(HttpStatusCode.OK, me.Status);
        Assert.Equal("Milena", me.Json.Str("name"));
        Assert.DoesNotContain("password", me.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Session_cookie_is_http_only_and_csrf_cookie_is_readable()
    {
        var client = Api.Anonymous();
        var response = await client.Post("/api/auth/register", new
        {
            name = "Pessoa", email = $"{Guid.NewGuid():N}@example.com", password = "senha-segura-123", passwordConfirmation = "senha-segura-123",
        });

        var cookies = response.Raw.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(cookies, c => c.StartsWith("lacoos_session=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cookies, c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal) && !c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_rejects_mismatched_confirmation_in_portuguese()
    {
        var response = await Api.Anonymous().Post("/api/auth/register", new
        {
            name = "Pessoa", email = "a@example.com", password = "senha-segura-123", passwordConfirmation = "outra-senha-123",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("A confirmação de senha não confere.", response.Json.Str("errors.passwordConfirmation.0"));
    }

    [Fact]
    public async Task Register_rejects_duplicate_email_ignoring_case()
    {
        var user = await Api.NewUser();

        var response = await Api.Anonymous().Post("/api/auth/register", new
        {
            name = "Outra", email = user.Email.ToUpperInvariant(), password = "senha-segura-123", passwordConfirmation = "senha-segura-123",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
    }

    [Fact]
    public async Task Login_rejects_wrong_password_and_unknown_email_with_the_same_message()
    {
        var user = await Api.NewUser();

        var wrong = await Api.Anonymous().Post("/api/auth/login", new { email = user.Email, password = "senha-errada-1" });
        var unknown = await Api.Anonymous().Post("/api/auth/login", new { email = "ninguem@example.com", password = "senha-errada-1" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.Status);
        Assert.Equal(wrong.Json.Str("detail"), unknown.Json.Str("detail"));
    }

    [Fact]
    public async Task Logout_ends_the_session_on_the_server()
    {
        var user = await Api.NewUser();

        var logout = await user.Post("/api/auth/logout");
        var me = await user.Get("/api/me");

        Assert.Equal(HttpStatusCode.NoContent, logout.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, me.Status);
    }

    [Fact]
    public async Task Expired_session_is_rejected()
    {
        var user = await Api.NewUser();

        Api.Clock.FixedAt = DateTimeOffset.UtcNow.AddDays(36501);
        var me = await user.Get("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, me.Status);
    }

    [Fact]
    public async Task Writes_without_csrf_token_are_rejected()
    {
        var user = await Api.NewUser();
        user.ForgetCsrf();

        var response = await user.Post("/api/weddings", new { name = "X", weddingDate = "2030-01-01", estimatedGuestCount = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
    }

    [Fact]
    public async Task Forgot_password_answers_the_same_for_known_and_unknown_emails()
    {
        var user = await Api.NewUser();

        var known = await Api.Anonymous().Post("/api/auth/forgot-password", new { email = user.Email });
        var unknown = await Api.Anonymous().Post("/api/auth/forgot-password", new { email = "ninguem@example.com" });

        Assert.Equal(known.Status, unknown.Status);
        Assert.Equal(known.Text, unknown.Text);
    }

    [Fact]
    public async Task Forgot_password_does_not_reveal_email_delivery_failures()
    {
        var user = await Api.NewUser();
        Api.Emails.ShouldFail = true;

        var response = await Api.Anonymous().Post("/api/auth/forgot-password", new { email = user.Email });

        Assert.Equal(HttpStatusCode.Accepted, response.Status);
    }

    [Fact]
    public async Task Reset_password_changes_it_ends_sessions_and_token_works_once()
    {
        var user = await Api.NewUser();
        await Api.Anonymous().Post("/api/auth/forgot-password", new { email = user.Email });
        var token = Api.Emails.LastTokenFor(user.Email);
        const string NewPassword = "nova-senha-456";

        var reset = await Api.Anonymous().Post("/api/auth/reset-password", new { token, password = NewPassword, passwordConfirmation = NewPassword });
        var reused = await Api.Anonymous().Post("/api/auth/reset-password", new { token, password = NewPassword, passwordConfirmation = NewPassword });
        var oldSession = await user.Get("/api/me");
        var oldLogin = await Api.Anonymous().Post("/api/auth/login", new { email = user.Email, password = user.Password });
        var newLogin = await Api.Anonymous().Post("/api/auth/login", new { email = user.Email, password = NewPassword });

        Assert.Equal(HttpStatusCode.NoContent, reset.Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.Status);
        Assert.Equal(HttpStatusCode.OK, newLogin.Status);
    }

    [Fact]
    public async Task Reset_token_expires()
    {
        var user = await Api.NewUser();
        await Api.Anonymous().Post("/api/auth/forgot-password", new { email = user.Email });
        var token = Api.Emails.LastTokenFor(user.Email);

        Api.Clock.FixedAt = DateTimeOffset.UtcNow.AddHours(2);
        var reset = await Api.Anonymous().Post("/api/auth/reset-password", new { token, password = "nova-senha-456", passwordConfirmation = "nova-senha-456" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reset.Status);
    }

    [Fact]
    public async Task Profile_can_be_edited_but_email_must_stay_unique()
    {
        var user = await Api.NewUser();
        var other = await Api.NewUser();

        var ok = await user.Patch("/api/me", new { name = "Novo Nome" });
        var duplicate = await user.Patch("/api/me", new { email = other.Email });

        Assert.Equal("Novo Nome", ok.Json.Str("name"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.Status);
    }

    [Fact]
    public async Task Change_password_requires_the_current_one_and_ends_other_sessions()
    {
        var user = await Api.NewUser();
        var otherDevice = Api.Anonymous();
        await otherDevice.Post("/api/auth/login", new { email = user.Email, password = user.Password });
        const string NewPassword = "nova-senha-456";

        var wrong = await user.Post("/api/me/change-password", new { currentPassword = "errada-1234", newPassword = NewPassword, newPasswordConfirmation = NewPassword });
        var ok = await user.Post("/api/me/change-password", new { currentPassword = user.Password, newPassword = NewPassword, newPasswordConfirmation = NewPassword });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.Status);
        Assert.Equal(HttpStatusCode.NoContent, ok.Status);
        Assert.Equal(HttpStatusCode.OK, (await user.Get("/api/me")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.Get("/api/me")).Status);
    }
}
