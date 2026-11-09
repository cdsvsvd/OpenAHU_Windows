using OpenAHU.Contracts.Dtos;
using OpenAHU.Contracts.Local;
using OpenAHU.Desktop.Features.Common;

namespace OpenAHU.Desktop.Features.Timetable;

public sealed record DayChoice(DayOfWeek Value, string Name);

public sealed class CourseEditorViewModel : ObservableObject
{
    public static IReadOnlyList<DayChoice> Days { get; } = [
        new(DayOfWeek.Monday, "周一"), new(DayOfWeek.Tuesday, "周二"), new(DayOfWeek.Wednesday, "周三"),
        new(DayOfWeek.Thursday, "周四"), new(DayOfWeek.Friday, "周五"), new(DayOfWeek.Saturday, "周六"), new(DayOfWeek.Sunday, "周日")];
    private string _name = "", _teacher = "", _location = "", _start = "1", _end = "2", _weeks = "";
    private DayOfWeek _day = DayOfWeek.Monday;
    public string? Id { get; }
    public TermDto Term { get; }
    public string Title => Id is null ? "新增本地课程" : "编辑本地课程";
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Teacher { get => _teacher; set => Set(ref _teacher, value); }
    public string Location { get => _location; set => Set(ref _location, value); }
    public string StartSection { get => _start; set => Set(ref _start, value); }
    public string EndSection { get => _end; set => Set(ref _end, value); }
    public string Weeks { get => _weeks; set => Set(ref _weeks, value); }
    public DayOfWeek Day { get => _day; set => Set(ref _day, value); }
    public string WeekHint => $"周次范围 1–{Term.WeekCount}，例如：1-8,10,12（重复周次自动去重）";

    public CourseEditorViewModel(TermDto term, int selectedWeek, CourseDto? course = null)
    {
        Term = term;
        Id = course?.Id;
        Name = course?.Name ?? "";
        Teacher = course?.Teacher ?? "";
        Location = course?.Location ?? "";
        Day = course?.DayOfWeek ?? DayOfWeek.Monday;
        StartSection = (course?.StartSection ?? 1).ToString();
        EndSection = (course?.EndSection ?? 2).ToString();
        Weeks = course is null ? selectedWeek.ToString() : string.Join(",", course.WeekNumbers);
    }

    public bool TryCreateRequest(out CourseUpsertRequest? request, out string error)
    {
        request = null;
        error = "";
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 120) error = "课程名称必填，且不能超过 120 字。";
        else if (Teacher.Trim().Length > 100 || Location.Trim().Length > 150) error = "教师最多 100 字，地点最多 150 字。";
        else if (!Enum.IsDefined(Day)) error = "请选择有效星期。";
        else if (!int.TryParse(StartSection, out var start) || !int.TryParse(EndSection, out var end) || start < 1 || end > 20 || start > end)
            error = "节次必须为 1–20 的整数，结束节次不能小于开始节次。";
        else if (!TryParseWeeks(Weeks, Term.WeekCount, out var weeks)) error = WeekHint;
        else request = new(Name.Trim(), Teacher.Trim(), Location.Trim(), Day, start, end, weeks, Term.Id);
        return request is not null;
    }

    public static bool TryParseWeeks(string text, int maxWeek, out int[] weeks)
    {
        weeks = [];
        if (string.IsNullOrWhiteSpace(text) || text.Length > 300 || maxWeek is < 1 or > 30) return false;
        var result = new SortedSet<int>();
        foreach (var part in text.Replace('，', ',').Split(','))
        {
            var range = part.Trim().Split('-');
            if (range.Length is < 1 or > 2 || !int.TryParse(range[0], out var start)) return false;
            var end = start;
            if (range.Length == 2 && !int.TryParse(range[1], out end)) return false;
            if (start < 1 || end > maxWeek || start > end) return false;
            for (var week = start; week <= end; week++) result.Add(week);
        }
        weeks = result.ToArray();
        return weeks.Length > 0;
    }
}
