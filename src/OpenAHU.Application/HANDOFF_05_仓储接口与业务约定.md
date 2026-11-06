# 04 → 05：仓储接口与业务约定（可直接开始实现）

> 对应你提的三件事：**① 接口代码 ② 配套数据类型 ③ 业务约定（能否删除、修改影响谁、失败如何回滚）**。
> 全部代码在 `src/OpenAHU.Application/`，本层**不引用 EF Core / SQLite / ASP.NET Core**，
> 你实现 `Abstractions/` 下的 8 个接口 + `IUnitOfWork` 即可，领域模型直接用 `Models/` 里的 record。

---

## 1. 你要实现的接口（9 个）

| 文件 | 方法 | 一句话 |
| --- | --- | --- |
| `IAppSettingsRepository` | `GetActiveDemoStudentIdAsync(ct)` | 读 `app_settings.active_demo_student_id`，没有则返回 `null` |
| `IStudentRepository` | `FindAsync(studentId, ct)` | 按主键取学生，无则 `null` |
| `ITermRepository` | `GetAllAsync(ct)` / `FindAsync(termId, ct)` | 学期列表 / 单个学期 |
| `ICourseRepository` | `GetByStudentAndTermAsync` / `FindAsync` / `AddAsync` / `UpdateAsync` / `DeleteAsync` | 课程与周次读写 |
| `IEnrollmentRepository` | `AddAsync` / `CountByCourseAsync` / `CountOthersByCourseAsync` | 选课关系与影响面统计 |
| `IGradeRepository` | `GetByStudentAndTermAsync` / `ExistsByCourseAsync` | 成绩查询 + 删除前冲突判定 |
| `IExamRepository` | `GetByStudentAndTermAsync` / `ExistsByCourseAsync` | 考试查询 + 删除前冲突判定 |
| `ILinkRepository` | `GetAllAsync(ct)` | 常用入口 |
| `IUnitOfWork` | `ExecuteAsync(operation, ct)` | 事务边界（见第 4 节） |

**关键：这 8 个仓储接口里没有任何一个接受 `studentId` 作为“任意查询条件”。**
`GetByStudentAndTermAsync` 的 `studentId` 只允许来自 `IAppSettingsRepository` 解析出的活动演示学生，
业务层已经保证了这一点，你**不要**再为仓储加“按任意学号查”的重载。

---

## 2. 配套数据类型：查询返回什么、保存传什么

### 2.1 查询返回（`OpenAHU.Application.Models`）

| 类型 | 字段 | 你需要注意的 |
| --- | --- | --- |
| `Student` | Id, DisplayName, College, Major, GradeYear | College/Major/GradeYear 可空 |
| `Term` | Id, Name, StartDate(`DateOnly`), WeekCount, IsCurrent | `start_date` 是 `YYYY-MM-DD` 文本，转 `DateOnly` |
| `Course` | Id, Name, Teacher, Location, **DayOfWeek**, StartSection, EndSection, **WeekNumbers**, TermId | 见 2.2 星期转换 |
| `Grade` | CourseId, CourseName, Credits(`decimal`), Score(`decimal?`), GradePoint(`decimal?`), TermId | `NULL` 必须保持 `null`，**不许填 0** |
| `Exam` | Id, CourseName, StartsAt(`DateTimeOffset?`), EndsAt, Location, TermId | 带偏移 ISO → `DateTimeOffset` |
| `CampusLink` | Id, Title, Url, Category | — |

### 2.2 三个必须在**你的边界上**完成的转换

1. **星期**：库 `day_of_week` 是 周一=1 … 周日=7，领域模型是 C# `DayOfWeek`（周日=0 … 周六=6）。
   读：`(DayOfWeek)(dbDay % 7)`；写：`dbDay = value == DayOfWeek.Sunday ? 7 : (int)value`。
   **业务层和 Api 都不再转换**，你转错一次就是全项目错一格。
