using OpenAHU.Contracts.Local;

namespace OpenAHU.Application;

/// <summary>
/// Pure course-input validation adapted from member 04's domain validator.
/// Keeps the existing API DTO, field limits and invalid_request error contract.
/// Does not access the database; term existence and schedule conflicts are separate.
/// </summary>
internal static class CourseWriteValidator
{
    private static CampusException Invalid(string field, string message) =>
        new(CampusErrorKind.InvalidRequest, $"{field}: {message}");

    public static void ValidateFields(CourseUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw Invalid("name", "课程名称不能为空。");
        if (request.Name.Trim().Length > 120)
            throw Invalid("name", "课程名称不能超过120个字符。");

        // The frozen DTO allows an empty string, but not JSON null, for these fields.
        if (request.Teacher is null || request.Teacher.Trim().Length > 100)
            throw Invalid("teacher", "任课教师不能为空值且不能超过100个字符；不填写请传空字符串。");
        if (request.Location is null || request.Location.Trim().Length > 150)
            throw Invalid("location", "上课地点不能为空值且不能超过150个字符；不填写请传空字符串。");

        if ((int)request.DayOfWeek is < 0 or > 6)
            throw Invalid("dayOfWeek", "星期应为周日0至周六6。");
        if (request.StartSection is < 1 or > 20)
            throw Invalid("startSection", "开始节次应在1至20之间。");
        if (request.EndSection is < 1 or > 20 || request.EndSection < request.StartSection)
            throw Invalid("endSection", "结束节次应在开始节次之后，且不能超过20。");

        if (request.WeekNumbers is null || request.WeekNumbers.Count == 0)
            throw Invalid("weekNumbers", "至少选择一个上课周次。");
        if (request.WeekNumbers.Count > 30)
            throw Invalid("weekNumbers", "上课周次不能超过30个。");
        if (request.WeekNumbers.Distinct().Count() != request.WeekNumbers.Count)
            throw Invalid("weekNumbers", "上课周次不能重复。");
    }

    public static void ValidateWeeks(CourseUpsertRequest request, int termWeekCount)
    {
        if (request.WeekNumbers.Any(week => week < 1 || week > termWeekCount))
            throw Invalid("weekNumbers", $"上课周次必须在当前学期第1至{termWeekCount}周之间。");
    }
}
