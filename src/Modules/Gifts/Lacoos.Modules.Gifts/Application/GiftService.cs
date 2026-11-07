using Lacoos.Modules.Gifts.Domain;
using Lacoos.Modules.Gifts.Persistence;
using Lacoos.SharedKernel.Persistence;

namespace Lacoos.Modules.Gifts.Application;

internal sealed record GiftFilter(
    string? Search,
    Guid? CategoryId,
    string? Status,
    decimal? MinValue,
    decimal? MaxValue,
    bool? Available,
    bool? HasReservations,
    bool? HasReceipts);

internal sealed record SaveGiftRequest(
    string? Name,
    string? Description,
    Guid? CategoryId,
    decimal? EstimatedValue,
    int? DesiredQuantity,
    string? ExternalLink,
    string? ImageUrl);

internal sealed record GiftResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid CategoryId,
    string CategoryName,
    decimal EstimatedValue,
    int DesiredQuantity,
    string? ExternalLink,
    string? ImageUrl,
    GiftDisplayStatus Status,
    int ReservedUnits,
    int ReceivedUnits,
    int AvailableUnits);

internal sealed record GiftDetailResponse(
    GiftResponse Gift, IReadOnlyList<ReservationResponse> Reservations, IReadOnlyList<ReceiptResponse> Receipts);

internal sealed record GiftTotals(
    int ActiveGifts, int DesiredUnits, int ReservedUnits, int ReceivedUnits, int AvailableUnits, decimal EstimatedValue);

internal sealed record GiftListResponse(IReadOnlyList<GiftResponse> Items, GiftTotals Totals);

internal sealed record SaveReservationRequest(int? Quantity, Guid? GuestId, string? PersonName, DateOnly? ReservedAt);

internal sealed record ReservationResponse(
    Guid Id,
    Guid GiftId,
    int Quantity,
    int RemainingQuantity,
    Guid? GuestId,
    string? GuestName,
    string? PersonName,
    DateOnly ReservedAt,
    ReservationStatus Status);

internal sealed record SaveReceiptRequest(int? Quantity, Guid? GuestId, string? PersonName, DateOnly? ReceivedAt, Guid? ReservationId);

internal sealed record ReceiptResponse(
    Guid Id,
    Guid GiftId,
    int Quantity,
    Guid? GuestId,
    string? GuestName,
    string? PersonName,
    DateOnly ReceivedAt,
    Guid? ReservationId);

internal sealed record SaveCategoryRequest(string? Name);

internal sealed record CategoryResponse(Guid Id, string Name);

