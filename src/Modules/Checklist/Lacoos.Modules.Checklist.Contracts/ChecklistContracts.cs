namespace Lacoos.Modules.Checklist.Contracts;

public sealed record ChecklistSummary(int Total, int Done, int Open, decimal CompletionPercent);

public sealed record UpcomingTask(Guid Id, string Title, DateOnly DueDate, string Priority, string Status, Guid? AssignedToMemberId);

public interface IChecklistQueries
{
    Task<ChecklistSummary> GetSummaryAsync(CancellationToken ct);

    /// <summary>Tarefas abertas com prazo até a data limite, incluindo as atrasadas.</summary>
    Task<IReadOnlyList<UpcomingTask>> GetUpcomingTasksAsync(DateOnly until, int limit, CancellationToken ct);
}

public interface IChecklistAssignments
{
    /// <summary>Deixa sem responsável as tarefas de um membro que será removido.</summary>
    Task UnassignMemberAsync(Guid memberId, CancellationToken ct);
}
