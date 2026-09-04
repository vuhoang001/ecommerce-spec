namespace ECommerce.Shared.Kernel.Primitives;

/// <summary>
/// A page of results that always states the total and the position within it (FR-007),
/// and states why it is empty when it is (FR-008).
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    string? EmptyReason = null)
{
    public static PagedResult<T> Empty(int page, int pageSize, int totalCount, string emptyReason)
        => new([], page, pageSize, totalCount, emptyReason);
}
