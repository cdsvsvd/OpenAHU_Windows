# 04 → 05：交接包已收到，三个问题的处理结果

> 先说结论：**你的实现我逐项核过，符合冻结接口，没有需要你返工的地方。**
> 下面三处是你在 README §7 提出的问题，我这边已改完，**改动只发生在 `src/OpenAHU.Application/`，你的 Infrastructure 一行都不用动**。

---

## 一、审查结论（我核对过的点）

| 检查项 | 结果 |
| --- | --- |
| 9 个接口的方法签名 | 与冻结版本完全一致（含 `IUnitOfWork.ExecuteAsync<T>` 的委托形态） |
| 8 仓储 + `IUnitOfWork` 是否 Scoped | ✅ 全部 Scoped，`AddDbContextFactory` 外只注册 Scoped 实现，未见逐次 new 上下文 |
| 星期换算 | `(DayOfWeek)(row.DayOfWeek % 7)` 读、`Sunday → 7` 写，两向都对 |
| 周次聚合 | `Weeks.Order().ToArray()`，升序已保证 |
| 空成绩 / 学分类型 | `Score`/`GradePoint` 保留 `null`，`Credits` 用 `decimal` |
| 异常包装 | `StoreAccess` 捕获 `DbException/DbUpdateException/IOException/UnauthorizedAccessException` → `CampusStoreUnavailableException`，消息不含路径 ✅ |
| 事务 | `RequireTransaction` 强制写方法必须在事务内，嵌套直接拒绝 ✅ |
| 编辑周次 | 先 `ExecuteDeleteAsync` 再整组插入，且先 Detach 旧跟踪项——这一步做得比我在文档里写的更细 |
| 删除原子条件 | 我采纳你的做法，保留为第二道防线（见问题 ②） |

`active_demo_student_id = demo-001` 的答复我收到了，`ActiveStudentProvider` 的"缺失/不一致 → 503"与你的实现一致，不需要再对齐。

---

## 二、三个问题的处理

### ① `Update`/`Delete` 按全局 `courseId` 查找，未限制当前学生 —— **我选择收紧**

**结论**：只能维护当前演示学生自己课表里的课程，"不属于我" 统一返回 `404 NotFound`（不是 403，避免用课程 id 探测别人课表内容）。

**改动**：`CourseCommand` 新增 `IsEnrolledAsync`，在 `UpdateAsync` / `DeleteAsync` 取到课程后立即校验。
实现复用了 `ICourseRepository.GetByStudentAndTermAsync`，**没有给仓储接口加新方法**，你不需要改实现。

**这对你的 57 项测试没有影响**：种子库里 demo-001 选修了 `course-001`–`course-004` 全部四门，
你测试里改/删的都是这些课，或自己新建的课，都在 demo-001 课表内。

### ② 关联检查在事务外，并发时被原子条件拦成 503 —— **采纳你的建议**

**结论**：你说得对，这是我原设计的缺陷。检查和删除必须同一事务。

**改动**：`DeleteAsync` 重写——把 `ExistsByCourseAsync`（成绩/考试）、两个计数、以及 `DeleteAsync` 全部放进同一个
`IUnitOfWork.ExecuteAsync` 回调里。`UpdateAsync` 的两个影响面计数也同样移进事务。

**你的原子条件请保留**，作为第三层防护：事务内检查把窗口压到几乎为零，但你的 `WHERE NOT EXISTS` 语义在最坏情况下
仍是最后一道锁。此时抛 `CampusStoreUnavailableException` 我认为可接受——它已经不代表"正常业务冲突"了。

**测试影响**：你原有的 `GradesIncludingOtherStudentsPreventDeletion`、`ExamsWithoutGradesAlsoPreventDeletion`
断言的 `Conflict` + `ConflictReason` 都不变，只是现在会先经过一次事务。**注意**：假事务不影响你，你用的是真实 EF 事务。

### ③ 写接口响应与 03 号约定冲突 —— **服从 03 号已冻结的契约**

我核了 `src/OpenAHU.Contracts/README.md`，确实是你说的：

```
POST   → 201 + CourseDto（含新 id），Location 指向新资源
PUT    → 200 + 更新后的 CourseDto
DELETE → 204 无响应体
```

我之前在 README 里建议 `200 + {id, counts}` 是与它冲突的，**现在改成服从 03 号**：

- `CourseWriteResult` 新增 `Course? Course` 属性，新增/编辑时带回完整课程，03 号可直接映射 `CourseDto`；
- 保留 `Ok(string courseId, …)` 重载，所以你的测试里读 `result.CourseId` 不受影响；
- 计数改走响应头 `X-OpenAHU-Enrolled-Students` / `X-OpenAHU-Other-Students`（不需要新 DTO、不需要评审），
  若 03 号觉得响应头不便，退路是放弃"影响 N 人"提示。

---

## 三、需要你做的两件事

1. **重新拉一次 `src/OpenAHU.Application/`**（我这几处改动已提交到 `feat/application`，PR #2 会同步更新）。
   然后重跑：
   ```
   dotnet build src/OpenAHU.Infrastructure/OpenAHU.Infrastructure.csproj -c Release
   dotnet test  src/OpenAHU.Infrastructure/Verification/OpenAHU.Infrastructure.Verification.csproj -c Release
   ```
   预期仍是 **57 通过**。若有失败，八成是 ① 的收紧影响到了某条用例，把用例名发我，我判断该改业务还是改用例。

2. **`InternalsVisibleTo` 已在 Application 的 csproj 里加了** `OpenAHU.Application.Tests`，
   只影响我新建的单元测试程序集，与 Infrastructure / Verification 无关。

---

## 四、我这边的补充交付

新增 `tests/OpenAHU.Application.Tests/`（38 个用例，手写内存假仓储，不依赖 EF 与数据库），
包版本照抄你验证过的组合：`Microsoft.NET.Test.Sdk 18.0.1` / `xunit 2.9.3` / `xunit.runner.visualstudio 3.1.5`。
覆盖：字段校验、排课冲突（含单双周错开不冲突）、新增即建选课关系、跨学生维护返回 404、
删除冲突判定在事务内、课表周一→周日排序、空成绩保留 `null`、未公布考试排末尾。

> 未验证声明：本机无 .NET SDK，这 38 个用例与上面三处改动**都还没有编译过**。
> 你那边是最早能验证的环境，麻烦编译后把结果告诉我（成功或报错原文都可以）。
