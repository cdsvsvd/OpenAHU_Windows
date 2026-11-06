using Microsoft.Extensions.DependencyInjection;
using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Abstractions.Commands;
using OpenAHU.Application.Models;
using OpenAHU.Application.Services;

namespace OpenAHU.Infrastructure.Tools;

// A development-only console composition root; it does not register production HTTP endpoints.
internal static class CourseTool
{
    public static void RegisterApplication(IServiceCollection services)
    {
        services.AddScoped<ActiveStudentProvider>();
        services.AddScoped<ICourseCommand, CourseCommand>();
    }

    public static async Task<CourseToolOutcome> ExecuteAsync(string[] args, IServiceProvider services)
    {
        var action = GetArgument(args, "--course-action") ?? throw new ArgumentException("缺少操作名称。");
        await using var scope = services.CreateAsyncScope();
        var command = scope.ServiceProvider.GetRequiredService<ICourseCommand>();
        var courses = scope.ServiceProvider.GetRequiredService<ICourseRepository>();
        var id = GetArgument(args, "--course-id");
        var name = GetArgument(args, "--course-name");

        if (action == "create")
        {
            // Fictional development fixture, validated by the actual Application service.
            var draft = new CourseDraft(name ?? "成员05跨进程演示课程", "示例教师", "演示教室",
                DayOfWeek.Sunday, 19, 20, new[] { 1, 3 }, "term-2026-fall");
            var created = await command.CreateAsync(draft, CancellationToken.None);
            return FromResult(action, created);
        }
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("缺少课程id。");
        if (action == "delete") return FromResult(action, await command.DeleteAsync(id, CancellationToken.None));
        var course = await courses.FindAsync(id, CancellationToken.None);
        if (course is null) return new(action, false, "NotFound", id, "课程不存在。", null, null, null);
        if (action == "read") return new(action, true, "Ok", id, null, null, null, course);
        if (action == "update")
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("缺少新课程名。");
            var draft = new CourseDraft(name, course.Teacher, course.Location, course.DayOfWeek,
                course.StartSection, course.EndSection, course.WeekNumbers, course.TermId);
            return FromResult(action, await command.UpdateAsync(id, draft, CancellationToken.None));
        }
        throw new ArgumentException("未知课程操作。");
    }

    private static CourseToolOutcome FromResult(string action, CourseWriteResult result) => new(
        action, result.Status == CourseWriteStatus.Ok, result.Status.ToString(), result.CourseId,
        result.Message, result.EnrolledStudentCount, result.OtherStudentCount, null);

    private static string? GetArgument(string[] args, string key)
    {
        var index = Array.IndexOf(args, key);
        if (index < 0) return null;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("缺少参数值。");
        return args[index + 1];
    }
}

internal sealed record CourseToolOutcome(string Action, bool Success, string Status, string? CourseId,
    string? Message, int? EnrolledStudentCount, int? OtherStudentCount, Course? Course);
