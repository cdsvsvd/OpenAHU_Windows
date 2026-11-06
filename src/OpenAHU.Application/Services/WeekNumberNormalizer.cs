namespace OpenAHU.Application.Services;

/// <summary>
/// 开课周次的规范化：升序 + 去重。
/// 契约要求 <c>weekNumbers</c> 以升序去重形式出现在响应里，写入时也必须按同一形式落库，
/// 因此读、写两条路径共用同一段逻辑，避免“读出来是乱的、写进去是排好的”这种不一致。
/// </summary>
internal static class WeekNumberNormalizer
{
    /// <summary>升序去重；<c>null</c> 视为空集合。</summary>
    /// <param name="weekNumbers">原始周次。</param>
    /// <returns>不可变语义的升序去重数组。</returns>
    public static IReadOnlyList<int> Normalize(IEnumerable<int>? weekNumbers)
    {
        if (weekNumbers is null)
        {
            return Array.Empty<int>();
        }

        return weekNumbers.Distinct().OrderBy(week => week).ToArray();
    }
}
