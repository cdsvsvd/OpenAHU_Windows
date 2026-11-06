# 03 号接入代码（4 号提供，可直接粘贴）

> 面向 03 号：把 6 个查询占位 + 3 个写占位换成真实实现。
> 全部代码已对齐**你已冻结的契约**（`Contracts/README.md` 的 `201/200 + CourseDto`、`204`）与
> **05 号的 Infrastructure**（`AddInfrastructure` / `CampusDatabase`）。
> 我改的文件都在 03 号目录里，按项目规则我**没有**直接改，你确认后自行落地。

---

## 0. 要动的文件（5 个）

| 文件 | 动作 |
| --- | --- |
| `src/OpenAHU.Api/OpenAHU.Api.csproj` | 加两个 `ProjectReference` |
| `src/OpenAHU.Api/Errors/ApiExceptionHandler.cs` | 整文件替换（加 404 / 503 分支） |
| `src/OpenAHU.Api/Program.cs` | 加 DI 注册 + 数据库初始化 |
| `src/OpenAHU.Api/Endpoints/CampusEndpoints.cs` | 整文件替换（9 个占位改真实实现） |
| `src/OpenAHU.Api/README.md` | 更新端点状态表 |

---

## 1. `OpenAHU.Api.csproj`

```xml
  <ItemGroup>
    <ProjectReference Include="..\OpenAHU.Contracts\OpenAHU.Contracts.csproj" />
    <ProjectReference Include="..\OpenAHU.Application\OpenAHU.Application.csproj" />
    <ProjectReference Include="..\OpenAHU.Infrastructure\OpenAHU.Infrastructure.csproj" />
  </ItemGroup>
```

> Api 引用 Infrastructure 是合规的：`01_架构 §2` 明确「Infrastructure 通过 DI 在 Api Composition Root 注册」。
> 但**只允许 `Program.cs` 用 `AddInfrastructure` 和 `CampusDatabase`**，端点里不许出现 EF 类型。

---

## 2. `Errors/ApiExceptionHandler.cs`（整文件替换）

```csharp
using Microsoft.AspNetCore.Diagnostics;
using OpenAHU.Application.Errors;
using OpenAHU.Contracts.Errors;

namespace OpenAHU.Api.Errors;

/// <summary>
/// 统一异常出口。业务层只抛两种"有意义"的异常，其余一律 500：
/// <list type="bullet">
/// <item><see cref="CampusResourceNotFoundException"/> → 404 <c>not_found</c>（读路径的未知学期/课程）</item>
/// <item><see cref="CampusStoreUnavailableException"/> → 503 <c>db_unavailable</c>（SQLite 不可用）</item>
/// </list>
/// 写链路的 400/404/409 不走异常，由 <c>CourseWriteResult</c> 分类返回，故这里不出现它们。
/// 响应体不含异常消息、堆栈、数据库路径，细节只进日志。
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    /// <summary>创建处理器。</summary>
    /// <param name="logger">日志。</param>
    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>按异常类型映射为 404 / 503 / 500。</summary>
    /// <param name="httpContext">当前请求上下文。</param>
    /// <param name="exception">未处理异常。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>总是 <c>true</c>（已处理）。</returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var traceId = ApiResults.TraceId(httpContext);

        // 1) 资源不存在：这是正常的业务结果，用 Debug 级别，不要污染错误日志。
        if (exception is CampusResourceNotFoundException notFound)
        {
            _logger.LogDebug(
                "资源不存在 {Method} {Path}（{Resource}），traceId={TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                notFound.Resource,
                traceId);

            await ApiResults.WriteAsync(
                httpContext,
                StatusCodes.Status404NotFound,
                ApiErrorCodes.NotFound,
                "请求的学期或课程不存在，请刷新后重试。",
                cancellationToken);
            return true;
        }

        // 2) 数据库不可用：Warning + 保留内部异常供排障；对外只说"未就绪"。
        if (exception is CampusStoreUnavailableException storeUnavailable)
        {
            _logger.LogWarning(
                storeUnavailable,
                "本地数据库不可用 {Method} {Path}，traceId={TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                traceId);

            await ApiResults.WriteAsync(
                httpContext,
                StatusCodes.Status503ServiceUnavailable,
                ApiErrorCodes.DbUnavailable,
                "本地数据服务尚未就绪，请稍后重试。",
                cancellationToken);
            return true;
        }

        // 3) 其余一律 500，响应体只给通用文案。
        _logger.LogError(
            exception,
            "未处理异常 {Method} {Path}，traceId={TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            traceId);

        await ApiResults.WriteAsync(
            httpContext,
            StatusCodes.Status500InternalServerError,
            ApiErrorCodes.Internal,
            "本地服务发生未预期错误，请稍后重试。",
            cancellationToken);
        return true;
    }
}
```

