# OpenAHU.Application（04 号：业务层与仓储抽象）

业务规则、查询筛选、写入校验、事务边界的唯一归属地。
**不引用** EF Core / SQLite / ASP.NET Core / WPF，**也不引用** `OpenAHU.Contracts`（见第 2 节先决问题 A）。

```
Desktop ──HTTP──> Api ──> Application ──(接口)──> Infrastructure(EF Core/SQLite)
```

## 1. 目录

```
src/OpenAHU.Application/
  OpenAHU.Application.csproj
  Models/                       领域只读模型（record）+ 写结果
    Student.cs                  Student, CurrentProfile
    Term.cs / Course.cs / Grade.cs / Exam.cs / CampusLink.cs
    CourseDraft.cs              写请求进入业务层的形状（无 Id）
    CourseWriteResult.cs        写结果分类（Ok / ValidationFailed / UnknownTerm / NotFound / Conflict）
  Abstractions/                 5 号要实现的持久化接口
    IAppSettingsRepository.cs   active_demo_student_id
    IStudentRepository.cs / ITermRepository.cs / ICourseRepository.cs
    IEnrollmentRepository.cs / IGradeRepository.cs / IExamRepository.cs / ILinkRepository.cs
    IUnitOfWork.cs              事务边界
  Abstractions/Queries/         3 号要消费的查询用例（7 个）
  Abstractions/Commands/        ICourseCommand（课程增删改）
  Errors/
    CampusResourceNotFoundException.cs     → 404
    CampusStoreUnavailableException.cs     → 503
  Services/                     上述接口的实现
  HANDOFF_05_仓储接口与业务约定.md        ← 5 号从这里开始
  README.md
```

## 2. 对 03 号 `INTERFACE_PLAN.md` 两个先决问题的答复

### 先决问题 A：Application 是否引用 `Contracts`？—— **采你的方案 A**

Application **不引用** `Contracts`，返回自己的只读模型，形状映射由你在 Api 完成。

理由：
1. `00_所有AI必须遵守_统一上下文.md` 原文是「Contracts 被 Desktop / Api / 必要测试引用」，未列 Application，
   方案 A 是**不改冻结文档**的读法，不需要评审开口子；
2. 你已书面确认“代价是多一组 View + mapper（约 200 行），由我写”，成本可控且只有一处；
3. `WeekNumbers` 去重排序、空成绩保留、星期转换这些规则留在业务层，Api 只做字段搬运。

**我已按你的命名落地**，你写的 mapper 可以直接照抄字段名：

| 你的草案 | 实际类型 | 命名空间 |
| --- | --- | --- |
| `CurrentProfile` | `CurrentProfile` | `OpenAHU.Application.Models` |
| `TermView` | `Term` | 同上 |
| `CourseView` | `Course` | 同上 |
| `GradeView` | `Grade` | 同上 |
| `ExamView` | `Exam` | 同上 |
| `LinkView` | `CampusLink` | 同上 |

> 只把 `*View` 后缀去掉了：这些类型同时也是 5 号仓储的返回类型，叫 View 会让人误以为只能给 UI 用。
> 字段与你的草案**逐个一致**，映射就是 `new TermDto(t.Id, t.Name, t.StartDate, t.WeekCount, t.IsCurrent)` 这一级。

### 先决问题 B：`503 db_unavailable` 由谁判定？—— **采你的异常方案**

- 04 定义 `OpenAHU.Application.Errors.CampusStoreUnavailableException`
- 05 在仓储里把 `SqliteException` 包装成它
- 03 在 `ApiExceptionHandler` 里加一条类型分支 → `503 + db_unavailable`

替代方案 `QueryResult<T>` 不采：它会给**每一个**查询签名都套一层包装，而“数据库挂了”是任何调用都可能发生的真异常，
不是需要逐层判断的业务结果。

**注意分工**：写链路的可预期失败（校验、404、409）**不走异常**，走 `CourseWriteResult` 分类结果。
理由：那些结果需要携带 `field` 给前端定位输入框，用异常表达会让 `ApiExceptionHandler` 堆满分支。

### 3. 冻结签名（可直接替换你草案里的 7 个接口）

