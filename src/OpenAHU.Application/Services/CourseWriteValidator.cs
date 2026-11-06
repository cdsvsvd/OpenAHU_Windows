using OpenAHU.Application.Models;

namespace OpenAHU.Application.Services;

/// <summary>
/// 课程字段校验（<b>纯函数</b>，不访问数据库，便于单元测试）。
/// 只做“单字段 + 跨字段”的规则，不判断与库内其他课程的冲突——那是 <c>CourseCommand</c> 的职责。
/// </summary>
internal static class CourseWriteValidator
{
    /// <summary>课程名上限，与 UI 输入框长度保持一致。</summary>
    private const int MaxNameLength = 60;

    /// <summary>教师名上限。</summary>
    private const int MaxTeacherLength = 40;

    /// <summary>地点上限。</summary>
    private const int MaxLocationLength = 60;

    /// <summary>节次下限。</summary>
    private const int MinSection = 1;

    /// <summary>节次上限（与 <c>courses.start_section/end_section</c> 的 CHECK 约束一致）。</summary>
    private const int MaxSection = 20;

    /// <summary>校验课程字段。</summary>
    /// <param name="draft">待校验字段。</param>
    /// <param name="term">所属学期，用于判断周次是否越界。</param>
    /// <returns>通过时返回 <c>null</c>；否则返回 <see cref="CourseWriteStatus.ValidationFailed"/> 结果。</returns>
    public static CourseWriteResult? Validate(CourseDraft draft, Term term)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(term);

        var name = draft.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return CourseWriteResult.ValidationFailed("name", "课程名称不能为空。");
        }

        if (name.Length > MaxNameLength)
        {
            return CourseWriteResult.ValidationFailed("name", $"课程名称不能超过 {MaxNameLength} 个字符。");
        }

        if ((draft.Teacher?.Trim().Length ?? 0) > MaxTeacherLength)
        {
            return CourseWriteResult.ValidationFailed("teacher", $"任课教师不能超过 {MaxTeacherLength} 个字符。");
        }

        if ((draft.Location?.Trim().Length ?? 0) > MaxLocationLength)
        {
            return CourseWriteResult.ValidationFailed("location", $"上课地点不能超过 {MaxLocationLength} 个字符。");
        }

        if (!Enum.IsDefined(draft.DayOfWeek))
        {
            return CourseWriteResult.ValidationFailed("dayOfWeek", "上课星期不合法，应为周日(0)到周六(6)。");
        }

        if (draft.StartSection is < MinSection or > MaxSection)
        {
            return CourseWriteResult.ValidationFailed(
                "startSection",
                $"起始节次必须在 {MinSection}–{MaxSection} 之间。");
        }

        if (draft.EndSection < draft.StartSection || draft.EndSection > MaxSection)
        {
            return CourseWriteResult.ValidationFailed(
                "endSection",
                $"结束节次必须不小于起始节次，且不超过 {MaxSection}。");
        }

        if (draft.WeekNumbers is null || draft.WeekNumbers.Count == 0)
        {
            return CourseWriteResult.ValidationFailed("weekNumbers", "至少需要选择一个开课周次。");
        }

        if (draft.WeekNumbers.Any(week => week < 1 || week > term.WeekCount))
        {
            return CourseWriteResult.ValidationFailed(
                "weekNumbers",
                $"开课周次必须在第 1–{term.WeekCount} 周之间（本学期共 {term.WeekCount} 周）。");
        }

        return null;
    }
}
