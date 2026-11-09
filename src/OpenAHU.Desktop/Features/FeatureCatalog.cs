using System.Windows.Controls;
using OpenAHU.Desktop.Features.Common;
using OpenAHU.Desktop.Features.Exams;
using OpenAHU.Desktop.Features.Grades;
using OpenAHU.Desktop.Features.Home;
using OpenAHU.Desktop.Features.Links;
using OpenAHU.Desktop.Features.Services;
using OpenAHU.Desktop.Features.Settings;
using OpenAHU.Desktop.Features.Timetable;

namespace OpenAHU.Desktop.Features;

public sealed record FeaturePage(string Key, string Title, PageViewModel ViewModel, UserControl View) : IDisposable
{
    public Task ActivateAsync() => ViewModel.RefreshAsync();
    public void Dispose() => ViewModel.Dispose();
}

/// <summary>Member01 registers these pages in Shell navigation; this assembly owns no main window.</summary>
public static class FeatureCatalog
{
    public static IReadOnlyList<FeaturePage> Create(IFeatureApi api) => [
        Bind("home", "首页", new HomeViewModel(api), new HomeView()),
        Bind("timetable", "课表", new TimetableViewModel(api), new TimetableView()),
        Bind("grades", "成绩", new GradesViewModel(api), new GradesView()),
        Bind("exams", "考试", new ExamsViewModel(api), new ExamsView()),
        Bind("links", "链接", new LinksViewModel(api), new LinksView()),
        Bind("settings", "设置", new SettingsViewModel(api), new SettingsView())];

    private static FeaturePage Bind(string key, string title, PageViewModel vm, UserControl view)
    {
        view.DataContext = vm;
        return new(key, title, vm, view);
    }
}