```csharp
// 全部带 CancellationToken；全部只返回当前演示资料范围内的数据，不接受 studentId 参数。
public interface IProfileQuery
{ Task<CurrentProfile> GetActiveAsync(CancellationToken ct); }

public interface ITermQuery
{ Task<IReadOnlyList<Term>> GetAllAsync(CancellationToken ct); }

public interface ICourseQuery
{ Task<IReadOnlyList<Course>> GetByTermAsync(string termId, CancellationToken ct); }

public interface IGradeQuery
{ Task<IReadOnlyList<Grade>> GetByTermAsync(string termId, CancellationToken ct); }

public interface IExamQuery
{ Task<IReadOnlyList<Exam>> GetByTermAsync(string termId, CancellationToken ct); }

public interface ILinkQuery
{ Task<IReadOnlyList<CampusLink>> GetAllAsync(CancellationToken ct); }

public interface IDataStatusQuery
{ Task<DateTimeOffset?> GetLastUpdatedAsync(CancellationToken ct); }   // 首版恒 null，见下
```

**写链路有一处必须改**：`ICourseCommand` 不再收 `CourseUpsertRequest`（那是 Contracts 里的类型，
方案 A 下我不可能引用它），改为收业务层的 `CourseDraft`：

```csharp
public sealed record CourseDraft(
    string? Name, string? Teacher, string? Location,
    DayOfWeek DayOfWeek, int StartSection, int EndSection,
    IReadOnlyList<int>? WeekNumbers, string? TermId);

public interface ICourseCommand
{
    Task<CourseWriteResult> CreateAsync(CourseDraft draft, CancellationToken ct);
    Task<CourseWriteResult> UpdateAsync(string courseId, CourseDraft draft, CancellationToken ct);
    Task<CourseWriteResult> DeleteAsync(string courseId, CancellationToken ct);
}
```

Api 侧只需一句映射：

```csharp
var draft = new CourseDraft(
    request.Name, request.Teacher, request.Location, request.DayOfWeek,
    request.StartSection, request.EndSection, request.WeekNumbers, request.TermId);
```

`CourseDraft` 的字段刻意都可空：JSON 给 `null` 时由业务校验返回 `ValidationFailed("name", …)`，
而不是在 Api 层抛空引用。

## 4. 错误映射表（你只需要加两条 using 和两个分支）

| 抛出/返回 | HTTP | `code` |
| --- | --- | --- |
| `CampusResourceNotFoundException` | 404 | `not_found` |
| `CampusStoreUnavailableException` | 503 | `db_unavailable` |
| 其余未捕获异常 | 500 | `internal`（现状不变） |
| `CourseWriteResult.Status == Ok` | 200 | — |
| `… == ValidationFailed`（含排课时间冲突） | 400 | `invalid_request`，`message` 用结果里的文案 |
| `… == UnknownTerm` / `NotFound` | 404 | `not_found` |
| `… == Conflict`（`HasGrades`/`HasExams`） | 409 | `conflict` |

在 `ApiExceptionHandler.TryHandleAsync` 里，于现有 500 分支**之前**插入：

```csharp
switch (exception)
{
    case CampusResourceNotFoundException:
        await ApiResults.WriteAsync(httpContext, StatusCodes.Status404NotFound,
            ApiErrorCodes.NotFound, "请求的学期或课程不存在。", cancellationToken);
        return true;
    case CampusStoreUnavailableException:
        await ApiResults.WriteAsync(httpContext, StatusCodes.Status503ServiceUnavailable,
            ApiErrorCodes.DbUnavailable, "本地数据服务尚未就绪，请稍后重试。", cancellationToken);
        return true;
}
```

### 三个语义决定（原草案的“待 4 号确认”，现在确认）

| 主题 | 结论 |
| --- | --- |
| 未知 `termId` | **404 `not_found`**（资源不存在），与你建议一致；空 `termId` 仍由你做 400 |
| 排课时间冲突（同一天、节次重叠、有共同周次） | **400 `invalid_request`**，`field="startSection"`。虽然语义上接近 409，但它是用户在表单里能直接改的错，带字段名更好用 |
| `DataStatusDto.UpdatedAt` | **首版恒 `null`**（你 §6 的选项 3）。`IDataStatusQuery` 已实现并固定返回 `null`，你接不接都行——建议先别接，省一次 DI 和一次往返 |

