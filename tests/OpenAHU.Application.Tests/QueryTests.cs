using OpenAHU.Application.Errors;
using OpenAHU.Application.Models;
using OpenAHU.Application.Tests.Fakes;
using Xunit;

namespace OpenAHU.Application.Tests;

/// <summary>
/// 读路径的业务规则：排序、周次规范化、空值保留、数据隔离。
/// 这些规则此前最容易出错的地方是“星期用 (int)DayOfWeek 排序”，会让周日跑到周一前面。
/// </summary>
public sealed class QueryTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task 课表_按周一到周日排序_周日排在最后()
    {
        var harness = ApplicationHarness.Create().WithDemoData();
        harness.Store.Courses.Add(new Course("course-sun", "周日实验", "", "",
            DayOfWeek.Sunday, 1, 2, new[] { 1 }, ApplicationHarness.FallTerm));
        harness.Store.Enrollments.Add(("demo-001", "course-sun"));

        var courses = await harness.CourseQuery.GetByTermAsync(ApplicationHarness.FallTerm, Ct);

        // 周一(001) → 周二(002) → 周四(003) → 周日(course-sun)。
        // 若误用 (int)DayOfWeek 排序，周日 = 0 会跑到最前面，此断言即失败。
        Assert.Equal(
            new[] { "course-001", "course-002", "course-003", "course-sun" },
            courses.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task 课表_周次升序去重()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        var courses = await harness.CourseQuery.GetByTermAsync(ApplicationHarness.FallTerm, Ct);

        // 种子故意写成 {3,1,2,3}。
        Assert.Equal(new[] { 1, 2, 3 }, courses.Single(x => x.Id == "course-001").WeekNumbers);
    }

    [Fact]
    public async Task 课表_未知学期_抛资源不存在()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        await Assert.ThrowsAsync<CampusResourceNotFoundException>(
            () => harness.CourseQuery.GetByTermAsync("term-not-exist", Ct));
    }

    [Fact]
    public async Task 课表_该学期没选课_返回空列表而不是404()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        // 前提：学期存在（不存在才是 404），但该学生这一学期一门课都没选。
        // 演示库里 demo-001 其实选了春季的 course-004，所以这里换成只选秋季课的 demo-002。
        harness.Store.Settings["active_demo_student_id"] = "demo-002";

        var courses = await harness.CourseQuery.GetByTermAsync(ApplicationHarness.SpringTerm, Ct);

        Assert.Empty(courses);
    }

    [Fact]
    public async Task 课表_演示学生甲的春季只有course004()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        // 钉住种子事实：demo-001 的春季并非空课表（course-004 就在春季）。
        // 上面那条“空课表”用例必须用没选春季课的学生，否则前提与期望自相矛盾。
        var courses = await harness.CourseQuery.GetByTermAsync(ApplicationHarness.SpringTerm, Ct);

        Assert.Equal(new[] { "course-004" }, courses.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task 课表_只返回当前学生选修的课程()
    {
        var harness = ApplicationHarness.Create().WithDemoData();
        harness.Store.Courses.Add(new Course("course-only-b", "学生乙限定课", "", "",
            DayOfWeek.Friday, 11, 12, new[] { 1 }, ApplicationHarness.FallTerm));
        harness.Store.Enrollments.Add(("demo-002", "course-only-b"));

        var courses = await harness.CourseQuery.GetByTermAsync(ApplicationHarness.FallTerm, Ct);

        Assert.DoesNotContain(courses, x => x.Id == "course-only-b");
    }

    [Fact]
    public async Task 成绩_未公布保持null而不是0()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        var grades = await harness.GradeQuery.GetByTermAsync(ApplicationHarness.FallTerm, Ct);

        var unpublished = grades.Single(x => x.CourseId == "course-003");
        Assert.Null(unpublished.Score);
        Assert.Null(unpublished.GradePoint);
        Assert.Equal(3.0m, unpublished.Credits);
    }

    [Fact]
    public async Task 成绩_按课程名升序()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        var grades = await harness.GradeQuery.GetByTermAsync(ApplicationHarness.FallTerm, Ct);
        var names = grades.Select(x => x.CourseName).ToArray();

        Assert.Equal(names.OrderBy(x => x, StringComparer.Ordinal).ToArray(), names);
    }

    [Fact]
    public async Task 考试_未公布时间的排在末尾()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        var exams = await harness.ExamQuery.GetByTermAsync(ApplicationHarness.FallTerm, Ct);

        Assert.Equal(3, exams.Count);
        Assert.All(exams.Take(2), x => Assert.NotNull(x.StartsAt));
        Assert.Null(exams[^1].StartsAt);
        // 有时间的按时间升序：12-21 前于 12-23。
        Assert.Equal("exam-001", exams[0].Id);
        Assert.Equal("exam-002", exams[1].Id);
    }

    [Fact]
    public async Task 学期_按开始日期倒序_最近的在最前()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        var terms = await harness.TermQuery.GetAllAsync(Ct);

        Assert.Equal(ApplicationHarness.SpringTerm, terms[0].Id);
        Assert.Equal(ApplicationHarness.FallTerm, terms[1].Id);
    }

    [Fact]
    public async Task 常用入口_按分组与标题排序()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        var links = await harness.LinkQuery.GetAllAsync(Ct);

        Assert.Equal(2, links.Count);
        Assert.Equal(
            links.Select(x => x.Category).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            links.Select(x => x.Category).ToArray());
    }

    [Fact]
    public async Task 当前资料_恒定标记为演示数据()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        var profile = await harness.ProfileQuery.GetActiveAsync(Ct);

        Assert.Equal("demo-001", profile.Id);
        Assert.True(profile.IsDemo);
    }

    [Fact]
    public async Task 数据状态_最后更新时间首版恒为null()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        var updatedAt = await harness.DataStatusQuery.GetLastUpdatedAsync(Ct);

        Assert.Null(updatedAt);
    }

    [Fact]
    public async Task 活动学生_设置缺失_按数据库未就绪处理()
    {
        var harness = ApplicationHarness.Create().WithDemoData();
        harness.Store.Settings.Remove("active_demo_student_id");

        await Assert.ThrowsAsync<CampusStoreUnavailableException>(
            () => harness.ProfileQuery.GetActiveAsync(Ct));
    }

    [Fact]
    public async Task 活动学生_指向不存在的学生_按数据库未就绪处理()
    {
        var harness = ApplicationHarness.Create().WithDemoData();
        harness.Store.Settings["active_demo_student_id"] = "demo-999";

        await Assert.ThrowsAsync<CampusStoreUnavailableException>(
            () => harness.ProfileQuery.GetActiveAsync(Ct));
    }

    [Fact]
    public async Task 活动学生_可切换到另一个演示学生并看到其独立课表()
    {
        var harness = ApplicationHarness.Create().WithDemoData();

        harness.Store.Settings["active_demo_student_id"] = "demo-002";

        var courses = await harness.CourseQuery.GetByTermAsync(ApplicationHarness.FallTerm, Ct);

        // demo-002 只选了 001/002。
        Assert.Equal(new[] { "course-001", "course-002" }, courses.Select(x => x.Id).ToArray());
    }
}
