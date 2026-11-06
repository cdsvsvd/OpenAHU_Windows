namespace OpenAHU.Application.Models;

/// <summary>
/// 学期（对应 SQLite <c>terms</c> 表），也是周次合法性的唯一判据。
/// </summary>
/// <param name="Id">学期主键，例如 <c>term-2026-fall</c>。</param>
/// <param name="Name">学期名称。</param>
/// <param name="StartDate">教学首周周一，周次推算基准。</param>
/// <param name="WeekCount">总教学周数，数据库约束 1–30；课程周次不得超出此值。</param>
/// <param name="IsCurrent">是否当前学期。</param>
public sealed record Term(
    string Id,
    string Name,
    DateOnly StartDate,
    int WeekCount,
    bool IsCurrent);
