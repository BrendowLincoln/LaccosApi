using Lacoos.Modules.Checklist.Domain;
using Lacoos.Modules.Checklist.Persistence;

namespace Lacoos.Modules.Checklist.Application;

internal sealed record ChecklistFilter(
    string? Search,
    string? Status,
    string? Priority,
    Guid? CategoryId,
    Guid? AssignedToMemberId,
    DateOnly? DueFrom,
    DateOnly? DueTo,
    string? Sort);

internal sealed record SaveItemRequest(
    string? Title,
    string? Description,
    Guid? CategoryId,
    Guid? AssignedToMemberId,
    DateOnly? DueDate,
    ChecklistPriority? Priority,
    ChecklistStatus? Status);

internal sealed record ItemResponse(
    Guid Id,
    string Title,
    string? Description,
    Guid? CategoryId,
    string? CategoryName,
    Guid? AssignedToMemberId,
    string? AssignedToMemberName,
    DateOnly? DueDate,
    ChecklistPriority Priority,
    ChecklistStatus Status);

internal sealed record ChecklistTotals(int Total, int Pending, int InProgress, int Done, decimal CompletionPercent);

internal sealed record ItemListResponse(IReadOnlyList<ItemResponse> Items, ChecklistTotals Totals);

internal sealed record SaveCategoryRequest(string? Name, string? Description);

internal sealed record SetActiveRequest(bool? Active);

internal sealed record CategoryResponse(Guid Id, string Name, string? Description, bool Active);

