using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenAHU.Application.Errors;
using OpenAHU.Infrastructure;
using OpenAHU.Infrastructure.Persistence;
using OpenAHU.Infrastructure.Tools;

Console.OutputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

var pathIndex = Array.IndexOf(args, "--database-path");
if (pathIndex < 0 || pathIndex + 1 >= args.Length)
{
    Console.Error.WriteLine("请显式指定数据库：--database-path <专用目录内的绝对文件路径> [--check-only]");
    return 1;
}

try
{
    if (args.Contains("--check-only") && args.Contains("--course-action"))
        throw new ArgumentException("只读探针不能同时执行课程操作。");
    var registrations = new ServiceCollection().AddInfrastructure(new CampusDatabaseOptions
    {
        DatabasePath = args[pathIndex + 1]
    });
    CourseTool.RegisterApplication(registrations);
    await using var services = registrations.BuildServiceProvider(validateScopes: true);
    var database = services.GetRequiredService<CampusDatabase>();
    var result = args.Contains("--check-only") ? await database.CheckAsync() : await database.InitializeAsync();
    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters = { new JsonStringEnumConverter() }
    };
    Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
    if (!result.IsReady) return 2;

    await using var db = await services.GetRequiredService<IDbContextFactory<CampusDbContext>>().CreateDbContextAsync();
    var counts = new Dictionary<string, int>
    {
        ["students"] = await db.Students.CountAsync(), ["terms"] = await db.Terms.CountAsync(),
        ["courses"] = await db.Courses.CountAsync(), ["course_weeks"] = await db.CourseWeeks.CountAsync(),
        ["enrollments"] = await db.Enrollments.CountAsync(), ["grades"] = await db.Grades.CountAsync(),
        ["exams"] = await db.Exams.CountAsync(), ["campus_links"] = await db.CampusLinks.CountAsync(),
        ["app_settings"] = await db.AppSettings.CountAsync()
    };
    Console.WriteLine(JsonSerializer.Serialize(counts, jsonOptions));
    if (args.Contains("--course-action"))
    {
        var outcome = await CourseTool.ExecuteAsync(args, services);
        Console.WriteLine(JsonSerializer.Serialize(outcome, jsonOptions));
        return outcome.Success ? 0 : 3;
    }
    return 0;
}
catch (ArgumentException)
{
    Console.Error.WriteLine("参数不合法。请提供绝对 --database-path；课程操作使用 --course-action create|read|update|delete，读改删需 --course-id，改名需 --course-name。");
    return 1;
}
catch (CampusStoreUnavailableException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
catch (CampusResourceNotFoundException)
{
    Console.Error.WriteLine("所选学期或课程不存在。");
    return 3;
}
