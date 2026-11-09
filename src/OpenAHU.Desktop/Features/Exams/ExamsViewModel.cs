using OpenAHU.Contracts.Dtos;
using OpenAHU.Desktop.Features.Common;
using OpenAHU.Desktop.Features.Services;

namespace OpenAHU.Desktop.Features.Exams;

public sealed record ExamRow(ExamDto Data)
{
    public string CourseName => Data.CourseName;
    public string StartText => Data.StartsAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm zzz") ?? "时间未公布";
    public string EndText => Data.EndsAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm zzz") ?? "结束时间未公布";
    public string LocationText => string.IsNullOrWhiteSpace(Data.Location) ? "地点未公布" : Data.Location;
}

public sealed class ExamsViewModel(IFeatureApi api) : TermPageViewModel(api)
{
    private ExamRow[] _items = [];
    public ExamRow[] Items { get => _items; private set => Set(ref _items, value); }
    protected override void ClearRows() => Items = [];
    protected override async Task LoadTermAsync(TermDto term, CancellationToken ct)
    {
        var items = await Api.GetExamsAsync(term.Id, ct);
        ct.ThrowIfCancellationRequested();
        Items = items.OrderBy(e => e.StartsAt is null).ThenBy(e => e.StartsAt).Select(e => new ExamRow(e)).ToArray();
        if (items.Length == 0) Notice = "本学期暂无考试安排。";
    }
}