internal sealed class ChecklistService(ChecklistDbContext db, IWeddingDirectory wedding)
{
    public async Task<ItemListResponse> ListAsync(ChecklistFilter filter, CancellationToken ct)
    {
        var status = EnumText.ParseFilter<ChecklistStatus>(filter.Status, "status");
        var priority = EnumText.ParseFilter<ChecklistPriority>(filter.Priority, "priority");

        var query = db.Items.AsNoTracking().Include(i => i.Category).AsQueryable();

        if (Text.Clean(filter.Search) is { } search)
        {
            var pattern = Text.ContainsPattern(search);
            query = query.Where(i => EF.Functions.ILike(i.Title, pattern, @"\")
                || (i.Description != null && EF.Functions.ILike(i.Description, pattern, @"\")));
        }

        if (status is not null)
        {
            query = query.Where(i => i.Status == status);
        }

        if (priority is not null)
        {
            query = query.Where(i => i.Priority == priority);
        }

        if (filter.CategoryId is not null)
        {
            query = query.Where(i => i.CategoryId == filter.CategoryId);
        }

        if (filter.AssignedToMemberId is not null)
        {
            query = query.Where(i => i.AssignedToMemberId == filter.AssignedToMemberId);
        }

        if (filter.DueFrom is not null)
        {
            query = query.Where(i => i.DueDate >= filter.DueFrom);
        }

        if (filter.DueTo is not null)
        {
            query = query.Where(i => i.DueDate <= filter.DueTo);
        }

        // Tarefas sem prazo ficam sempre por último.
        query = filter.Sort == "-dueDate"
            ? query.OrderBy(i => i.DueDate == null).ThenByDescending(i => i.DueDate).ThenBy(i => i.Title)
            : query.OrderBy(i => i.DueDate == null).ThenBy(i => i.DueDate).ThenBy(i => i.Title);

        var items = await query.ToListAsync(ct);
        return new ItemListResponse(await ToResponsesAsync(items, ct), await GetTotalsAsync(ct));
    }

    public async Task<ItemResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Items.AsNoTracking().Include(i => i.Category).FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Tarefa não encontrada.");
        return (await ToResponsesAsync([item], ct))[0];
    }

    public async Task<ItemResponse> CreateAsync(SaveItemRequest request, CancellationToken ct)
    {
        ValidateItem(request);
        await EnsureReferencesAsync(request, currentCategoryId: null, ct);

        var item = new ChecklistItem { Title = request.Title!.Trim() };
        Apply(item, request);
        db.Items.Add(item);
        await db.SaveChangesAsync(ct);
        return await GetAsync(item.Id, ct);
    }

    public async Task<ItemResponse> UpdateAsync(Guid id, SaveItemRequest request, CancellationToken ct)
    {
        ValidateItem(request);
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Tarefa não encontrada.");
        await EnsureReferencesAsync(request, item.CategoryId, ct);

        item.Title = request.Title!.Trim();
        Apply(item, request);
        await db.SaveChangesAsync(ct);
        return await GetAsync(item.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var deleted = await db.Items.Where(i => i.Id == id).ExecuteDeleteAsync(ct);
        if (deleted == 0)
        {
            throw new NotFoundException("Tarefa não encontrada.");
        }
    }

    public async Task<IReadOnlyList<CategoryResponse>> ListCategoriesAsync(bool? active, CancellationToken ct)
    {
        var query = db.Categories.AsNoTracking();
        if (active is not null)
        {
            query = query.Where(c => c.Active == active);
        }

        return await query.OrderBy(c => c.Name)
            .Select(c => new CategoryResponse(c.Id, c.Name, c.Description, c.Active))
            .ToListAsync(ct);
    }

    public async Task<CategoryResponse> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken ct)
    {
        ValidateCategory(request);
        var category = new ChecklistCategory { Name = request.Name!.Trim(), Description = Text.Clean(request.Description) };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return ToResponse(category);
    }

    public async Task<CategoryResponse> UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken ct)
    {
        ValidateCategory(request);
        var category = await LoadCategoryAsync(id, ct);
        category.Name = request.Name!.Trim();
        category.Description = Text.Clean(request.Description);
        await db.SaveChangesAsync(ct);
        return ToResponse(category);
    }

    public async Task<CategoryResponse> SetCategoryActiveAsync(Guid id, SetActiveRequest request, CancellationToken ct)
    {
        new Validator().Required(request.Active, "active", "Ativo").ThrowIfInvalid();
        var category = await LoadCategoryAsync(id, ct);
        category.Active = request.Active!.Value;
        await db.SaveChangesAsync(ct);
        return ToResponse(category);
    }

    private async Task<ChecklistCategory> LoadCategoryAsync(Guid id, CancellationToken ct) =>
        await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("Categoria não encontrada.");

    private async Task EnsureReferencesAsync(SaveItemRequest request, Guid? currentCategoryId, CancellationToken ct)
    {
        if (request.CategoryId is { } categoryId)
        {
            var category = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId, ct)
                ?? throw new BusinessRuleException("Categoria não encontrada neste casamento.");

            // Tarefas existentes mantêm a categoria desativada; só não pode ser escolhida de novo.
            if (!category.Active && categoryId != currentCategoryId)
            {
                throw new BusinessRuleException("Esta categoria está inativa.");
            }
        }

        if (request.AssignedToMemberId is { } memberId && !await wedding.MemberExistsAsync(memberId, ct))
        {
            throw new BusinessRuleException("O responsável deve ser membro deste casamento.");
        }
    }

    private async Task<ChecklistTotals> GetTotalsAsync(CancellationToken ct)
    {
        var counts = await db.Items.AsNoTracking()
            .GroupBy(i => i.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        var pending = counts.GetValueOrDefault(ChecklistStatus.Pending);
        var inProgress = counts.GetValueOrDefault(ChecklistStatus.InProgress);
        var done = counts.GetValueOrDefault(ChecklistStatus.Done);
        var total = pending + inProgress + done;
        return new ChecklistTotals(total, pending, inProgress, done, Text.Percent(done, total));
    }

    private async Task<IReadOnlyList<ItemResponse>> ToResponsesAsync(IReadOnlyList<ChecklistItem> items, CancellationToken ct)
    {
        var memberIds = items.Where(i => i.AssignedToMemberId is not null).Select(i => i.AssignedToMemberId!.Value).Distinct().ToList();
        var names = await wedding.GetMemberNamesAsync(memberIds, ct);
        return items.Select(i => new ItemResponse(
            i.Id, i.Title, i.Description, i.CategoryId, i.Category?.Name, i.AssignedToMemberId,
            i.AssignedToMemberId is { } id ? names.GetValueOrDefault(id) : null,
            i.DueDate, i.Priority, i.Status)).ToList();
    }

    private static void Apply(ChecklistItem item, SaveItemRequest request)
    {
        item.Description = Text.Clean(request.Description);
        item.CategoryId = request.CategoryId;
        item.AssignedToMemberId = request.AssignedToMemberId;
        item.DueDate = request.DueDate;
        item.Priority = request.Priority ?? ChecklistPriority.Medium;
        item.Status = request.Status ?? ChecklistStatus.Pending;
    }

    private static void ValidateItem(SaveItemRequest request) =>
        new Validator()
            .Required(request.Title, "title", "Título")
            .MaxLength(request.Title, 300, "title", "Título")
            .ThrowIfInvalid();

    private static void ValidateCategory(SaveCategoryRequest request) =>
        new Validator()
            .Required(request.Name, "name", "Nome")
            .MaxLength(request.Name, 200, "name", "Nome")
            .ThrowIfInvalid();

    private static CategoryResponse ToResponse(ChecklistCategory c) => new(c.Id, c.Name, c.Description, c.Active);
}
