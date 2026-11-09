using System.Diagnostics;
using System.IO;
using System.Text.Json;
using OpenAHU.Desktop.Features.Grades;
using OpenAHU.Desktop.Features.Services;
using OpenAHU.Desktop.Features.Timetable;

namespace OpenAHU.Desktop.Features.Verification;

internal static class IntegrationChecks
{
    public static async Task RunAsync(string apiDll, string dotnetExe, string output)
    {
        apiDll = Path.GetFullPath(apiDll);
        dotnetExe = Path.GetFullPath(dotnetExe);
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        // Unique DB/session locations prevent touching any existing user data. Only the API opens the DB.
        var sandbox = Path.Combine(output, "integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        var session = Path.Combine(sandbox, "session.json");
        Process? process = null;
        async Task<SessionApiClient> StartAsync()
        {
            var info = new ProcessStartInfo(dotnetExe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add(apiDll);
            info.Environment["OpenAHU__DatabasePath"] = Path.Combine(sandbox, "campus.db");
            info.Environment["OpenAHU__SessionPath"] = session;
            process = Process.Start(info) ?? throw new Exception("API failed to start");
            // Consume output asynchronously to avoid blocked pipes. Do not print handshake tokens.
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            for (var attempt = 0; attempt < 150; attempt++)
            {
                if (process.HasExited) throw new Exception("API exited: " + process.ExitCode);
                if (File.Exists(session))
                {
                    using var json = JsonDocument.Parse(await File.ReadAllTextAsync(session));
                    if (json.RootElement.GetProperty("processId").GetInt32() == process.Id) return Program.ReadSession(session);
                }
                await Task.Delay(100);
            }
            throw new TimeoutException("API handshake missing");
        }
        async Task StopAsync()
        {
            if (process is null) return;
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
            process.Dispose();
            process = null;
        }
        try
        {
            string savedId;
            using (var client = await StartAsync())
            {
                BehaviorChecks.Check((await client.GetHealthAsync(default)).DatabaseReady, "real API reports SQLite ready");
                using var timetable = new TimetableViewModel(client);
                await timetable.RefreshAsync();
                BehaviorChecks.Check(timetable.Error == "" && timetable.Terms.Length >= 2, "real SQLite terms load in ViewModel");
                timetable.SelectedWeek = 1;
                timetable.BeginNew();
                timetable.Editor!.Name = "虚构UI持久化验收课程";
                timetable.Editor.Day = DayOfWeek.Sunday;
                timetable.Editor.StartSection = "19";
                timetable.Editor.EndSection = "20";
                timetable.Editor.Weeks = "1,3";
                await timetable.SaveAsync();
                BehaviorChecks.Check(timetable.Error == "" && timetable.Notice.Contains("保存成功"), "UI ViewModel POST to real SQLite and refresh");
                var saved = timetable.Items.Single(c => c.Name == "虚构UI持久化验收课程");
                savedId = saved.Data.Id;
                timetable.SelectedCourse = saved;
                timetable.BeginEdit();
                timetable.Editor!.Name = "虚构UI编辑后课程";
                timetable.Editor.Weeks = "1-3";
                await timetable.SaveAsync();
                BehaviorChecks.Check(timetable.Error == "" && timetable.Items.Single(c => c.Data.Id == savedId).Data.WeekNumbers.SequenceEqual([1, 2, 3]), "UI ViewModel PUT updates course weeks transactionally; " + timetable.Error);
            }
            await StopAsync();
            using (var client = await StartAsync())
            {
                using var timetable = new TimetableViewModel(client);
                await timetable.RefreshAsync();
                timetable.ShowAllWeeks = true;
                BehaviorChecks.Check(timetable.Items.Any(c => c.Data.Id == savedId && c.Name == "虚构UI编辑后课程" && c.Data.WeekNumbers.SequenceEqual([1, 2, 3])), "restart reloads persisted edited course through new session");
                using var grades = new GradesViewModel(client);
                await grades.RefreshAsync();
                BehaviorChecks.Check(grades.Error == "" && grades.Items.Any(g => g.Score is null), "real nullable grade survives HTTP deserialization");
                await Program.RenderAsync(client, Path.Combine(output, "real-api-pages"));
            }
            Console.WriteLine("PASS: API -> Application -> SQLite -> HTTP -> WPF ViewModel create/edit/restart/render");
        }
        finally { await StopAsync(); }
    }
}
