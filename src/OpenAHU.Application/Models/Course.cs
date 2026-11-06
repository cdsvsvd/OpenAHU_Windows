namespace OpenAHU.Application.Models;

/// <summary>
/// 一门课程及其开课时段（<c>courses</c> + <c>course_weeks</c>）。
/// </summary>
/// <remarks>
/// 星期一律使用 C# <see cref="System.DayOfWeek"/> 语义（周日=0 … 周六=6）。
/// SQLite 侧的 周一=1 … 周日=7 换算由 05 号仓储在边界上完成，本层不再做二次转换，
/// 避免出现「周日错一格」。参见 INTERFACE_PLAN.md §4。
/// </remarks>
/// <param name="Id">课程主键。新增时由 Application 生成。</param>
/// <param name="Name">课程名称。</param>
/// <param name="Teacher">任课教师；无则为空字符串，不使用 <c>null</c>。</param>
/// <param name="Location">上课地点；无则为空字符串。</param>
/// <param name="DayOfWeek">上课星期，C# 语义。</param>
/// <param name="StartSection">起始节次 1–20。</param>
/// <param name="EndSection">结束节次，不小于 <paramref name="StartSection"/> 且不超过 20。</param>
/// <param name="WeekNumbers">开课周次，升序、去重、非空；空表示尚未排课。</param>
/// <param name="TermId">所属学期主键。</param>
public sealed record Course(
    string Id,
    string Name,
    string Teacher,
    string Location,
    DayOfWeek DayOfWeek,
    int StartSection,
    int EndSection,
    IReadOnlyList<int> WeekNumbers,
    string TermId);
