using Lacoos.Modules.Identity.Domain;
using Lacoos.Modules.Identity.Persistence;
using Lacoos.SharedKernel.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lacoos.Modules.Identity.Application;

internal sealed record RegisterRequest(string? Name, string? Email, string? Password, string? PasswordConfirmation);

internal sealed record LoginRequest(string? Email, string? Password);

internal sealed record ForgotPasswordRequest(string? Email);

internal sealed record ResetPasswordRequest(string? Token, string? Password, string? PasswordConfirmation);

internal sealed record UpdateProfileRequest(string? Name, string? Email);

internal sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword, string? NewPasswordConfirmation);

internal sealed record UserResponse(Guid Id, string Name, string Email);

internal sealed record IssuedSession(string Token, string CsrfToken, DateTimeOffset ExpiresAt);

internal sealed class AccountService(
    IdentityDbContext db,
    IPasswordHasher<User> hasher,
    IClock clock,
    IEmailSender email,
    AppLinks links,
    IOptions<AuthOptions> options,
    ILogger<AccountService> logger)
{
    private const int MinPasswordLength = 8;

    // Verificado quando o e-mail não existe, para o tempo de resposta não revelar quem tem conta.
    private static readonly string DummyHash = new PasswordHasher<User>()
        .HashPassword(new User { Name = "x", Email = "x", NormalizedEmail = "x" }, Guid.NewGuid().ToString());

    public async Task<(UserResponse User, IssuedSession Session)> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var validator = new Validator()
            .Required(request.Name, "name", "Nome")
            .MaxLength(request.Name, 200, "name", "Nome")
            .Required(request.Email, "email", "E-mail")
            .Email(request.Email, "email");
        ValidateNewPassword(validator, request.Password, request.PasswordConfirmation, "password", "passwordConfirmation");
        validator.ThrowIfInvalid();

        var normalized = User.Normalize(request.Email!);
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct))
        {
            throw new ConflictException("Este e-mail já está cadastrado.");
        }

        var user = new User
        {
            Name = request.Name!.Trim(),
            Email = request.Email!.Trim(),
            NormalizedEmail = normalized,
            CreatedAt = clock.UtcNow,
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password!);
        db.Users.Add(user);

        var session = NewSession(user.Id);
        await db.SaveChangesAsync(ct);
        return (ToResponse(user), session);
    }

    public async Task<(UserResponse User, IssuedSession Session)> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        new Validator()
            .Required(request.Email, "email", "E-mail")
            .Required(request.Password, "password", "Senha")
            .ThrowIfInvalid();

        var normalized = User.Normalize(request.Email!);
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);

        var result = hasher.VerifyHashedPassword(user!, user?.PasswordHash ?? DummyHash, request.Password!);
        if (user is null || result == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedException("E-mail ou senha incorretos.");
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password!);
        }

        var session = NewSession(user.Id);
        await db.SaveChangesAsync(ct);
        return (ToResponse(user), session);
    }

    public Task LogoutAsync(Guid sessionId, CancellationToken ct) =>
        db.Sessions.Where(s => s.Id == sessionId).ExecuteDeleteAsync(ct);

    /// <summary>Responde igual para e-mails com e sem conta, para não revelar quem está cadastrado.</summary>
    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        new Validator().Required(request.Email, "email", "E-mail").Email(request.Email, "email").ThrowIfInvalid();

        var normalized = User.Normalize(request.Email!);
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
        if (user is null)
        {
            return;
        }

        var token = Tokens.Generate();
        await db.PasswordResetTokens.Where(t => t.UserId == user.Id && t.UsedAt == null).ExecuteDeleteAsync(ct);
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = Tokens.Hash(token),
            ExpiresAt = clock.UtcNow.AddMinutes(options.Value.PasswordResetMinutes),
        });
        await db.SaveChangesAsync(ct);

        var link = links.Build($"redefinir-senha?token={token}");
        try
        {
            await email.SendAsync(
                new EmailMessage(
                    user.Email,
                    "Redefinição de senha do Laçoos",
                    $"Olá, {user.Name}. Para definir uma nova senha, acesse: {link}\nO link expira em {options.Value.PasswordResetMinutes} minutos.",
                    $"<p>Olá, {System.Net.WebUtility.HtmlEncode(user.Name)}.</p><p><a href=\"{link}\">Definir nova senha</a></p><p>O link expira em {options.Value.PasswordResetMinutes} minutos.</p>"),
                ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Falha ao enviar e-mail de recuperação de senha do usuário {UserId}", user.Id);
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var validator = new Validator().Required(request.Token, "token", "Token");
        ValidateNewPassword(validator, request.Password, request.PasswordConfirmation, "password", "passwordConfirmation");
        validator.ThrowIfInvalid();

        var hash = Tokens.Hash(request.Token!);
        var reset = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (reset is null || reset.UsedAt is not null || reset.ExpiresAt <= clock.UtcNow)
        {
            throw new BusinessRuleException("Token inválido ou expirado.");
        }

        var user = await db.Users.FirstAsync(u => u.Id == reset.UserId, ct);
        user.PasswordHash = hasher.HashPassword(user, request.Password!);
        reset.UsedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await db.Sessions.Where(s => s.UserId == user.Id).ExecuteDeleteAsync(ct);
    }

    public async Task<UserResponse> GetProfileAsync(Guid userId, CancellationToken ct) =>
        ToResponse(await GetUserAsync(userId, ct));

    public async Task<UserResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
    {
        new Validator()
            .MaxLength(request.Name, 200, "name", "Nome")
            .Email(request.Email, "email")
            .ThrowIfInvalid();

        var user = await GetUserAsync(userId, ct);
        if (Text.Clean(request.Name) is { } name)
        {
            user.Name = name;
        }

        if (Text.Clean(request.Email) is { } newEmail)
        {
            var normalized = User.Normalize(newEmail);
            if (normalized != user.NormalizedEmail
                && await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct))
            {
                throw new ConflictException("Este e-mail já está cadastrado.");
            }

            user.Email = newEmail;
            user.NormalizedEmail = normalized;
        }

        await db.SaveChangesAsync(ct);
        return ToResponse(user);
    }

    public async Task ChangePasswordAsync(Guid userId, Guid currentSessionId, ChangePasswordRequest request, CancellationToken ct)
    {
        var validator = new Validator().Required(request.CurrentPassword, "currentPassword", "Senha atual");
        ValidateNewPassword(validator, request.NewPassword, request.NewPasswordConfirmation, "newPassword", "newPasswordConfirmation");
        validator.ThrowIfInvalid();

        var user = await GetUserAsync(userId, ct);
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword!) == PasswordVerificationResult.Failed)
        {
            throw new BusinessRuleException("Senha atual incorreta.");
        }

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword!);
        await db.SaveChangesAsync(ct);
        await db.Sessions.Where(s => s.UserId == userId && s.Id != currentSessionId).ExecuteDeleteAsync(ct);
    }

    private async Task<User> GetUserAsync(Guid userId, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
        ?? throw new NotFoundException("Usuário não encontrado.");

    private IssuedSession NewSession(Guid userId)
    {
        var token = Tokens.Generate();
        var csrf = Tokens.Generate();
        var now = clock.UtcNow;
        var expiresAt = now.AddDays(options.Value.SessionDays);
        db.Sessions.Add(new Session
        {
            UserId = userId,
            TokenHash = Tokens.Hash(token),
            CsrfToken = csrf,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = expiresAt,
        });
        return new IssuedSession(token, csrf, expiresAt);
    }

    private static void ValidateNewPassword(Validator validator, string? password, string? confirmation, string field, string confirmationField)
    {
        validator.Required(password, field, "Senha");
        if (password is not null && password.Length < MinPasswordLength)
        {
            validator.Add(field, $"A senha deve ter pelo menos {MinPasswordLength} caracteres.");
        }

        if (password != confirmation)
        {
            validator.Add(confirmationField, "A confirmação de senha não confere.");
        }
    }

    private static UserResponse ToResponse(User user) => new(user.Id, user.Name, user.Email);
}