---

## 3. `Program.cs`（整文件替换）

```csharp
using OpenAHU.Api.Endpoints;
using OpenAHU.Api.Errors;
using OpenAHU.Api.Hosting;
using OpenAHU.Application.Abstractions.Commands;
using OpenAHU.Application.Abstractions.Queries;
using OpenAHU.Application.Services;
using OpenAHU.Contracts.Json;
using OpenAHU.Infrastructure;
using OpenAHU.Infrastructure.Persistence;

// 本地伴随进程：只监听回环地址，端口由系统随机分配（0），启动后把真实端口与令牌写入交接文件。
// 边界：此进程不直接访问 SQLite，数据库访问经 04 Application → 05 Infrastructure。
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://127.0.0.1:0");

// 与 Desktop 共用同一份 JSON 约定，避免两侧字段名/大小写漂移。
var contractJson = ContractJson.CreateOptions();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = contractJson.PropertyNamingPolicy;
    options.SerializerOptions.PropertyNameCaseInsensitive = contractJson.PropertyNameCaseInsensitive;
    options.SerializerOptions.NumberHandling = contractJson.NumberHandling;
});

var sessionToken = SessionToken.Create();
builder.Services.AddSingleton(sessionToken);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// 05 号：8 个仓储 + IUnitOfWork，全部 Scoped 且共享同一个 CampusDbContext；配置键 OpenAHU:DatabasePath。
builder.Services.AddInfrastructure(builder.Configuration);

// 04 号：业务用例（Composition Root 归 03 号，故 04 号不提供 AddApplication 扩展方法）。
builder.Services.AddScoped<ActiveStudentProvider>();
builder.Services.AddScoped<IProfileQuery, ProfileQuery>();
builder.Services.AddScoped<ITermQuery, TermQuery>();
builder.Services.AddScoped<ICourseQuery, CourseQuery>();
builder.Services.AddScoped<IGradeQuery, GradeQuery>();
builder.Services.AddScoped<IExamQuery, ExamQuery>();
builder.Services.AddScoped<ILinkQuery, LinkQuery>();
builder.Services.AddScoped<IDataStatusQuery, DataStatusQuery>();
builder.Services.AddScoped<ICourseCommand, CourseCommand>();

var app = builder.Build();

app.UseMiddleware<SessionTokenMiddleware>();
app.UseExceptionHandler();
app.MapCampusEndpoints();

// 先迁移 + 种子，再对外宣布就绪：客户端读到 session.json 时数据库已经是可用状态。
// 失败不退出进程——否则 Desktop 只能看到"后端起不来"，无法区分"数据库坏了"；
// 由 /health 返回 starting/degraded，前端据此提示。
var database = app.Services.GetRequiredService<CampusDatabase>();
var initialization = await database.InitializeAsync(app.Lifetime.ApplicationStopping);
app.Logger.LogInformation(
    "OpenAHU 本地数据库初始化：{State} {Message}",
    initialization.State,
    initialization.Message);

await app.StartAsync();

var handshakePath = SessionHandshake.DefaultPath;
var handshake = SessionHandshake.Create(app.Urls, sessionToken.Value, Environment.ProcessId);
SessionHandshake.Write(handshakePath, handshake);
app.Logger.LogInformation("OpenAHU 本地 API 已启动：{BaseUrl}（交接文件 {Path}）", handshake.BaseUrl, handshakePath);

try
{
    await app.WaitForShutdownAsync();
}
finally
{
    // 正常退出时清除交接文件；崩溃残留由下次启动覆盖，客户端以 pid 判断进程是否存活。
    SessionHandshake.Delete(handshakePath);
}
```

---

## 4. `Endpoints/CampusEndpoints.cs`（整文件替换）