internal sealed class GiftService(GiftsDbContext db, IGuestQueries guests, IClock clock)
{
    public async Task<GiftListResponse> ListAsync(GiftFilter filter, CancellationToken ct)
    {
        var status = EnumText.ParseFilter<GiftDisplayStatus>(filter.Status, "status");
        var all = await LoadAll().OrderBy(g => g.Name).ToListAsync(ct);
        IEnumerable<Gift> matches = all;

        if (Text.Clean(filter.Search) is { } search)
        {
            matches = matches.Where(g =>
                g.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (g.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || g.Category!.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.CategoryId is not null)
        {
            matches = matches.Where(g => g.CategoryId == filter.CategoryId);
        }

        if (status is not null)
        {
            matches = matches.Where(g => g.DisplayStatus == status);
        }

        if (filter.MinValue is not null)
        {
            matches = matches.Where(g => g.EstimatedValue >= filter.MinValue);
        }

        if (filter.MaxValue is not null)
        {
            matches = matches.Where(g => g.EstimatedValue <= filter.MaxValue);
        }

        if (filter.Available is not null)
        {
            matches = matches.Where(g => (g.Status == GiftStatus.Active && g.AvailableUnits > 0) == filter.Available);
        }

        if (filter.HasReservations is not null)
        {
            matches = matches.Where(g => g.ReservedUnits > 0 == filter.HasReservations);
        }

        if (filter.HasReceipts is not null)
        {
            matches = matches.Where(g => g.ReceivedUnits > 0 == filter.HasReceipts);
        }

        return new GiftListResponse(matches.Select(ToResponse).ToList(), ToTotals(all));
    }

    public async Task<GiftTotals> GetTotalsAsync(CancellationToken ct) =>
        ToTotals(await LoadAll().ToListAsync(ct));

    public async Task<GiftDetailResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var gift = await LoadAll().FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException("Presente não encontrado.");

        var guestIds = gift.Reservations.Select(r => r.GuestId)
            .Concat(gift.Receipts.Select(r => r.GuestId))
            .Where(guestId => guestId is not null)
            .Select(guestId => guestId!.Value)
            .Distinct()
            .ToList();
        var names = await guests.GetNamesAsync(guestIds, ct);

        return new GiftDetailResponse(
            ToResponse(gift),
            gift.Reservations.OrderBy(r => r.ReservedAt).Select(r => ToResponse(gift, r, names)).ToList(),
            gift.Receipts.OrderBy(r => r.ReceivedAt).Select(r => ToResponse(r, names)).ToList());
    }

    public async Task<GiftDetailResponse> CreateAsync(SaveGiftRequest request, CancellationToken ct)
    {
        await ValidateAsync(request, ct);
        var gift = new Gift { Name = request.Name!.Trim() };
        Apply(gift, request);
        db.Gifts.Add(gift);
        await db.SaveChangesAsync(ct);
        return await GetAsync(gift.Id, ct);
    }

    public async Task<GiftDetailResponse> UpdateAsync(Guid id, SaveGiftRequest request, CancellationToken ct)
    {
        await ValidateAsync(request, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var gift = await LoadLockedAsync(id, ct);

        var committed = gift.ReservedUnits + gift.ReceivedUnits;
        if (request.DesiredQuantity < committed)
        {
            throw new BusinessRuleException($"A quantidade desejada não pode ser menor que as {committed} unidades reservadas ou recebidas.");
        }

        gift.Name = request.Name!.Trim();
        Apply(gift, request);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var gift = await db.Gifts.Include(g => g.Reservations).Include(g => g.Receipts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException("Presente não encontrado.");

        if (gift.Reservations.Count > 0 || gift.Receipts.Count > 0)
        {
            throw new ConflictException("Não é possível excluir um presente com reservas ou recebimentos. Cancele o presente.");
        }

        db.Gifts.Remove(gift);
        await db.SaveChangesAsync(ct);
    }

    public Task<GiftDetailResponse> CancelAsync(Guid id, CancellationToken ct) => SetStatusAsync(id, GiftStatus.Cancelled, ct);

    public Task<GiftDetailResponse> ReactivateAsync(Guid id, CancellationToken ct) => SetStatusAsync(id, GiftStatus.Active, ct);

    public async Task<ReservationResponse> ReserveAsync(Guid giftId, SaveReservationRequest request, CancellationToken ct)
    {
        await ValidateQuantityAndPersonAsync(request.Quantity, request.GuestId, request.PersonName, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var gift = await LoadLockedAsync(giftId, ct);
        EnsureActive(gift);

        if (request.Quantity!.Value > gift.AvailableUnits)
        {
            throw new BusinessRuleException($"Quantidade indisponível. Restam {gift.AvailableUnits} unidades.");
        }

        var reservation = new GiftReservation
        {
            GiftId = gift.Id,
            Quantity = request.Quantity.Value,
            GuestId = request.GuestId,
            PersonName = Text.Clean(request.PersonName),
            ReservedAt = request.ReservedAt ?? clock.Today,
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var names = await guests.GetNamesAsync(GuestIds(reservation.GuestId), ct);
        return ToResponse(gift, reservation, names);
    }

    public async Task<ReservationResponse> CancelReservationAsync(Guid reservationId, CancellationToken ct)
    {
        var giftId = await db.Reservations.Where(r => r.Id == reservationId).Select(r => (Guid?)r.GiftId).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Reserva não encontrada.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var gift = await LoadLockedAsync(giftId, ct);
        var reservation = gift.Reservations.First(r => r.Id == reservationId);

        if (reservation.Status == ReservationStatus.Cancelled)
        {
            throw new BusinessRuleException("Esta reserva já está cancelada.");
        }

        if (gift.RemainingOf(reservation) == 0)
        {
            throw new BusinessRuleException("Esta reserva já foi totalmente recebida.");
        }

        reservation.Status = ReservationStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var names = await guests.GetNamesAsync(GuestIds(reservation.GuestId), ct);
        return ToResponse(gift, reservation, names);
    }

    public async Task<ReceiptResponse> ReceiveAsync(Guid giftId, SaveReceiptRequest request, CancellationToken ct)
    {
        var fromReservation = request.ReservationId is not null;
        var hasPerson = request.GuestId is not null || Text.Clean(request.PersonName) is not null;

        // Com reserva, o recebimento herda a pessoa dela quando nenhuma é informada.
        if (fromReservation && !hasPerson)
        {
            ValidateQuantity(request.Quantity);
        }
        else
        {
            await ValidateQuantityAndPersonAsync(request.Quantity, request.GuestId, request.PersonName, ct);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var gift = await LoadLockedAsync(giftId, ct);
        EnsureActive(gift);

        var quantity = request.Quantity!.Value;
        var receipt = new GiftReceipt
        {
            GiftId = gift.Id,
            Quantity = quantity,
            GuestId = request.GuestId,
            PersonName = Text.Clean(request.PersonName),
            ReceivedAt = request.ReceivedAt ?? clock.Today,
            ReservationId = request.ReservationId,
        };

        if (fromReservation)
        {
            var reservation = gift.Reservations.FirstOrDefault(r => r.Id == request.ReservationId)
                ?? throw new BusinessRuleException("A reserva informada não pertence a este presente.");

            if (reservation.Status != ReservationStatus.Active)
            {
                throw new BusinessRuleException("Esta reserva está cancelada.");
            }

            var remaining = gift.RemainingOf(reservation);
            if (quantity > remaining)
            {
                throw new BusinessRuleException($"A quantidade excede o saldo da reserva ({remaining}).");
            }

            if (!hasPerson)
            {
                receipt.GuestId = reservation.GuestId;
                receipt.PersonName = reservation.PersonName;
            }
        }
        else if (quantity > gift.AvailableUnits)
        {
            throw new BusinessRuleException($"Quantidade indisponível. Restam {gift.AvailableUnits} unidades.");
        }

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var names = await guests.GetNamesAsync(GuestIds(receipt.GuestId), ct);
        return ToResponse(receipt, names);
    }

    public async Task<IReadOnlyList<CategoryResponse>> ListCategoriesAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => new CategoryResponse(c.Id, c.Name)).ToListAsync(ct);

    public async Task<CategoryResponse> GetCategoryAsync(Guid id, CancellationToken ct)
    {
        var category = await LoadCategoryAsync(id, ct);
        return new CategoryResponse(category.Id, category.Name);
    }

    public async Task<CategoryResponse> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken ct)
    {
        ValidateCategory(request);
        var category = new GiftCategory { Name = request.Name!.Trim() };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return new CategoryResponse(category.Id, category.Name);
    }

    public async Task<CategoryResponse> UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken ct)
    {
        ValidateCategory(request);
        var category = await LoadCategoryAsync(id, ct);
        category.Name = request.Name!.Trim();
        await db.SaveChangesAsync(ct);
        return new CategoryResponse(category.Id, category.Name);
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken ct)
    {
        var category = await LoadCategoryAsync(id, ct);
        if (await db.Gifts.AnyAsync(g => g.CategoryId == id, ct))
        {
            throw new ConflictException("Não é possível excluir uma categoria que tem presentes.");
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Gift> LoadAll() =>
        db.Gifts.AsNoTracking()
            .Include(g => g.Category)
            .Include(g => g.Reservations)
            .Include(g => g.Receipts)
            .AsSplitQuery();

    /// <summary>Bloqueia o presente até o commit, para duas reservas não disputarem as mesmas unidades.</summary>
    private async Task<Gift> LoadLockedAsync(Guid id, CancellationToken ct)
    {
        await db.LockRowAsync(GiftsDbContext.GiftsTable, id, ct);
        return await db.Gifts.Include(g => g.Reservations).Include(g => g.Receipts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException("Presente não encontrado.");
    }

    private async Task<GiftCategory> LoadCategoryAsync(Guid id, CancellationToken ct) =>
        await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("Categoria não encontrada.");

    private async Task<GiftDetailResponse> SetStatusAsync(Guid id, GiftStatus status, CancellationToken ct)
    {
        var gift = await db.Gifts.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException("Presente não encontrado.");
        gift.Status = status;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private static void EnsureActive(Gift gift)
    {
        if (gift.Status == GiftStatus.Cancelled)
        {
            throw new BusinessRuleException("Este presente está cancelado.");
        }
    }

    private async Task ValidateAsync(SaveGiftRequest request, CancellationToken ct)
    {
        var validator = new Validator()
            .Required(request.Name, "name", "Nome")
            .MaxLength(request.Name, 200, "name", "Nome")
            .Required(request.CategoryId, "categoryId", "Categoria")
            .Money(request.EstimatedValue, "estimatedValue", "Valor estimado")
            .Required(request.DesiredQuantity, "desiredQuantity", "Quantidade desejada")
            .Url(request.ExternalLink, "externalLink", "Link externo")
            .Url(request.ImageUrl, "imageUrl", "Imagem");
        if (request.DesiredQuantity is not null)
        {
            validator.Min(request.DesiredQuantity.Value, 1, "desiredQuantity", "Quantidade desejada");
        }

        validator.ThrowIfInvalid();

        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            throw new BusinessRuleException("Categoria não encontrada neste casamento.");
        }
    }

    private static void ValidateQuantity(int? quantity)
    {
        var validator = new Validator().Required(quantity, "quantity", "Quantidade");
        if (quantity is not null)
        {
            validator.Min(quantity.Value, 1, "quantity", "Quantidade");
        }

        validator.ThrowIfInvalid();
    }

    private async Task ValidateQuantityAndPersonAsync(int? quantity, Guid? guestId, string? personName, CancellationToken ct)
    {
        ValidateQuantity(quantity);

        var hasName = Text.Clean(personName) is not null;
        if (guestId is null == !hasName)
        {
            new Validator().Add("guestId", "Informe um convidado ou um nome, apenas um dos dois.").ThrowIfInvalid();
        }

        if (guestId is { } id && !await guests.ExistsAsync(id, ct))
        {
            throw new BusinessRuleException("Convidado não encontrado neste casamento.");
        }
    }

    private static IReadOnlyCollection<Guid> GuestIds(Guid? guestId) => guestId is { } id ? [id] : [];

    private static void ValidateCategory(SaveCategoryRequest request) =>
        new Validator()
            .Required(request.Name, "name", "Nome")
            .MaxLength(request.Name, 200, "name", "Nome")
            .ThrowIfInvalid();

    private static void Apply(Gift gift, SaveGiftRequest request)
    {
        gift.Description = Text.Clean(request.Description);
        gift.CategoryId = request.CategoryId!.Value;
        gift.EstimatedValue = request.EstimatedValue ?? 0;
        gift.DesiredQuantity = request.DesiredQuantity!.Value;
        gift.ExternalLink = Text.Clean(request.ExternalLink);
        gift.ImageUrl = Text.Clean(request.ImageUrl);
    }

    private static GiftTotals ToTotals(IReadOnlyList<Gift> gifts)
    {
        var active = gifts.Where(g => g.Status == GiftStatus.Active).ToList();
        return new GiftTotals(
            active.Count,
            active.Sum(g => g.DesiredQuantity),
            active.Sum(g => g.ReservedUnits),
            active.Sum(g => g.ReceivedUnits),
            active.Sum(g => g.AvailableUnits),
            active.Sum(g => g.EstimatedValue * g.DesiredQuantity));
    }

    private static GiftResponse ToResponse(Gift g) => new(
        g.Id, g.Name, g.Description, g.CategoryId, g.Category?.Name ?? string.Empty, g.EstimatedValue, g.DesiredQuantity,
        g.ExternalLink, g.ImageUrl, g.DisplayStatus, g.ReservedUnits, g.ReceivedUnits, g.AvailableUnits);

    private static ReservationResponse ToResponse(Gift gift, GiftReservation r, IReadOnlyDictionary<Guid, string> names) => new(
        r.Id, r.GiftId, r.Quantity, gift.RemainingOf(r), r.GuestId,
        r.GuestId is { } id ? names.GetValueOrDefault(id) : null, r.PersonName, r.ReservedAt, r.Status);

    private static ReceiptResponse ToResponse(GiftReceipt r, IReadOnlyDictionary<Guid, string> names) => new(
        r.Id, r.GiftId, r.Quantity, r.GuestId,
        r.GuestId is { } id ? names.GetValueOrDefault(id) : null, r.PersonName, r.ReceivedAt, r.ReservationId);
}