2. **学分**：`REAL` → `decimal`，不要用 `double`（前端会看到 3.0000000000000004）。
3. **时间**：`starts_at`/`ends_at` 是带偏移 ISO 文本（如 `2026-12-21T09:00:00+08:00`）→ `DateTimeOffset?`，`NULL` 保持 `null`。

### 2.3 写入时你收到什么

`AddAsync(Course)` / `UpdateAsync(Course)` 收到的是**已经校验通过、已经规范化**的对象：

- `Id` 已由业务层生成（`course-local-xxxxxxxxxxxx`），你直接用，不要重新生成；
- `WeekNumbers` 已升序去重；
- `Teacher` / `Location` 已 Trim，无值时是 `""` 而非 `null`；
- `DayOfWeek` 是 C# 语义，入库前按 2.2 转回 1–7。

> 业务层**不做**数据库层的兜底：外键、CHECK 约束仍由 schema 保证，你不用在仓储里重复校验一遍字段范围。

---

## 3. 业务约定（这一节是重点）

### 3.1 课程能不能删？

**能删，但有三道闸：**

| 情况 | 业务层行为 | 你的仓储要做什么 |
| --- | --- | --- |
| 课程有成绩（`grades` 有该 `course_id`） | **拒绝**，返回 409 `conflict`（`HasGrades`） | 不用管，`ExistsByCourseAsync` 只负责如实回答 |
| 课程有考试（`exams` 有该 `course_id`） | **拒绝**，返回 409 `conflict`（`HasExams`） | 同上 |
| 无成绩无考试 | 允许删除 | `DeleteAsync` 在一个事务内删课程，`course_weeks`/`enrollments` 随 `ON DELETE CASCADE` 消失 |

判定**不区分学生**：即使成绩是另一个演示学生的，也不许静默删掉——所以 `ExistsByCourseAsync` 请按 `course_id` 查，
**不要**加 `student_id` 过滤。

### 3.2 修改会不会影响其他选课学生？

**会。** `courses` 是共享行，`enrollments` 是多对多。当前种子库里 `course-001`、`course-002` 同时被 `demo-001` 和 `demo-002` 选修，
所以编辑这两门课会同步改变另一个演示学生的课表。

业务层的处理：**不阻止，但把影响人数告诉前端** —— `CourseWriteResult.OtherStudentCount`。
前端（2 号）据此弹警示“该课程还有 N 名学生选修，修改会同步影响他们的课表”。

你的仓储只需要老实实现 `CountByCourseAsync` 和 `CountOthersByCourseAsync`，
**不要**自作主张去“复制一份只给当前学生”或“顺手把别人的选课删掉”。编辑不改变选课关系。

### 3.3 事务边界在哪？

| 操作 | 事务内包含 | 为什么 |
| --- | --- | --- |
| 新增 | `courses` 行 + N 条 `course_weeks` + 1 条 `enrollments` | 少写选课关系 → 新课程在课表里看不见 |
| 编辑 | `courses` 行 + 整组 `course_weeks` 替换 | 周次是“整体替换”，不是增量 |
| 删除 | `courses` + 级联的 `course_weeks`/`enrollments` | 不能留孤儿周次 |

`ICourseRepository.AddAsync/UpdateAsync/DeleteAsync` 都会被 `IUnitOfWork.ExecuteAsync` 包住，
**所以这三个方法不要自己再开事务**（嵌套事务在 SQLite 上容易变成 `SQLITE_BUSY`）。
`IUnitOfWork` 的实现只要一层 `BeginTransaction` → 成功 `Commit` → 异常 `Rollback` 即可。

`UpdateAsync` 的周次替换请用 **先删该课全部 `course_weeks` 再批量插入**，不要做差量比对——
同一周次“先删后加”时差量实现很容易算错，这是最常见的 bug 来源。

### 3.4 失败怎么处理、如何回滚？

