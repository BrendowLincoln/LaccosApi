using Lacoos.Modules.Weddings.Domain;
using Lacoos.Modules.Weddings.Persistence;

namespace Lacoos.Modules.Weddings.Application;

internal sealed record CreateWeddingRequest(string? Name, DateOnly? WeddingDate, int? EstimatedGuestCount);

internal sealed record UpdateWeddingRequest(
    string? Name,
    DateOnly? WeddingDate,
    int? EstimatedGuestCount,
    string? Partner1,
    string? Partner2,
    TimeOnly? Time,
    string? City,
    string? State,
    string? Venue,
    string? Notes,
    string? CoverImage);

internal sealed record ChangeStatusRequest(WeddingStatus? Status);

internal sealed record WeddingResponse(
    Guid Id,
    string Name,
    DateOnly WeddingDate,
    int EstimatedGuestCount,
    WeddingStatus Status,
    string? Partner1,
    string? Partner2,
    TimeOnly? Time,
    string? City,
    string? State,
    string? Venue,
    string? Notes,
    string? CoverImage,
    WeddingRole? Role);

internal sealed class WeddingService(
    WeddingsDbContext db,
    IUserDirectory users,
    ICurrentUser currentUser,
    IWeddingContext weddingContext,
    IClock clock)
{
    public async Task<WeddingResponse> CreateAsync(CreateWeddingRequest request, CancellationToken ct)
    {
        Validate(request.Name, request.WeddingDate, request.EstimatedGuestCount);

        var user = await users.FindAsync(currentUser.RequiredUserId, ct)
            ?? throw new UnauthorizedException("Autenticação necessária.");

        var wedding = new Wedding
        {
            Name = request.Name!.Trim(),
            WeddingDate = request.WeddingDate!.Value,
            EstimatedGuestCount = request.EstimatedGuestCount!.Value,
        };
        db.Weddings.Add(wedding);
        db.Members.Add(new WeddingMember
        {
            WeddingId = wedding.Id,
            UserId = user.Id,
            Name = user.Name,
            Email = user.Email,
            NormalizedEmail = WeddingMember.Normalize(user.Email),
            Role = WeddingRole.Owner,
            Status = MemberStatus.Active,
            JoinedAt = clock.Today,
        });
        await db.SaveChangesAsync(ct);
        return ToResponse(wedding, WeddingRole.Owner);
    }

    public async Task<IReadOnlyList<WeddingResponse>> ListMineAsync(CancellationToken ct)
    {
        var userId = currentUser.RequiredUserId;
        var rows = await (
            from member in db.Members.AsNoTracking()
            join wedding in db.Weddings.AsNoTracking() on member.WeddingId equals wedding.Id
            where member.UserId == userId && member.Status == MemberStatus.Active
            orderby wedding.WeddingDate
            select new { wedding, member.Role }).ToListAsync(ct);
        return rows.Select(r => ToResponse(r.wedding, r.Role)).ToList();
    }

    public async Task<WeddingResponse> GetCurrentAsync(CancellationToken ct) =>
        ToResponse(await LoadCurrentAsync(ct), weddingContext.Role);

    public async Task<WeddingResponse> UpdateAsync(UpdateWeddingRequest request, CancellationToken ct)
    {
        Validate(request.Name, request.WeddingDate, request.EstimatedGuestCount);

        var wedding = await LoadCurrentAsync(ct);
        wedding.Name = request.Name!.Trim();
        wedding.WeddingDate = request.WeddingDate!.Value;
        wedding.EstimatedGuestCount = request.EstimatedGuestCount!.Value;
        wedding.Partner1 = Text.Clean(request.Partner1);
        wedding.Partner2 = Text.Clean(request.Partner2);
        wedding.Time = request.Time;
        wedding.City = Text.Clean(request.City);
        wedding.State = Text.Clean(request.State);
        wedding.Venue = Text.Clean(request.Venue);
        wedding.Notes = Text.Clean(request.Notes);
        wedding.CoverImage = Text.Clean(request.CoverImage);
        await db.SaveChangesAsync(ct);
        return ToResponse(wedding, weddingContext.Role);
    }

    public async Task<WeddingResponse> ChangeStatusAsync(ChangeStatusRequest request, CancellationToken ct)
    {
        new Validator().Required(request.Status, "status", "Status").ThrowIfInvalid();

        var wedding = await LoadCurrentAsync(ct);
        wedding.Status = request.Status!.Value;
        await db.SaveChangesAsync(ct);
        return ToResponse(wedding, weddingContext.Role);
    }

    private async Task<Wedding> LoadCurrentAsync(CancellationToken ct) =>
        await db.Weddings.FirstOrDefaultAsync(w => w.Id == weddingContext.WeddingId, ct)
        ?? throw new NotFoundException("Casamento não encontrado.");

    private static void Validate(string? name, DateOnly? date, int? estimated)
    {
        var validator = new Validator()
            .Required(name, "name", "Nome")
            .MaxLength(name, 200, "name", "Nome")
            .Required(date, "weddingDate", "Data do casamento")
            .Required(estimated, "estimatedGuestCount", "Convidados estimados");
        if (estimated is not null)
        {
            validator.Min(estimated.Value, 0, "estimatedGuestCount", "Convidados estimados");
        }

        validator.ThrowIfInvalid();
    }

    private static WeddingResponse ToResponse(Wedding w, WeddingRole? role) => new(
        w.Id, w.Name, w.WeddingDate, w.EstimatedGuestCount, w.Status, w.Partner1, w.Partner2,
        w.Time, w.City, w.State, w.Venue, w.Notes, w.CoverImage, role);
}