另外确认你 §4 表里的其余约定全部按你写的实现：课程按 `termId, dayOfWeek, startSection` 升序；
`weekNumbers` 升序去重、空数组表示未排课；成绩 `null` 不代 0；考试 `startsAt` 升序且 `null` 排末尾；
星期转换只在 4/5 号做一次；学分用 `decimal`；身份只读 `app_settings.active_demo_student_id`。

## 5. 你需要改的三处（都在你的目录里，我不动）

> 完整可粘贴的代码见同目录 `INTEGRATION_03_接入代码.md`（含整份 `ApiExceptionHandler`、`Program.cs`、
> 9 个端点实现与 DTO 映射、写接口响应映射、验证脚本）。下面是摘要。

1. `src/OpenAHU.Api/OpenAHU.Api.csproj` 增加：
   ```xml
   <ProjectReference Include="..\OpenAHU.Application\OpenAHU.Application.csproj" />
   ```
2. `ApiExceptionHandler.cs` 加上第 4 节的两个分支。
3. `Program.cs` 注册服务（本层刻意不提供 `AddApplication()`：加它需要
   `Microsoft.Extensions.DependencyInjection.Abstractions` 包，而本仓库目前零 NuGet 依赖；
   `01_架构 §2` 也写明注册应发生在 Api 的 Composition Root）：

```csharp
// Application（4 号）
builder.Services.AddScoped<ActiveStudentProvider>();
builder.Services.AddScoped<IProfileQuery, ProfileQuery>();
builder.Services.AddScoped<ITermQuery, TermQuery>();
builder.Services.AddScoped<ICourseQuery, CourseQuery>();
builder.Services.AddScoped<IGradeQuery, GradeQuery>();
builder.Services.AddScoped<IExamQuery, ExamQuery>();
builder.Services.AddScoped<ILinkQuery, LinkQuery>();
builder.Services.AddScoped<IDataStatusQuery, DataStatusQuery>();
builder.Services.AddScoped<ICourseCommand, CourseCommand>();
// 仓储与 IUnitOfWork 由 5 号提供，生命周期必须全为 Scoped 且共享同一 DbContext
```

把 6 个占位端点替换成真实查询时，端点体大概是这样（以课表为例）：

```csharp
group.MapGet("/courses", async (string termId, ICourseQuery query, HttpContext ctx) =>
{
    var courses = await query.GetByTermAsync(termId, ctx.RequestAborted);
    return Results.Ok(courses.Select(c => new CourseDto(
        c.Id, c.Name, c.Teacher, c.Location, c.DayOfWeek,
        c.StartSection, c.EndSection, c.WeekNumbers, c.TermId)));
});
```

## 6. 写接口响应约定（已对齐 03 号 `Contracts/README.md`）

03 号已在 `src/OpenAHU.Contracts/README.md` 冻结写接口响应，本层据此调整，**不再提议新的响应 DTO**：

| 接口 | 约定 | 本层如何配合 |
| --- | --- | --- |
| `POST /local/courses` | `201` + `CourseDto`（含新 id），`Location` 指向新资源 | `CourseWriteResult.Course` 带回完整课程，直接映射 |
| `PUT /local/courses/{id}` | `200` + 更新后的 `CourseDto` | 同上 |
| `DELETE /local/courses/{id}` | `204` 无响应体 | 只有 `CourseId`，`Course` 为 `null` |

```csharp
var result = await command.CreateAsync(draft, ct);
if (result.Status == CourseWriteStatus.Ok)
{
    var dto = new CourseDto(result.Course!.Id, result.Course.Name, /* … */);
    return Results.Created($"/api/v1/local/courses/{dto.Id}", dto);
}
```

### 唯一遗留：`OtherStudentCount` 无处安放

`EnrolledStudentCount` / `OtherStudentCount` 在冻结的 `CourseDto` 里没有字段，
但 2 号要做“该课程还有 N 名学生选修，修改会同步影响他们”的警示。

**建议（成本最低、不动 DTO）**：写成响应头，不需要新类型、不需要评审：