```csharp
using System.Globalization;
using OpenAHU.Api.Errors;
using OpenAHU.Application.Abstractions.Commands;
using OpenAHU.Application.Abstractions.Queries;
using OpenAHU.Application.Models;
using OpenAHU.Contracts.Dtos;
using OpenAHU.Contracts.Errors;
using OpenAHU.Contracts.Local;
using OpenAHU.Infrastructure.Persistence;

namespace OpenAHU.Api.Endpoints;

/// <summary>
/// <c>/api/v1</c> 路由表。11 条路径全部为真实实现：数据来自 04 号业务层，
/// 最终落到 05 号的本地 SQLite。Api 只做「参数校验 + 错误映射 + DTO 形状转换」。
/// </summary>
public static class CampusEndpoints
{
    /// <summary>本程序集版本号，供 <c>/health</c> 排障使用（不含构建路径）。</summary>
    private static readonly string ApiVersion =
        typeof(CampusEndpoints).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    /// <summary>影响面响应头：选课总人数。</summary>
    private const string EnrolledHeader = "X-OpenAHU-Enrolled-Students";

    /// <summary>影响面响应头：除当前演示学生外的选课人数（&gt;0 时前端须警示）。</summary>
    private const string OtherHeader = "X-OpenAHU-Other-Students";

    /// <summary>注册全部 v1 端点。</summary>
    /// <param name="endpoints">端点路由构建器。</param>
    /// <returns>同一个构建器，便于链式调用。</returns>
    public static IEndpointRouteBuilder MapCampusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1");

        // 就绪状态：真实探测数据库，不回显绝对路径。
        group.MapGet("/health", async (CampusDatabase database, HttpContext context) =>
        {
            var check = await database.CheckAsync(context.RequestAborted);
            var status = check.State switch
            {
                DatabaseState.Ready => "ok",
                DatabaseState.IncompatibleSchema or DatabaseState.InvalidData => "degraded",
                _ => "starting",
            };
            return Results.Ok(new HealthDto(status, check.IsReady, ApiVersion));
        });

        // 数据来源声明：UpdatedAt 首版恒为 null（库内无 updated_at 列），不编造时间。
        group.MapGet("/data-status", async (IDataStatusQuery status, HttpContext context) =>
        {
            var updatedAt = await status.GetLastUpdatedAsync(context.RequestAborted);
            return Results.Ok(new DataStatusDto(
                "LocalSQLite", IsDemo: true, updatedAt, "本地演示数据，非学校官方信息"));
        });

        group.MapGet("/profile", async (IProfileQuery query, HttpContext context) =>
        {
            var profile = await query.GetActiveAsync(context.RequestAborted);
            return Results.Ok(new StudentProfileDto(
                profile.Id, profile.DisplayName, profile.College,
                profile.Major, profile.GradeYear, profile.IsDemo));
        });

        group.MapGet("/terms", async (ITermQuery query, HttpContext context) =>
        {
            var terms = await query.GetAllAsync(context.RequestAborted);
            return Results.Ok(terms.Select(item => ToDto(item)).ToArray());
        });

        // 三个按学期过滤的端点：缺参/空白 termId 归 Api（400）；未知 termId 归 Application（404）。
        group.MapGet("/courses", async (HttpContext context, ICourseQuery query) =>
        {
            IResult result;
            if (TryReadTermId(context, out var termId))
            {
                var courses = await query.GetByTermAsync(termId, context.RequestAborted);
                result = Results.Ok(courses.Select(item => ToDto(item)).ToArray());
            }
            else
            {
                result = InvalidTermId(context);
            }

            return result;
        });

        group.MapGet("/grades", async (HttpContext context, IGradeQuery query) =>
        {
            IResult result;
            if (TryReadTermId(context, out var termId))
            {
                var grades = await query.GetByTermAsync(termId, context.RequestAborted);
                result = Results.Ok(grades.Select(item => ToDto(item)).ToArray());
            }
            else
            {
                result = InvalidTermId(context);
            }

            return result;
        });

        group.MapGet("/exams", async (HttpContext context, IExamQuery query) =>
        {
            IResult result;
            if (TryReadTermId(context, out var termId))
            {
                var exams = await query.GetByTermAsync(termId, context.RequestAborted);
                result = Results.Ok(exams.Select(item => ToDto(item)).ToArray());
            }
            else
            {
                result = InvalidTermId(context);
            }

            return result;
        });

        group.MapGet("/links", async (ILinkQuery query, HttpContext context) =>
        {
            var links = await query.GetAllAsync(context.RequestAborted);
            return Results.Ok(links.Select(item => ToDto(item)).ToArray());
        });

        // 写链路：成功 201/200 + CourseDto、删除 204；失败由 CourseWriteResult 分类映射。
        group.MapPost("/local/courses", async (CourseUpsertRequest request, HttpContext context, ICourseCommand command) =>
        {
            var result = await command.CreateAsync(ToDraft(request), context.RequestAborted);
            return ToWriteResult(result, context, created: true);
        });

        group.MapPut("/local/courses/{id}", async (string id, CourseUpsertRequest request, HttpContext context, ICourseCommand command) =>
        {
            var result = await command.UpdateAsync(id, ToDraft(request), context.RequestAborted);
            return ToWriteResult(result, context, created: false);
        });

        group.MapDelete("/local/courses/{id}", async (string id, HttpContext context, ICourseCommand command) =>
        {
            var result = await command.DeleteAsync(id, context.RequestAborted);
            return ToWriteResult(result, context, created: false);
        });

        return endpoints;
    }

    /// <summary>读取 <c>termId</c> 查询参数并判断是否非空。</summary>
    /// <param name="context">当前请求。</param>
    /// <param name="termId">解析出的学期 id（可能为空串）。</param>
    /// <returns>非空返回 <c>true</c>。</returns>
    private static bool TryReadTermId(HttpContext context, out string termId)
    {
        termId = context.Request.Query["termId"].ToString();
        return !string.IsNullOrWhiteSpace(termId);
    }

    /// <summary>缺参或空白 <c>termId</c> 的标准 400 响应。</summary>
    /// <param name="context">当前请求。</param>
    /// <returns>400 + <c>invalid_request</c>。</returns>
    private static IResult InvalidTermId(HttpContext context) => ApiResults.Error(
        StatusCodes.Status400BadRequest,
        ApiErrorCodes.InvalidRequest,
        "termId 不能为空，请先选择学期。",
        context);

    /// <summary>把写结果映射为 HTTP 响应。</summary>
    /// <param name="result">业务层返回的分类结果。</param>
    /// <param name="context">当前请求。</param>
    /// <param name="created">是否为新增（决定 201 还是 200）。</param>
    /// <returns>对应 HTTP 结果。</returns>
    private static IResult ToWriteResult(CourseWriteResult result, HttpContext context, bool created)
    {
        switch (result.Status)
        {
            case CourseWriteStatus.Ok when result.Course is not null:
            {
                WriteImpactHeaders(context, result);
                var dto = ToDto(result.Course);
                return created
                    ? Results.Created($"/api/v1/local/courses/{dto.Id}", dto)
                    : Results.Ok(dto);
            }

            case CourseWriteStatus.Ok:
                // 删除：204 无响应体，影响面只通过响应头告知。
                WriteImpactHeaders(context, result);
                return Results.NoContent();

            case CourseWriteStatus.ValidationFailed:
                return ApiResults.Error(
                    StatusCodes.Status400BadRequest,
                    ApiErrorCodes.InvalidRequest,
                    result.Message ?? "请求参数不合法。",
                    context);

            case CourseWriteStatus.UnknownTerm:
            case CourseWriteStatus.NotFound:
                return ApiResults.Error(
                    StatusCodes.Status404NotFound,
                    ApiErrorCodes.NotFound,
                    result.Message ?? "资源不存在。",
                    context);

            case CourseWriteStatus.Conflict:
                return ApiResults.Error(
                    StatusCodes.Status409Conflict,
                    ApiErrorCodes.Conflict,
                    result.Message ?? "关联冲突，操作被拒绝。",
                    context);

            default:
                return ApiResults.Error(
                    StatusCodes.Status500InternalServerError,
                    ApiErrorCodes.Internal,
                    "本地服务发生未预期错误，请稍后重试。",
                    context);
        }
    }

    /// <summary>写入影响面响应头，供 2 号提示“修改会影响其他演示学生”。</summary>
    /// <param name="context">当前请求。</param>
    /// <param name="result">写结果。</param>
    private static void WriteImpactHeaders(HttpContext context, CourseWriteResult result)
    {
        context.Response.Headers[EnrolledHeader] = result.EnrolledStudentCount.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers[OtherHeader] = result.OtherStudentCount.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>请求体 → 业务层草稿。没有 Id：新增由后端生成，编辑以路径参数为准。</summary>
    /// <param name="request">已反序列化的请求体。</param>
    /// <returns>业务层草稿。</returns>
    private static CourseDraft ToDraft(CourseUpsertRequest request) => new(
        request.Name, request.Teacher, request.Location, request.DayOfWeek,
        request.StartSection, request.EndSection, request.WeekNumbers, request.TermId);

    private static TermDto ToDto(Term term) =>
        new(term.Id, term.Name, term.StartDate, term.WeekCount, term.IsCurrent);

    private static CourseDto ToDto(Course course) => new(
        course.Id, course.Name, course.Teacher, course.Location, course.DayOfWeek,
        course.StartSection, course.EndSection, course.WeekNumbers, course.TermId);

    private static GradeDto ToDto(Grade grade) => new(
        grade.CourseId, grade.CourseName, grade.Credits, grade.Score, grade.GradePoint, grade.TermId);

    private static ExamDto ToDto(Exam exam) => new(
        exam.Id, exam.CourseName, exam.StartsAt, exam.EndsAt, exam.Location, exam.TermId);

    private static LinkDto ToDto(CampusLink link) =>
        new(link.Id, link.Title, link.Url, link.Category);
}
```

