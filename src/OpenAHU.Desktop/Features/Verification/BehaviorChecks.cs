using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using OpenAHU.Contracts.Dtos;
using OpenAHU.Contracts.Errors;
using OpenAHU.Contracts.Local;
using OpenAHU.Desktop.Features.Exams;
using OpenAHU.Desktop.Features.Grades;
using OpenAHU.Desktop.Features.Home;
using OpenAHU.Desktop.Features.Services;
using OpenAHU.Desktop.Features.Settings;
using OpenAHU.Desktop.Features.Timetable;

namespace OpenAHU.Desktop.Features.Verification;

internal static class BehaviorChecks
{
    private static int _count;
    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        _count++;
        Console.WriteLine("PASS: " + message);
    }
    public static async Task RunAsync()
    {
        Check(CourseEditorViewModel.TryParseWeeks("1-3，3,5", 16, out var weeks) && weeks.SequenceEqual([1, 2, 3, 5]), "week ranges normalize and deduplicate");
        Check(new[] { "", "0", "17", "3-1", "1,", "1-9999999", "1--2", "a" }.All(s => !CourseEditorViewModel.TryParseWeeks(s, 16, out _)), "invalid/bounded week input rejected");
        var api = new FakeApi();
        var editor = new CourseEditorViewModel(api.Terms[0], 1);
        Check(!editor.TryCreateRequest(out _, out _), "course name required");
        editor.Name = "校验课程";
        editor.StartSection = "abc";
        Check(!editor.TryCreateRequest(out _, out _), "nonnumeric section retained and rejected");
        editor.StartSection = "3";
        Check(!editor.TryCreateRequest(out _, out _), "end section before start rejected");
        editor.EndSection = "20";
        Check(editor.TryCreateRequest(out var payload, out _) && payload!.Teacher == "", "optional teacher/location accepted");

        using var timetable = new TimetableViewModel(api);
        await timetable.RefreshAsync();
        timetable.SelectedWeek = 1;
        Check(timetable.Items.Length == 2 && timetable.Items[0].Data.DayOfWeek == DayOfWeek.Monday && timetable.Items[1].Data.DayOfWeek == DayOfWeek.Sunday, "week filtering and Monday-to-Sunday ordering");
        timetable.SelectedWeek = 2;
        Check(timetable.Items.Length == 0 && timetable.Notice.Contains("本周暂无"), "empty week has explicit status");
        timetable.ShowAllWeeks = true;
        Check(timetable.Items.Length == 2, "all-weeks mode exposes all course rows");
        timetable.SelectedCourse = timetable.Items[0];
        timetable.BeginEdit();
        Check(timetable.Editor is null && !timetable.EditCommand.CanExecute(null), "seeded courses are view-only; local editor is disabled");
        timetable.BeginNew();
        timetable.Editor!.Name = "测试新增课程";
        api.WriteFailure = new FeatureApiException(HttpStatusCode.Conflict, new ApiError("conflict", "课程时间冲突", "trace-demo"));
        await timetable.SaveAsync();
        Check(timetable.Editor is not null && timetable.Error.Contains("课程时间冲突") && timetable.Error.Contains("trace-demo"), "409 preserves editor and displays API error trace");
        api.WriteFailure = null;
        await timetable.SaveAsync();
        Check(api.CreateCalls == 2 && timetable.Editor is null && timetable.Items.Any(c => c.Name == "测试新增课程") && timetable.Notice.Contains("保存成功"), "successful POST refreshes API-backed list");
        timetable.SelectedCourse = timetable.Items.Single(c => c.Name == "测试新增课程");
        timetable.BeginEdit();
        timetable.Editor!.Name = "测试已编辑课程";
        await timetable.SaveAsync();
        Check(api.UpdateCalls == 1 && timetable.Items.Any(c => c.Name == "测试已编辑课程"), "edit uses PUT and refreshes authoritative response");

        timetable.BeginNew();
        timetable.Editor!.Name = "写入成功刷新失败";
        api.ReadFailure = new HttpRequestException();
        await timetable.SaveAsync();
        Check(timetable.Editor is null && timetable.Notice.Contains("保存成功") && timetable.Error.Contains("勿重复新增"), "committed write and refresh failure distinguished");
        api.ReadFailure = null;
        timetable.BeginNew();
        timetable.Editor!.Name = "结果不确定";
        api.WriteFailure = new HttpRequestException();
        await timetable.SaveAsync();
        var writeCalls = api.CreateCalls;
        await timetable.SaveAsync();
        Check(timetable.Editor is null && timetable.Error.Contains("尚未确认") && api.CreateCalls == writeCalls, "ambiguous write prevents accidental repeated POST");

        using var grades = new GradesViewModel(api);
        await grades.RefreshAsync();
        Check(grades.Items.Single().Score is null && grades.Items.Single().GradePoint is null, "null grades remain null (no GPA computation)");
        // Term switching must not reload the unchanged list of terms.
        api.Terms = [api.Terms[0], new TermDto("second-term", "第二虚构学期", DateOnly.FromDateTime(DateTime.Today), 16, false)];
        await grades.RefreshAsync();
        var termRequests = api.TermRequests;
        grades.SelectedTerm = grades.Terms[1];
        await Task.Yield();
        Check(api.TermRequests == termRequests && grades.SelectedTerm?.Id == "second-term",
            "term selection fetches only selected term rows, not the term list");
        await grades.RefreshAsync();
        Check(api.TermRequests == termRequests + 1,
            "manual refresh re-queries the term list");
        api.GradesLoader = (_, _) => Task.FromResult(Array.Empty<GradeDto>());
        await grades.RefreshAsync();
        Check(grades.Notice.Contains("暂无成绩"), "empty grades status");
        using var exams = new ExamsViewModel(api);
        await exams.RefreshAsync();
        Check(exams.Items.Last().StartText == "时间未公布" && exams.Items.Last().LocationText == "地点未公布", "unannounced exams sort last and show missing fields");
        using var home = new HomeViewModel(api);
        await home.RefreshAsync();
        Check(home.Profile!.IsDemo && home.UpdatedAtText.Contains("尚未提供"), "home uses demo profile and does not invent update time");
        using var settings = new SettingsViewModel(api);
        api.Ready = false;
        await settings.RefreshAsync();
        Check(settings.HealthText.Contains("数据库未就绪"), "health degraded state displayed");

        var old = new TaskCompletionSource<GradeDto[]>();
        api.GradesLoader = (_, _) => old.Task;
        var first = grades.RefreshAsync();
        api.GradesLoader = (id, _) => Task.FromResult(new[] { new GradeDto("new", "新结果", 2, 80, null, id) });
        await grades.RefreshAsync();
        old.SetResult([new("stale", "过期结果", 2, 0, null, api.Terms[0].Id)]);
        await first;
        Check(grades.Items.Single().CourseId == "new", "late obsolete response cannot overwrite latest selection");
        api.Terms = [];
        await grades.RefreshAsync();
        Check(grades.SelectedTerm is null && grades.Items.Length == 0 && grades.Notice.Contains("暂无学期"), "empty term collection clears old records");

        await CheckHttpAsync();
        Console.WriteLine($"PASS: {_count} behavior assertions");
    }

    private static async Task CheckHttpAsync()
    {
        foreach (var address in new[] { "https://127.0.0.1:1234", "http://localhost:1234", "http://example.com", "http://127.0.0.1:1234/path", "http://127.0.0.1:1234/?x=1" })
        {
            var rejected = false;
            try { using var ignored = new SessionApiClient(new(address), "test-token"); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "non-handshake base address rejected: " + address);
        }
        var handler = new RecordingHandler();
        using var client = new SessionApiClient(new("http://127.0.0.1:1234"), "verification-token", handler);
        handler.Response = () => new(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<CourseDto>()) };
        await client.GetCoursesAsync("term & 汉字", default);
        Check(handler.Token == "verification-token" && handler.Uri!.Query.Contains("%26") && handler.Uri.Host == "127.0.0.1", "HTTP request contains session header and escaped term ID");
        handler.Response = () => new(HttpStatusCode.Unauthorized) { Content = JsonContent.Create(new ApiError("unauthorized", "失效", "test-trace")) };
        using var home = new HomeViewModel(client);
        await home.RefreshAsync();
        Check(home.Error.Contains("会话已失效"), "401 maps to reconnect message");
        handler.Response = () => new(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("not-json") };
        await home.RefreshAsync();
        Check(home.Error.StartsWith("本地服务暂不可用"), "malformed 503 uses safe local-service error");
        handler.Response = () => new(HttpStatusCode.Redirect) { Content = new StringContent("") };
        var before = handler.Calls;
        try { await client.GetTermsAsync(default); } catch (FeatureApiException) { }
        Check(handler.Calls == before + 1, "redirect status is rejected instead of retried");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Func<HttpResponseMessage> Response { get; set; } = () => new(HttpStatusCode.OK);
        public Uri? Uri { get; private set; }
        public string? Token { get; private set; }
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Uri = request.RequestUri;
            Token = request.Headers.GetValues("X-OpenAHU-Session").Single();
            return Task.FromResult(Response());
        }
    }
}

