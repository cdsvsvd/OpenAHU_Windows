using OpenAHU.Application.Models;
using OpenAHU.Application.Tests.Fakes;
using Xunit;

namespace OpenAHU.Application.Tests;

/// <summary>
/// 课程写链路的业务规则测试。重点覆盖三件容易出错的事：
/// ① 新增必须同时写选课关系；② 排课时间冲突的判定（节次重叠 + 有共同周次）；③ 删除前的关联检查必须在事务内。
/// </summary>
public sealed class CourseCommandTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task 新增成功_返回课程并建立当前学生选课关系()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        var result = await harness.CourseCommand.CreateAsync(ApplicationHarness.Draft(), Ct);

        Assert.Equal(CourseWriteStatus.Ok, result.Status);
        Assert.NotNull(result.Course);
        Assert.StartsWith("course-local-", result.CourseId);
        Assert.Equal(result.CourseId, result.Course!.Id);
        Assert.Equal(1, result.EnrolledStudentCount);
        Assert.Equal(0, result.OtherStudentCount);

        // 课程与选课关系必须同时存在，否则新课在课表里看不见。
        Assert.Contains(harness.Store.Courses, x => x.Id == result.CourseId);
        Assert.Contains(("demo-001", result.CourseId!), harness.Store.Enrollments);
        Assert.DoesNotContain(("demo-002", result.CourseId!), harness.Store.Enrollments);
    }

    [Fact]
    public async Task 新增_未知学期_返回UnknownTerm且不写库()
    {
        var harness = ApplicationHarness.Create().WithDemoData();
        var before = harness.Store.Courses.Count;

        var result = await harness.CourseCommand.CreateAsync(
            ApplicationHarness.Draft(termId: "term-not-exist"), Ct);

        Assert.Equal(CourseWriteStatus.UnknownTerm, result.Status);
        Assert.Equal(before, harness.Store.Courses.Count);
        Assert.Equal(0, harness.Courses.AddCalls);
    }

    [Fact]
    public async Task 新增_周次越界_字段校验失败且不写库()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        var result = await harness.CourseCommand.CreateAsync(
            ApplicationHarness.Draft(weeks: new[] { 1, 25 }), Ct);

        Assert.Equal(CourseWriteStatus.ValidationFailed, result.Status);
        Assert.Equal("weekNumbers", result.Field);
        Assert.Equal(0, harness.Courses.AddCalls);
        Assert.Equal(0, harness.UnitOfWork.Executions);
    }

    [Fact]
    public async Task 新增_同一天同节次且有共同周次_判为时间冲突()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        // course-001 是周一 1–2 节、第 1–3 周。
        var result = await harness.CourseCommand.CreateAsync(
            ApplicationHarness.Draft(dayOfWeek: DayOfWeek.Monday, startSection: 1, endSection: 2,
                weeks: new[] { 3, 4 }), Ct);

        Assert.Equal(CourseWriteStatus.ValidationFailed, result.Status);
        Assert.Equal("startSection", result.Field);
        Assert.Contains("软件工程实践", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, harness.Courses.AddCalls);
    }

    [Fact]
    public async Task 新增_节次重叠但没有共同周次_不算冲突()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        // course-003 是周四 5–6 节、第 2/4/6 周（单周）。这里选双周，互不重叠。
        var result = await harness.CourseCommand.CreateAsync(
            ApplicationHarness.Draft(dayOfWeek: DayOfWeek.Thursday, startSection: 5, endSection: 6,
                weeks: new[] { 1, 3, 5 }), Ct);

        Assert.Equal(CourseWriteStatus.Ok, result.Status);
    }

    [Fact]
    public async Task 新增_整条写链路只开一个事务()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        await harness.CourseCommand.CreateAsync(ApplicationHarness.Draft(), Ct);

        // 课程行、周次、选课关系必须落在同一个事务里；多次开事务就无法回滚成整体。
        Assert.Equal(1, harness.UnitOfWork.Executions);
    }

    [Fact]
    public async Task 编辑_课程不存在_返回NotFound()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        var result = await harness.CourseCommand.UpdateAsync("course-none", ApplicationHarness.Draft(), Ct);

        Assert.Equal(CourseWriteStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task 编辑_课程不在自己课表里_返回NotFound且不改动()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        // 只被 demo-002 选修的课程：当前活动学生是 demo-001，不应该能改。
        harness.Store.Courses.Add(new Course("course-only-b", "学生乙限定课", "", "",
            DayOfWeek.Friday, 11, 12, new[] { 1 }, ApplicationHarness.FallTerm));
        harness.Store.Enrollments.Add(("demo-002", "course-only-b"));

        var result = await harness.CourseCommand.UpdateAsync(
            "course-only-b", ApplicationHarness.Draft(name: "被篡改的名字"), Ct);

        Assert.Equal(CourseWriteStatus.NotFound, result.Status);
        Assert.Equal("学生乙限定课", harness.Store.Courses.Single(x => x.Id == "course-only-b").Name);
        Assert.Equal(0, harness.Courses.UpdateCalls);
    }

    [Fact]
    public async Task 编辑_保留自身时段不算与自己冲突()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        // 用 course-002 自己的时段去更新它，不应被判定为冲突。
        var result = await harness.CourseCommand.UpdateAsync("course-002",
            ApplicationHarness.Draft(name: "数据库原理（改名）", dayOfWeek: DayOfWeek.Tuesday,
                startSection: 3, endSection: 4, weeks: new[] { 1, 2 }), Ct);

        Assert.Equal(CourseWriteStatus.Ok, result.Status);
        Assert.Equal("数据库原理（改名）", harness.Store.Courses.Single(x => x.Id == "course-002").Name);
    }

    [Fact]
    public async Task 编辑_回传共享课程的影响人数()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        // course-002 被 demo-001 与 demo-002 共同选修：共 2 人，其中"别人"1 人。
        var result = await harness.CourseCommand.UpdateAsync("course-002",
            ApplicationHarness.Draft(name: "共享课改名", dayOfWeek: DayOfWeek.Tuesday,
                startSection: 3, endSection: 4, weeks: new[] { 1, 2 }), Ct);

        Assert.Equal(CourseWriteStatus.Ok, result.Status);
        Assert.Equal(2, result.EnrolledStudentCount);
        Assert.Equal(1, result.OtherStudentCount);
        Assert.NotNull(result.Course);
    }

    [Fact]
    public async Task 删除_课程有成绩_返回冲突且不删除()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        var result = await harness.CourseCommand.DeleteAsync("course-003", Ct);

        Assert.Equal(CourseWriteStatus.Conflict, result.Status);
        Assert.Equal(CourseConflictReason.HasGrades, result.ConflictReason);
        Assert.Contains(harness.Store.Courses, x => x.Id == "course-003");
        Assert.Equal(0, harness.Courses.DeleteCalls);
    }

    [Fact]
    public async Task 删除_课程有考试_返回冲突且不删除()
    {
        var harness = ApplicationHarness.Create().WithDemoData();
        var created = await harness.CourseCommand.CreateAsync(ApplicationHarness.Draft(), Ct);
        var id = created.CourseId!;

        // 只挂考试、不挂成绩，确认“有考试”这条规则独立生效（不被 HasGrades 抢先命中）。
        harness.Store.Exams.Add((id, new Exam("exam-x", "数据结构（演示）", null, null, null,
            ApplicationHarness.FallTerm)));

        var result = await harness.CourseCommand.DeleteAsync(id, Ct);

        Assert.Equal(CourseWriteStatus.Conflict, result.Status);
        Assert.Equal(CourseConflictReason.HasExams, result.ConflictReason);
        Assert.Contains(harness.Store.Courses, x => x.Id == id);
        Assert.Equal(0, harness.Courses.DeleteCalls);
    }

    [Fact]
    public async Task 删除_无关联_成功并清理选课关系()
    {
        var harness = ApplicationHarness.Create().WithDemoData();
        var created = await harness.CourseCommand.CreateAsync(ApplicationHarness.Draft(), Ct);
        var id = created.CourseId!;

        var result = await harness.CourseCommand.DeleteAsync(id, Ct);

        Assert.Equal(CourseWriteStatus.Ok, result.Status);
        Assert.DoesNotContain(harness.Store.Courses, x => x.Id == id);
        Assert.DoesNotContain(harness.Store.Enrollments, x => x.CourseId == id);
    }

    [Fact]
    public async Task 删除_关联检查发生在事务内()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        // 即便是"被拒绝"的删除，也应该进入过事务：检查与删除必须同一事务，
        // 否则检查通过到删除之间的窗口里新写入的成绩会被级联静默删掉。
        var result = await harness.CourseCommand.DeleteAsync("course-003", Ct);

        Assert.Equal(CourseWriteStatus.Conflict, result.Status);
        Assert.Equal(1, harness.UnitOfWork.Executions);
        Assert.Equal(0, harness.UnitOfWork.Rollbacks);
    }

    [Fact]
    public async Task 删除_第二次删除同一课程_返回NotFound()
    {
        var harness = ApplicationHarness.Create().WithDemoData();
        var created = await harness.CourseCommand.CreateAsync(ApplicationHarness.Draft(), Ct);
        var id = created.CourseId!;

        Assert.Equal(CourseWriteStatus.Ok, (await harness.CourseCommand.DeleteAsync(id, Ct)).Status);
        Assert.Equal(CourseWriteStatus.NotFound, (await harness.CourseCommand.DeleteAsync(id, Ct)).Status);
    }
}