---

## 5. 三处必须注意的坑（写的时候我踩过）

1. **三个 `termId` 端点刻意写成“先算出 `IResult`、最后只 return 一次”**，不是啰嗦：
   handler 里出现多个 `return` 且类型不一致（`IResult` 与 `Ok<T>`）时，lambda 的自然类型推断容易出岔子。
   你若想改成提前 return 的写法，编译不过就改回这个形状。
2. **不要给端点加 `try/catch`**。404/503 已由 `ApiExceptionHandler` 统一处理；端点里再 catch 会把
   `CourseWriteResult` 与异常两条错误通道混在一起，6 号没法做断言。
3. **`CourseDraft` 的字段全部可空**（`string?`），刻意如此：JSON 里 `null` 由业务层返回
   `ValidationFailed("name", …)`，Api 不抛空引用。所以不要在这里加 `!` 或默认值兜底。

---

## 6. 落地后的验证顺序

```powershell
dotnet build src/OpenAHU.Api/OpenAHU.Api.csproj -c Release
dotnet run --project src/OpenAHU.Api
# 另一个窗口
$s = Get-Content "$env:LOCALAPPDATA\OpenAHU\session.json" | ConvertFrom-Json
$h = @{ "X-OpenAHU-Session" = $s.token }

Invoke-RestMethod "$($s.baseUrl)/api/v1/health"  -Headers $h   # ok / true
Invoke-RestMethod "$($s.baseUrl)/api/v1/terms"   -Headers $h   # 2 个学期
Invoke-RestMethod "$($s.baseUrl)/api/v1/courses?termId=term-2026-fall" -Headers $h   # 4 门课
Invoke-RestMethod "$($s.baseUrl)/api/v1/grades?termId=term-2026-fall"  -Headers $h   # course-003 的 score 为 null
Invoke-RestMethod "$($s.baseUrl)/api/v1/exams?termId=term-2026-fall"   -Headers $h   # exam-003 时间/地点为 null 且排最后
Invoke-RestMethod "$($s.baseUrl)/api/v1/courses?termId=nope" -Headers $h             # 期望 404 not_found
Invoke-RestMethod "$($s.baseUrl)/api/v1/courses" -Headers $h                        # 期望 400 invalid_request
```

