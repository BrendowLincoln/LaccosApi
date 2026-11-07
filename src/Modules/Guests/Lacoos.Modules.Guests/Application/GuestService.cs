using Lacoos.Modules.Guests.Domain;
using Lacoos.Modules.Guests.Persistence;

namespace Lacoos.Modules.Guests.Application;

internal sealed record GuestFilter(
    string? Search, Guid? GroupId, string? GroupCategory, string? InvitationStatus, string? AttendanceStatus);

internal sealed record SaveGuestRequest(
    string? FirstName,
    string? LastName,
    Guid? GroupId,
    string? Email,
    string? Phone,
    bool? IsPrimaryGuest,
    int? MaxCompanions,
    InvitationStatus? InvitationStatus,
    AttendanceStatus? AttendanceStatus,
    string? DietaryRestrictions,
    string? AccessibilityNeeds,
    string? Notes);

internal sealed record GuestResponse(
    Guid Id,
    string FirstName,
    string LastName,
    Guid? GroupId,
    string? GroupName,
    string? Email,
    string? Phone,
    bool IsPrimaryGuest,
    int MaxCompanions,
    InvitationStatus InvitationStatus,
    AttendanceStatus AttendanceStatus,
    string? DietaryRestrictions,
    string? AccessibilityNeeds,
    string? Notes);

internal sealed record GuestSummary(
    int Total,
    int Confirmed,
    int Pending,
    int Declined,
    decimal ConfirmedPercent,
    int CompanionsAllowed,
    int InvitationsSent,
    int InvitationsOpened);

internal sealed record GuestListResponse(IReadOnlyList<GuestResponse> Items, GuestSummary Totals);

internal sealed record SaveGroupRequest(string? Name, GroupCategory? Category, string? Notes);

internal sealed record GroupResponse(Guid Id, string Name, GroupCategory Category, string? Notes);

