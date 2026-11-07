using System.Security.Cryptography;
using System.Text;
using Lacoos.Modules.Checklist.Contracts;
using Lacoos.Modules.Weddings.Domain;
using Lacoos.Modules.Weddings.Persistence;
using Lacoos.SharedKernel.Email;
using Lacoos.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;

namespace Lacoos.Modules.Weddings.Application;

internal sealed record InviteMemberRequest(string? Name, string? Email, WeddingRole? Role);

internal sealed record UpdateMemberRequest(WeddingRole? Role, string? Name, string? Email, string? Avatar);

internal sealed record MemberResponse(
    Guid Id, string Name, string Email, WeddingRole Role, MemberStatus Status, DateOnly JoinedAt, string? Avatar);

internal sealed record InviteResponse(MemberResponse Member, bool InviteEmailSent);

internal sealed record InvitationPreview(string WeddingName, string Email, WeddingRole Role, DateTimeOffset ExpiresAt);

internal sealed class MemberService(
    WeddingsDbContext db,
    IUserDirectory users,
    IChecklistAssignments checklist,
    ICurrentUser currentUser,
    IWeddingContext weddingContext,
    IEmailSender email,
    AppLinks links,
    IClock clock,
    ILogger<MemberService> logger)
{
    private const int InvitationDays = 7;

    private Guid WeddingId => weddingContext.WeddingId!.Value;

    public async Task<IReadOnlyList<MemberResponse>> ListAsync(CancellationToken ct)
    {
        var members = await db.Members.AsNoTracking()
            .Where(m => m.WeddingId == WeddingId)
            .OrderBy(m => m.JoinedAt).ThenBy(m => m.Name)
            .ToListAsync(ct);
        return await ToResponsesAsync(members, ct);
    }

    public async Task<InviteResponse> InviteAsync(InviteMemberRequest request, CancellationToken ct)
    {
        new Validator()
            .Required(request.Name, "name", "Nome")
            .MaxLength(request.Name, 200, "name", "Nome")
            .Required(request.Email, "email", "E-mail")
            .Email(request.Email, "email")
            .Required(request.Role, "role", "Papel")
            .ThrowIfInvalid();

        await EnsureEmailIsFreeAsync(request.Email!, exceptMemberId: null, ct);

        var member = new WeddingMember
        {
            WeddingId = WeddingId,
            Name = request.Name!.Trim(),
            Email = request.Email!.Trim(),
            NormalizedEmail = WeddingMember.Normalize(request.Email),
            Role = request.Role!.Value,
            Status = MemberStatus.Invited,
            JoinedAt = clock.Today,
        };
        db.Members.Add(member);
        var token = AddInvitation(member.Id);
        await db.SaveChangesAsync(ct);

        var sent = await SendInvitationAsync(member, token, ct);
        return new InviteResponse((await ToResponsesAsync([member], ct))[0], sent);
    }

    public async Task<InviteResponse> ResendInviteAsync(Guid memberId, CancellationToken ct)
    {
        var member = await LoadAsync(memberId, ct);
        if (member.Status != MemberStatus.Invited)
        {
            throw new BusinessRuleException("Só é possível reenviar convites pendentes.");
        }

        // O token anterior deixa de valer.
        await db.Invitations.Where(i => i.MemberId == member.Id).ExecuteDeleteAsync(ct);
        var token = AddInvitation(member.Id);
        await db.SaveChangesAsync(ct);

        var sent = await SendInvitationAsync(member, token, ct);
        return new InviteResponse((await ToResponsesAsync([member], ct))[0], sent);
    }

    public async Task<MemberResponse> UpdateAsync(Guid memberId, UpdateMemberRequest request, CancellationToken ct)
    {
        new Validator()
            .MaxLength(request.Name, 200, "name", "Nome")
            .Email(request.Email, "email")
            .ThrowIfInvalid();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockRowAsync(WeddingsDbContext.WeddingsTable, WeddingId, ct);

        var member = await LoadAsync(memberId, ct);
        var name = Text.Clean(request.Name);
        var newEmail = Text.Clean(request.Email);

        if (member.Status == MemberStatus.Active && (name is not null || newEmail is not null))
        {
            throw new BusinessRuleException("Nome e e-mail de um membro ativo vêm do perfil do usuário.");
        }

        if (request.Role is { } role && role != member.Role)
        {
            if (member.Role == WeddingRole.Owner)
            {
                await EnsureNotLastOwnerAsync(member, ct);
            }

            member.Role = role;
        }

        if (name is not null)
        {
            member.Name = name;
        }

        if (newEmail is not null && WeddingMember.Normalize(newEmail) != member.NormalizedEmail)
        {
            await EnsureEmailIsFreeAsync(newEmail, member.Id, ct);
            member.Email = newEmail;
            member.NormalizedEmail = WeddingMember.Normalize(newEmail);
        }

        if (request.Avatar is not null)
        {
            member.Avatar = Text.Clean(request.Avatar);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (await ToResponsesAsync([member], ct))[0];
    }

    public async Task RemoveAsync(Guid memberId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockRowAsync(WeddingsDbContext.WeddingsTable, WeddingId, ct);

        var member = await LoadAsync(memberId, ct);
        if (member.Role == WeddingRole.Owner)
        {
            await EnsureNotLastOwnerAsync(member, ct);
        }

        // Primeiro libera as tarefas: se a exclusão falhar depois, nada aponta para um membro inexistente.
        await checklist.UnassignMemberAsync(member.Id, ct);

        db.Members.Remove(member);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<InvitationPreview> PreviewInvitationAsync(string token, CancellationToken ct)
    {
        var (_, member) = await LoadValidInvitationAsync(token, ct);
        var wedding = await db.Weddings.AsNoTracking().FirstAsync(w => w.Id == member.WeddingId, ct);
        var invitation = await db.Invitations.AsNoTracking().FirstAsync(i => i.MemberId == member.Id, ct);
        return new InvitationPreview(wedding.Name, member.Email, member.Role, invitation.ExpiresAt);
    }

    public async Task<WeddingMemberAccepted> AcceptInvitationAsync(string token, CancellationToken ct)
    {
        var (invitation, member) = await LoadValidInvitationAsync(token, ct);

        var user = await users.FindAsync(currentUser.RequiredUserId, ct)
            ?? throw new UnauthorizedException("Autenticação necessária.");

        if (WeddingMember.Normalize(user.Email) != member.NormalizedEmail)
        {
            throw new ForbiddenException("Este convite foi enviado para outro e-mail.");
        }

        if (await db.Members.AnyAsync(m => m.WeddingId == member.WeddingId && m.UserId == user.Id, ct))
        {
            throw new ConflictException("Você já é membro deste casamento.");
        }

        member.UserId = user.Id;
        member.Name = user.Name;
        member.Status = MemberStatus.Active;
        member.JoinedAt = clock.Today;
        db.Invitations.Remove(invitation);
        await db.SaveChangesAsync(ct);
        return new WeddingMemberAccepted(member.WeddingId, member.Id);
    }

    private async Task<(MemberInvitation Invitation, WeddingMember Member)> LoadValidInvitationAsync(string token, CancellationToken ct)
    {
        var hash = HashToken(token);
        var invitation = await db.Invitations.FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (invitation is null || invitation.ExpiresAt <= clock.UtcNow)
        {
            throw new BusinessRuleException("Convite inválido ou expirado.");
        }

        var member = await db.Members.FirstAsync(m => m.Id == invitation.MemberId, ct);
        return (invitation, member);
    }

    private string AddInvitation(Guid memberId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = clock.UtcNow;
        db.Invitations.Add(new MemberInvitation
        {
            MemberId = memberId,
            TokenHash = HashToken(token),
            CreatedAt = now,
            ExpiresAt = now.AddDays(InvitationDays),
        });
        return token;
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Falha de e-mail não desfaz o convite: a API informa e ele pode ser reenviado.</summary>
    private async Task<bool> SendInvitationAsync(WeddingMember member, string token, CancellationToken ct)
    {
        var wedding = await db.Weddings.AsNoTracking().FirstAsync(w => w.Id == member.WeddingId, ct);
        var link = links.Build($"convite?token={token}");
        var safeWedding = System.Net.WebUtility.HtmlEncode(wedding.Name);
        var safeName = System.Net.WebUtility.HtmlEncode(member.Name);
        try
        {
            await email.SendAsync(
                new EmailMessage(
                    member.Email,
                    $"Convite para organizar {wedding.Name} no Laçoos",
                    $"Olá, {member.Name}. Você foi convidado para {wedding.Name}. Aceite em: {link}\nO convite expira em {InvitationDays} dias.",
                    $"<p>Olá, {safeName}.</p><p>Você foi convidado para <strong>{safeWedding}</strong>.</p><p><a href=\"{link}\">Aceitar convite</a></p><p>O convite expira em {InvitationDays} dias.</p>"),
                ct);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Falha ao enviar convite do membro {MemberId}", member.Id);
            return false;
        }
    }

    private async Task EnsureEmailIsFreeAsync(string email, Guid? exceptMemberId, CancellationToken ct)
    {
        var normalized = WeddingMember.Normalize(email);
        var duplicated = await db.Members.AnyAsync(
            m => m.WeddingId == WeddingId && m.NormalizedEmail == normalized && m.Id != exceptMemberId, ct);

        // Quem trocou de e-mail no perfil continua membro, mesmo com outro e-mail salvo aqui.
        if (!duplicated && await users.FindByEmailAsync(email, ct) is { } user)
        {
            duplicated = await db.Members.AnyAsync(m => m.WeddingId == WeddingId && m.UserId == user.Id, ct);
        }

        if (duplicated)
        {
            throw new ConflictException("Este e-mail já faz parte do casamento.");
        }
    }

    private async Task EnsureNotLastOwnerAsync(WeddingMember member, CancellationToken ct)
    {
        if (member.Status != MemberStatus.Active)
        {
            return;
        }

        var otherActiveOwners = await db.Members.CountAsync(
            m => m.WeddingId == WeddingId && m.Id != member.Id
                && m.Role == WeddingRole.Owner && m.Status == MemberStatus.Active, ct);
        if (otherActiveOwners == 0)
        {
            throw new BusinessRuleException("O casamento não pode ficar sem owner.");
        }
    }

    private async Task<WeddingMember> LoadAsync(Guid memberId, CancellationToken ct) =>
        await db.Members.FirstOrDefaultAsync(m => m.Id == memberId && m.WeddingId == WeddingId, ct)
        ?? throw new NotFoundException("Membro não encontrado.");

    private async Task<IReadOnlyList<MemberResponse>> ToResponsesAsync(IReadOnlyList<WeddingMember> members, CancellationToken ct)
    {
        var userIds = members.Where(m => m.UserId is not null).Select(m => m.UserId!.Value).ToList();
        var profiles = await users.GetManyAsync(userIds, ct);
        return members.Select(m =>
        {
            var profile = m.UserId is { } id ? profiles.GetValueOrDefault(id) : null;
            return new MemberResponse(m.Id, profile?.Name ?? m.Name, profile?.Email ?? m.Email, m.Role, m.Status, m.JoinedAt, m.Avatar);
        }).ToList();
    }
}

internal sealed record WeddingMemberAccepted(Guid WeddingId, Guid MemberId);