```
X-OpenAHU-Enrolled-Students: 2
X-OpenAHU-Other-Students: 1
```

若 3 号/组长认为响应头不便于前端读取，退路是放弃该提示——功能不残缺，只是少一次提醒。

## 7. 验证状态（如实记录，不声称通过）

**已执行（由 05 号代为编译）**

- 05 号在 .NET SDK 10.0.403 下执行 `dotnet build src/OpenAHU.Infrastructure/`，
  **Application + Infrastructure 一起编译，0 警告 0 错误**。这是本层第一次通过真实编译。
- 05 号用真实 `CourseCommand` 跑了 57 项集成测试（含新增/编辑/删除/重启持久化），全部通过。

**本次修订后未再编译**

在 05 号交回之后，本层又做了三处改动（见第 10 节），**这三处尚未编译验证**，
需在有 SDK 的机器上重跑 `dotnet build src/OpenAHU.Application/OpenAHU.Application.csproj`。

## 8. 下一步（按优先级）

1. **5 号**：按 `HANDOFF_05_仓储接口与业务约定.md` 实现 9 个接口 → ✅ 已完成（57 项测试通过）。
2. **3 号**：接第 5 节三处改动 + 第 4 节异常分支 → 6 个占位端点转真实查询。
3. **4 号**：补业务单元测试（见第 9 节）。

## 9. 业务单元测试（`tests/OpenAHU.Application.Tests/`）

包版本沿用 5 号已验证的组合，避免 CI 因版本猜错而红：

```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
```

用手写假仓储（内存 `List`），不引 EF、不用 Moq，因此测试不依赖数据库文件。

| 被测对象 | 用例 |
| --- | --- |
| `CourseWriteValidator` | 空名/超长名；`startSection` 越界；`endSection < startSection`；周次为空；周次超出 `WeekCount`；`DayOfWeek` 非法值 |
| `CourseCommand.CreateAsync` | 正常创建 → `Ok` 且带回课程、id 前缀 `course-local-`；未知学期 → `UnknownTerm`；周次越界 → `ValidationFailed("weekNumbers")`；同日同节且有共同周 → `ValidationFailed("startSection")`；单双周错开 → **不**冲突 |
| `CourseCommand.UpdateAsync` | 课程不存在 → `NotFound`；**只选修了别人课表的课程 → `NotFound`**；跳过自身不算冲突；`OtherStudentCount > 0` 时仍允许并回传人数 |
| `CourseCommand.DeleteAsync` | 有成绩 → `Conflict(HasGrades)`；有考试 → `Conflict(HasExams)`；都没有 → `Ok`；**检查发生在事务内**（假 `IUnitOfWork` 断言回调被调用） |
| `CourseQuery` | 输出周次升序去重；排序为 **周一→周日**（防 `DayOfWeek` 周日=0 的错位）；未知 `termId` → `CampusResourceNotFoundException` |
| `GradeQuery` / `ExamQuery` | 空成绩保持 `null`（不得变 0）；考试按 `StartsAt` 升序且 `null` 排末尾 |
| `ActiveStudentProvider` | 设置缺失 → `CampusStoreUnavailableException`；学生不存在 → 同上 |

## 10. 响应 05 号提出的三个问题（本次修订）

| 问题 | 结论 | 改动 |
| --- | --- | --- |
| ①`Update`/`Delete` 按全局 `courseId` 查找，未限制当前学生 | **收紧**：只能维护自己课表里的课程，“不属于我”返回 404（不泄露存在性） | `CourseCommand` 新增 `IsEnrolledAsync` 前置校验 |
| ② 关联检查在事务外，并发时被 05 号的原子条件拦成 503 | **采纳 05 号建议**：检查、计数、删除放进同一个 `IUnitOfWork.ExecuteAsync` | `DeleteAsync` 重写；`UpdateAsync` 的计数也移入事务 |
| ③ 本层建议的 `200 +{id,counts}` 与 03 号的 `201/200 + CourseDto`、`204` 不一致 | **服从 03 号已冻结的契约**，本层补 `Course` 回传 | `CourseWriteResult.Ok(Course, …)`；计数改走响应头 |