internal sealed class GuestService(GuestsDbContext db, IGiftGuestLinks gifts)
{
    public async Task<GuestListResponse> ListAsync(GuestFilter filter, CancellationToken ct)
    {
        var groupCategory = EnumText.ParseFilter<GroupCategory>(filter.GroupCategory, "groupCategory");
        var invitation = EnumText.ParseFilter<InvitationStatus>(filter.InvitationStatus, "invitationStatus");
        var attendance = EnumText.ParseFilter<AttendanceStatus>(filter.AttendanceStatus, "attendanceStatus");

        var query = db.Guests.AsNoTracking().Include(g => g.Group).AsQueryable();

        if (Text.Clean(filter.Search) is { } search)
        {
            var pattern = Text.ContainsPattern(search);
            query = query.Where(g =>
                EF.Functions.ILike(g.FirstName + " " + g.LastName, pattern, @"\")
                || (g.Email != null && EF.Functions.ILike(g.Email, pattern, @"\"))
                || (g.Phone != null && EF.Functions.ILike(g.Phone, pattern, @"\"))
                || (g.Group != null && EF.Functions.ILike(g.Group.Name, pattern, @"\")));
        }

        if (filter.GroupId is not null)
        {
            query = query.Where(g => g.GroupId == filter.GroupId);
        }

        if (groupCategory is not null)
        {
            query = query.Where(g => g.Group != null && g.Group.Category == groupCategory);
        }

        if (invitation is not null)
        {
            query = query.Where(g => g.InvitationStatus == invitation);
        }

        if (attendance is not null)
        {
            query = query.Where(g => g.AttendanceStatus == attendance);
        }

        var guests = await query.OrderBy(g => g.FirstName).ThenBy(g => g.LastName).ToListAsync(ct);
        return new GuestListResponse(guests.Select(ToResponse).ToList(), await GetSummaryAsync(ct));
    }

    public async Task<GuestSummary> GetSummaryAsync(CancellationToken ct)
    {
        var guests = await db.Guests.AsNoTracking()
            .Select(g => new { g.AttendanceStatus, g.InvitationStatus, g.MaxCompanions })
            .ToListAsync(ct);
        var confirmed = guests.Count(g => g.AttendanceStatus == AttendanceStatus.Confirmed);
        return new GuestSummary(
            guests.Count,
            confirmed,
            guests.Count(g => g.AttendanceStatus == AttendanceStatus.Pending),
            guests.Count(g => g.AttendanceStatus == AttendanceStatus.Declined),
            Text.Percent(confirmed, guests.Count),
            guests.Sum(g => g.MaxCompanions),
            guests.Count(g => g.InvitationStatus != InvitationStatus.NotSent),
            guests.Count(g => g.InvitationStatus == InvitationStatus.Opened));
    }

    public async Task<GuestResponse> GetAsync(Guid id, CancellationToken ct) =>
        ToResponse(await db.Guests.AsNoTracking().Include(g => g.Group).FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException("Convidado não encontrado."));

    public async Task<GuestResponse> CreateAsync(SaveGuestRequest request, CancellationToken ct)
    {
        await ValidateAsync(request, ct);
        var guest = new Guest { FirstName = request.FirstName!.Trim(), LastName = request.LastName!.Trim() };
        Apply(guest, request);
        db.Guests.Add(guest);
        await db.SaveChangesAsync(ct);
        return await GetAsync(guest.Id, ct);
    }

    public async Task<GuestResponse> UpdateAsync(Guid id, SaveGuestRequest request, CancellationToken ct)
    {
        await ValidateAsync(request, ct);
        var guest = await LoadAsync(id, ct);
        guest.FirstName = request.FirstName!.Trim();
        guest.LastName = request.LastName!.Trim();
        Apply(guest, request);
        await db.SaveChangesAsync(ct);
        return await GetAsync(guest.Id, ct);
    }

    public async Task<GuestResponse> MarkSentAsync(Guid id, CancellationToken ct)
    {
        var guest = await LoadAsync(id, ct);
        guest.MarkInvitationSent();
        await db.SaveChangesAsync(ct);
        return await GetAsync(guest.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var guest = await LoadAsync(id, ct);

        // Primeiro preserva o nome nos presentes: se a exclusão falhar, nenhum vínculo fica quebrado.
        await gifts.DetachGuestAsync(guest.Id, guest.FullName, ct);

        db.Guests.Remove(guest);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<GroupResponse>> ListGroupsAsync(CancellationToken ct) =>
        await db.Groups.AsNoTracking().OrderBy(g => g.Name)
            .Select(g => new GroupResponse(g.Id, g.Name, g.Category, g.Notes))
            .ToListAsync(ct);

    public async Task<GroupResponse> GetGroupAsync(Guid id, CancellationToken ct) =>
        ToResponse(await LoadGroupAsync(id, ct));

    public async Task<GroupResponse> CreateGroupAsync(SaveGroupRequest request, CancellationToken ct)
    {
        ValidateGroup(request);
        var group = new GuestGroup
        {
            Name = request.Name!.Trim(),
            Category = request.Category!.Value,
            Notes = Text.Clean(request.Notes),
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync(ct);
        return ToResponse(group);
    }

    public async Task<GroupResponse> UpdateGroupAsync(Guid id, SaveGroupRequest request, CancellationToken ct)
    {
        ValidateGroup(request);
        var group = await LoadGroupAsync(id, ct);
        group.Name = request.Name!.Trim();
        group.Category = request.Category!.Value;
        group.Notes = Text.Clean(request.Notes);
        await db.SaveChangesAsync(ct);
        return ToResponse(group);
    }

    public async Task DeleteGroupAsync(Guid id, CancellationToken ct)
    {
        var group = await LoadGroupAsync(id, ct);
        if (await db.Guests.AnyAsync(g => g.GroupId == id, ct))
        {
            throw new ConflictException("Não é possível excluir um grupo que tem convidados.");
        }

        db.Groups.Remove(group);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Guest> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Guests.FirstOrDefaultAsync(g => g.Id == id, ct)
        ?? throw new NotFoundException("Convidado não encontrado.");

    private async Task<GuestGroup> LoadGroupAsync(Guid id, CancellationToken ct) =>
        await db.Groups.FirstOrDefaultAsync(g => g.Id == id, ct)
        ?? throw new NotFoundException("Grupo não encontrado.");

    private async Task ValidateAsync(SaveGuestRequest request, CancellationToken ct)
    {
        var validator = new Validator()
            .Required(request.FirstName, "firstName", "Nome")
            .MaxLength(request.FirstName, 100, "firstName", "Nome")
            .Required(request.LastName, "lastName", "Sobrenome")
            .MaxLength(request.LastName, 100, "lastName", "Sobrenome")
            .Email(request.Email, "email")
            .MaxLength(request.Phone, 40, "phone", "Telefone");

        if (request.MaxCompanions is < 0)
        {
            validator.Add("maxCompanions", "Acompanhantes não pode ser negativo.");
        }

        validator.ThrowIfInvalid();

        if (request.GroupId is { } groupId && !await db.Groups.AnyAsync(g => g.Id == groupId, ct))
        {
            throw new BusinessRuleException("Grupo não encontrado neste casamento.");
        }
    }

    private static void ValidateGroup(SaveGroupRequest request) =>
        new Validator()
            .Required(request.Name, "name", "Nome")
            .MaxLength(request.Name, 200, "name", "Nome")
            .Required(request.Category, "category", "Categoria")
            .ThrowIfInvalid();

    private static void Apply(Guest guest, SaveGuestRequest request)
    {
        guest.GroupId = request.GroupId;
        guest.Email = Text.Clean(request.Email);
        guest.Phone = Text.Clean(request.Phone);
        guest.IsPrimaryGuest = request.IsPrimaryGuest ?? true;
        guest.MaxCompanions = request.MaxCompanions ?? 0;
        guest.InvitationStatus = request.InvitationStatus ?? guest.InvitationStatus;
        guest.AttendanceStatus = request.AttendanceStatus ?? guest.AttendanceStatus;
        guest.DietaryRestrictions = Text.Clean(request.DietaryRestrictions);
        guest.AccessibilityNeeds = Text.Clean(request.AccessibilityNeeds);
        guest.Notes = Text.Clean(request.Notes);
    }

    private static GroupResponse ToResponse(GuestGroup g) => new(g.Id, g.Name, g.Category, g.Notes);

    private static GuestResponse ToResponse(Guest g) => new(
        g.Id, g.FirstName, g.LastName, g.GroupId, g.Group?.Name, g.Email, g.Phone, g.IsPrimaryGuest, g.MaxCompanions,
        g.InvitationStatus, g.AttendanceStatus, g.DietaryRestrictions, g.AccessibilityNeeds, g.Notes);
}
