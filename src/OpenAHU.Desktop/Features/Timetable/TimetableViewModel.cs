using System.Windows.Input;
using OpenAHU.Contracts.Dtos;
using OpenAHU.Desktop.Features.Common;
using OpenAHU.Desktop.Features.Services;

namespace OpenAHU.Desktop.Features.Timetable;

public sealed record CourseRow(CourseDto Data)
{
    public string Name => Data.Name;
    public string Teacher => string.IsNullOrWhiteSpace(Data.Teacher) ? "未填写" : Data.Teacher;
    public string Location => string.IsNullOrWhiteSpace(Data.Location) ? "地点未公布" : Data.Location;
    public string DayText => CourseEditorViewModel.Days.First(d => d.Value == Data.DayOfWeek).Name;
    public string Sections => $"{Data.StartSection}–{Data.EndSection} 节";
    public string Weeks => string.Join(", ", Data.WeekNumbers);
}

public sealed class TimetableViewModel : TermPageViewModel
{
    private CourseDto[] _courses = [];
    private CourseRow[] _items = [];
    private CourseRow? _selectedCourse;
    private CourseEditorViewModel? _editor;
    private int _week = 1;
    private bool _showAll;
    private bool _hasLoaded;
    public TimetableViewModel(IFeatureApi api) : base(api)
    {
        NewCommand = new(BeginNew, () => IsIdle && SelectedTerm is not null && Editor is null);
        EditCommand = new(BeginEdit, () => IsIdle && SelectedCourse is not null &&
            SelectedCourse.Data.Id.StartsWith("local-", StringComparison.Ordinal) && Editor is null);
        CancelEditCommand = new(() => { Editor = null; Error = ""; }, () => IsIdle && Editor is not null);
        SaveCommand = new(SaveAsync, ShowError, () => IsIdle && Editor is not null);
    }
    public CourseRow[] Items { get => _items; private set => Set(ref _items, value); }
    public CourseRow? SelectedCourse { get => _selectedCourse; set { Set(ref _selectedCourse, value); CommandManager.InvalidateRequerySuggested(); } }
    public CourseEditorViewModel? Editor
    {
        get => _editor;
        private set { Set(ref _editor, value); Changed(nameof(IsEditing)); Changed(nameof(CanChangeFilters)); CommandManager.InvalidateRequerySuggested(); }
    }
    public bool IsEditing => Editor is not null;
    public bool CanChangeFilters => Editor is null;
    public int[] Weeks => SelectedTerm is null ? [] : Enumerable.Range(1, SelectedTerm.WeekCount).ToArray();
    public int SelectedWeek
    {
        get => _week;
        set { if (value >= 1 && value <= (SelectedTerm?.WeekCount ?? 1) && Set(ref _week, value)) Filter(); }
    }
    public bool ShowAllWeeks { get => _showAll; set { if (Set(ref _showAll, value)) Filter(); } }
    public ActionCommand NewCommand { get; }
    public ActionCommand EditCommand { get; }
    public ActionCommand CancelEditCommand { get; }
    public AsyncCommand SaveCommand { get; }

    protected override void TermChanged()
    {
        Editor = null;
        _week = SelectedTerm is null ? 1 : Math.Clamp((int)Math.Floor((DateOnly.FromDateTime(DateTime.Today).DayNumber - SelectedTerm.StartDate.DayNumber) / 7.0) + 1, 1, SelectedTerm.WeekCount);
        Changed(nameof(Weeks));
        Changed(nameof(SelectedWeek));
    }
    protected override void ClearRows() { _hasLoaded = false; _courses = []; Items = []; SelectedCourse = null; }
    protected override async Task LoadTermAsync(TermDto term, CancellationToken ct)
    {
        var items = await Api.GetCoursesAsync(term.Id, ct);
        ct.ThrowIfCancellationRequested();
        _courses = items;
        _hasLoaded = true;
        Changed(nameof(Weeks));
        if (_week > term.WeekCount) SelectedWeek = term.WeekCount;
        Filter();
    }
    private void Filter()
    {
        Items = _courses.Where(c => ShowAllWeeks || c.WeekNumbers.Contains(SelectedWeek))
            .OrderBy(c => ((int)c.DayOfWeek + 6) % 7).ThenBy(c => c.StartSection).ThenBy(c => c.Name)
            .Select(c => new CourseRow(c)).ToArray();
        SelectedCourse = null;
        Notice = _hasLoaded && Items.Length == 0 ? (ShowAllWeeks ? "本学期暂无课程，可新增本地课程。" : "本周暂无课程，可切换周次或查看全部课程。") : "";
    }
    public void BeginNew()
    {
        if (IsBusy || SelectedTerm is null || Editor is not null) return;
        Error = "";
        Notice = "";
        Editor = new(SelectedTerm, SelectedWeek);
    }
    public void BeginEdit()
    {
        if (IsBusy || SelectedTerm is null || SelectedCourse is null || Editor is not null ||
            !SelectedCourse.Data.Id.StartsWith("local-", StringComparison.Ordinal)) return;
        Error = "";
        Notice = "";
        Editor = new(SelectedTerm, SelectedWeek, SelectedCourse.Data);
    }
    public Task SaveAsync()
    {
        if (IsBusy || Editor is null) return Task.CompletedTask;
        var editor = Editor;
        if (!editor.TryCreateRequest(out var request, out var validation)) { Error = validation; return Task.CompletedTask; }
        return RunAsync(async ct =>
        {
            CourseDto saved;
            try
            {
                saved = editor.Id is null ? await Api.CreateCourseAsync(request!, ct) : await Api.UpdateCourseAsync(editor.Id, request!, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                Error = ErrorText(ex);
                if (ex is not FeatureApiException)
                {
                    // A timeout can occur after commit. Avoid accidental duplicate POST on another click.
                    Editor = null;
                    Error += " 保存结果尚未确认，请先刷新课程列表核对，再决定是否重新编辑。";
                }
                return;
            }
            ct.ThrowIfCancellationRequested();
            Editor = null; // confirmed write must never be submitted twice if the follow-up GET fails
            try
            {
                await LoadTermAsync(editor.Term, ct);
                SelectedWeek = saved.WeekNumbers.FirstOrDefault(1);
                SelectedCourse = Items.FirstOrDefault(c => c.Data.Id == saved.Id);
                Notice = "保存成功，已从本地服务重新加载课程。";
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                ClearRows();
                Notice = "课程保存成功。";
                Error = ErrorText(ex) + " 列表刷新失败，请点击刷新，勿重复新增。";
            }
        });
    }
}