写链路（用返回的 id 串起来）：

```powershell
$body = @{ name="联调课程"; teacher="示例"; location="演示楼"; dayOfWeek=5;
           startSection=9; endSection=10; weekNumbers=@(1,2); termId="term-2026-fall" } | ConvertTo-Json
$created = Invoke-WebRequest "$($s.baseUrl)/api/v1/local/courses" -Method Post -Headers $h `
    -ContentType "application/json" -Body $body
$created.StatusCode                                    # 201
($created.Headers["X-OpenAHU-Other-Students"])         # 0
$id = ($created.Content | ConvertFrom-Json).id

Invoke-WebRequest "$($s.baseUrl)/api/v1/local/courses/$id" -Method Put -Headers $h `
    -ContentType "application/json" -Body $body        # 200 + CourseDto

Invoke-WebRequest "$($s.baseUrl)/api/v1/local/courses/$id" -Method Delete -Headers $h   # 204
Invoke-WebRequest "$($s.baseUrl)/api/v1/local/courses/$id" -Method Delete -Headers $h   # 404
```

> 阻塞提醒：`course-003` 有成绩，删它应返回 **409 conflict**；这是给 6 号写断言用的固定样例。

---

## 7. 我这边还欠一个验证

Api 层的代码我**没有**编译过（本机无 .NET SDK）。上面所有代码按既有的 `Contracts` / `Api` 风格书写，
签名严格对齐 04 号 `Abstractions/` 与 05 号 `AddInfrastructure` / `CampusDatabase` 的实际实现。
若编译报错，以你本机能编译的写法为准并把差异告诉我，我改业务层，不让 Api 迁就。