1. 任何 `SqliteException`（含 `SQLITE_BUSY`、`SQLITE_READONLY`、`no such table`、迁移未应用）
   **必须在仓储内包装成** `CampusStoreUnavailableException` 再抛出：
   ```csharp
   catch (SqliteException ex)
   {
       throw new CampusStoreUnavailableException("本地数据库不可用（读/写失败）。", ex);
   }
   ```
   不要让 `SqliteException` 漏到上层：Api 一旦为了 catch 它就得 `using Microsoft.Data.Sqlite`，分层就破了。
2. 包装时**不要**把数据库绝对路径写进 `Message`（会进日志甚至被误传到响应体）。
3. `IUnitOfWork.ExecuteAsync` 捕获到任何异常都要 `Rollback` 后**原样重抛**，不要吞掉、不要换成别的类型。
4. 业务层承诺：字段校验、学期/课程不存在、删除冲突这些**可预期**情况根本不会走到你的写方法，
   所以你不用在仓储里判断“这门课该不该删”。

### 3.5 排序与筛选归谁？

**归业务层。** 仓储的方法一律**不排序**（`GetAllAsync`、`GetByStudentAndTermAsync` 的返回顺序不保证），
业务层会按 `DayOfWeek → StartSection → Name`、学期按 `StartDate` 倒序等规则统一排。
你排了也不会错，但会造成两处规则、以后改一处忘一处——所以**请不要在仓储里 `OrderBy`**。

唯一必须由你保证的是 `Course.WeekNumbers` 的**聚合正确性**：一门课的多条 `course_weeks` 要合并进一条 `Course`，
不要一门课返回多条。

---

## 4. `IUnitOfWork` 实现要点

```csharp
public sealed class EfUnitOfWork : IUnitOfWork   // Scoped
{
    private readonly CampusDbContext _db;
    public EfUnitOfWork(CampusDbContext db) => _db = db;

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await operation(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);   // 回滚本身不要再被取消打断
            throw;
        }
    }
}
```

⚠️ **最容易踩的坑**：`IUnitOfWork` 与全部仓储必须是 **Scoped**，且共享同一个 `DbContext` 实例。
若你给仓储注入了不同的 `DbContext`（或用了 `IDbContextFactory` 每次 new 一个），
事务根本包不住它们，出现“课程写进去了、周次没写进去”的半截数据。

---

## 5. 建议的实现顺序（每一步都能独立验证）

1. `IAppSettingsRepository` + `IStudentRepository` → 让 `GET /profile` 能返回真实数据（Gate A 的一半）。
2. `ITermRepository` + `ILinkRepository` → `GET /terms`、`GET /links` 通。
3. `ICourseRepository.GetByStudentAndTermAsync` → `GET /courses?termId=...` 通（读链路 Gate B）。
4. `IGradeRepository` / `IExamRepository` 的查询 → 成绩、考试通。
5. 三个写方法 + `IEnrollmentRepository` + `IUnitOfWork` → Gate C（重启后新增课程仍在）。

---

## 6. 我这边的验证状态（如实说明）

- **未编译**：本机 `dotnet --version` 报 no .NET SDKs were found，与 3 号的情况一致。
- 代码按 C# 12 / net10.0 书写，未引入任何 NuGet 包（与 `Contracts`/`Api` 一致）。
- 你实现完仓储后，请在装有 .NET 10 SDK 的机器上跑
  `dotnet build src/OpenAHU.Application/OpenAHU.Application.csproj`，
  若签名有编译错误，**以你实际能编译的版本为准并回传给我**，我改业务层，不要你迁就。

## 7. 需要你回给我的两件事

1. `IUnitOfWork` 与 8 个仓储的**注册生命周期**（确认全是 Scoped + 共享 DbContext）。
2. `app_settings.active_demo_student_id` 在**首次迁移 + Seed** 后是否一定存在（我按“不存在 → 503 未初始化”处理）。