// Fixtures are compiled only into the verification executable, never the shipping feature library.
internal sealed class FakeApi : IFeatureApi
{
    public TermDto[] Terms = [new("test-term", "虚构验收学期", DateOnly.FromDateTime(DateTime.Today), 16, true)];
    private readonly List<CourseDto> _courses = [
        new("sun", "虚构周日课程", "演示教师", "演示教室", DayOfWeek.Sunday, 1, 2, [1, 3], "test-term"),
        new("mon", "虚构周一课程", "", "", DayOfWeek.Monday, 3, 4, [1, 3], "test-term")];
    public Exception? ReadFailure;
    public Exception? WriteFailure;
    public int CreateCalls;
    public int UpdateCalls;
    public bool Ready = true;
    public int TermRequests;
    public Func<string, CancellationToken, Task<GradeDto[]>> GradesLoader = (id, _) => Task.FromResult(new[] { new GradeDto("g", "虚构未公布成绩", 2, null, null, id) });
    public Task<StudentProfileDto> GetProfileAsync(CancellationToken ct) => Task.FromResult(new StudentProfileDto("demo-fixture", "虚构验收学生", "演示学院", "演示专业", 2026, true));
    public Task<DataStatusDto> GetDataStatusAsync(CancellationToken ct) => Task.FromResult(new DataStatusDto("LocalSQLite", true, null, "本地演示数据，非学校官方信息"));
    public Task<HealthDto> GetHealthAsync(CancellationToken ct) => Task.FromResult(new HealthDto(Ready ? "ok" : "degraded", Ready, "verification"));
    public Task<TermDto[]> GetTermsAsync(CancellationToken ct) { TermRequests++; return Task.FromResult(Terms); }
    public Task<CourseDto[]> GetCoursesAsync(string termId, CancellationToken ct) => ReadFailure is null ? Task.FromResult(_courses.Where(c => c.TermId == termId).ToArray()) : Task.FromException<CourseDto[]>(ReadFailure);
    public Task<GradeDto[]> GetGradesAsync(string termId, CancellationToken ct) => GradesLoader(termId, ct);
    public Task<ExamDto[]> GetExamsAsync(string termId, CancellationToken ct) => Task.FromResult(new[] {
        new ExamDto("unknown", "虚构待公布考试", null, null, null, termId),
        new ExamDto("known", "虚构已安排考试", DateTimeOffset.Now.AddDays(2), DateTimeOffset.Now.AddDays(2).AddHours(2), "演示教室", termId) });
    public Task<LinkDto[]> GetLinksAsync(CancellationToken ct) => Task.FromResult(new[] { new LinkDto("project", "项目仓库", "https://github.com/cdsvsvd/-Windows-", "项目") });
    public Task<CourseDto> CreateCourseAsync(CourseUpsertRequest r, CancellationToken ct) { CreateCalls++; return Write("local-" + Guid.NewGuid().ToString("N"), r); }
    public Task<CourseDto> UpdateCourseAsync(string id, CourseUpsertRequest r, CancellationToken ct) { UpdateCalls++; return Write(id, r); }
    private Task<CourseDto> Write(string id, CourseUpsertRequest r)
    {
        if (WriteFailure is not null) return Task.FromException<CourseDto>(WriteFailure);
        var value = new CourseDto(id, r.Name, r.Teacher, r.Location, r.DayOfWeek, r.StartSection, r.EndSection, r.WeekNumbers, r.TermId);
        _courses.RemoveAll(c => c.Id == id);
        _courses.Add(value);
        return Task.FromResult(value);
    }
}
