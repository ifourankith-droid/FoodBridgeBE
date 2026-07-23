namespace FoodBridge.Application.Common;

public static class PaginationHelper
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize switch
        {
            <= 0 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize,
        };

        return (normalizedPage, normalizedPageSize);
    }
}
