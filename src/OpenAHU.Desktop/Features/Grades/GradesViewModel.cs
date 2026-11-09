using OpenAHU.Contracts.Dtos;
using OpenAHU.Desktop.Features.Common;
using OpenAHU.Desktop.Features.Services;

namespace OpenAHU.Desktop.Features.Grades;

public sealed class GradesViewModel(IFeatureApi api) : TermPageViewModel(api)
{
    private GradeDto[] _items = [];
    public GradeDto[] Items { get => _items; private set => Set(ref _items, value); }
    protected override void ClearRows() => Items = [];
    protected override async Task LoadTermAsync(TermDto term, CancellationToken ct)
    {
        var items = await Api.GetGradesAsync(term.Id, ct);
        ct.ThrowIfCancellationRequested();
        Items = items;
        if (items.Length == 0) Notice = "本学期暂无成绩。";
    }
}
