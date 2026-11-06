using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Models;
using OpenAHU.Application.Services;

namespace OpenAHU.Application.Tests.Fakes;

/// <summary>
/// 组装"全部假仓储 + 被测业务服务"的测试夹具。
/// 种子数据刻意复刻演示库的关键形态：一学期 18 周、demo-002 与 demo-001 共享 course-001/002、
/// course-003 有成绩但分数未公布、exam-003 未公布时间。这样业务规则才有意义。
/// </summary>
internal sealed class ApplicationHarness
{
    private ApplicationHarness()
    {
        Store = new FakeStore();

        AppSettings = new FakeAppSettingsRepository(Store);
        Students = new FakeStudentRepository(Store);
        Terms = new FakeTermRepository(Store);
        Courses = new FakeCourseRepository(Store);
        Enrollments = new FakeEnrollmentRepository(Store);
        Grades = new FakeGradeRepository(Store);
        Exams = new FakeExamRepository(Store);
        Links = new FakeLinkRepository(Store);
        UnitOfWork = new FakeUnitOfWork(Store);

        ActiveStudent = new ActiveStudentProvider(AppSettings, Students);
        CourseCommand = new CourseCommand(Terms, Courses, Enrollments, Grades, Exams, UnitOfWork, ActiveStudent);
        CourseQuery = new CourseQuery(Terms, Courses, ActiveStudent);
        GradeQuery = new GradeQuery(Terms, Grades, ActiveStudent);
        ExamQuery = new ExamQuery(Terms, Exams, ActiveStudent);
        TermQuery = new TermQuery(Terms);
        ProfileQuery = new ProfileQuery(ActiveStudent);
        LinkQuery = new LinkQuery(Links);
        DataStatusQuery = new DataStatusQuery();
    }

    public FakeStore Store { get; }

    public FakeAppSettingsRepository AppSettings { get; }

    public FakeStudentRepository Students { get; }

    public FakeTermRepository Terms { get; }

    public FakeCourseRepository Courses { get; }

    public FakeEnrollmentRepository Enrollments { get; }

    public FakeGradeRepository Grades { get; }

    public FakeExamRepository Exams { get; }

    public FakeLinkRepository Links { get; }

    public FakeUnitOfWork UnitOfWork { get; }

    public ActiveStudentProvider ActiveStudent { get; }

    public IUnitOfWork Uow => UnitOfWork;

    public CourseCommand CourseCommand { get; }

    public CourseQuery CourseQuery { get; }

    public GradeQuery GradeQuery { get; }

    public ExamQuery ExamQuery { get; }

    public TermQuery TermQuery { get; }

    public ProfileQuery ProfileQuery { get; }

    public LinkQuery LinkQuery { get; }

    public DataStatusQuery DataStatusQuery { get; }

    public const string FallTerm = "term-2026-fall";

    public const string SpringTerm = "term-2027-spring";

    public static ApplicationHarness Create() => new();

    /// <summary>建立与演示库同形的种子数据。</summary>
    /// <returns>已装好数据的夹具。</returns>
    public ApplicationHarness WithDemoData()
    {
        Store.Students.Add(new Student("demo-001", "演示学生甲", "示例学院", "示例专业", 2024));
        Store.Students.Add(new Student("demo-002", "演示学生乙", "示例学院", "示例专业", 2025));

        Store.Terms.Add(new Term(FallTerm, "2026年秋季学期（演示）", new DateOnly(2026, 9, 7), 18, true));
        Store.Terms.Add(new Term(SpringTerm, "2027年春季学期（演示）", new DateOnly(2027, 3, 1), 18, false));

        // 故意把周次写成乱序，验证读路径会规范化。
        Store.Courses.Add(new Course("course-001", "软件工程实践", "示例教师甲", "演示楼A101",
            DayOfWeek.Monday, 1, 2, new[] { 3, 1, 2, 3 }, FallTerm));
        Store.Courses.Add(new Course("course-002", "数据库原理", "示例教师乙", "演示楼B202",
            DayOfWeek.Tuesday, 3, 4, new[] { 1, 2 }, FallTerm));
        Store.Courses.Add(new Course("course-003", "计算机网络", "示例教师丙", "演示楼C303",
            DayOfWeek.Thursday, 5, 6, new[] { 2, 4, 6 }, FallTerm));
        Store.Courses.Add(new Course("course-004", "程序设计训练", "示例教师丁", "演示楼A202",
            DayOfWeek.Wednesday, 1, 2, new[] { 1 }, SpringTerm));

        foreach (var courseId in new[] { "course-001", "course-002", "course-003", "course-004" })
        {
            Store.Enrollments.Add(("demo-001", courseId));
        }

        Store.Enrollments.Add(("demo-002", "course-001"));
        Store.Enrollments.Add(("demo-002", "course-002"));

        Store.Grades.Add(new Grade("course-001", "软件工程实践", 3.0m, 88.0m, null, FallTerm));
        Store.Grades.Add(new Grade("course-002", "数据库原理", 3.5m, 91.0m, null, FallTerm));
        Store.Grades.Add(new Grade("course-003", "计算机网络", 3.0m, null, null, FallTerm));

        Store.Exams.Add(("course-003", new Exam("exam-003", "计算机网络", null, null, null, FallTerm)));
        Store.Exams.Add(("course-001", new Exam("exam-001", "软件工程实践",
            new DateTimeOffset(2026, 12, 21, 9, 0, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 12, 21, 11, 0, 0, TimeSpan.FromHours(8)),
            "演示楼D401", FallTerm)));
        Store.Exams.Add(("course-002", new Exam("exam-002", "数据库原理",
            new DateTimeOffset(2026, 12, 23, 14, 0, 0, TimeSpan.FromHours(8)), null, null, FallTerm)));

        Store.Links.Add(new CampusLink("link-002", "GitHub", "https://github.com/", "开发"));
        Store.Links.Add(new CampusLink("link-001", "项目仓库", "https://github.com/cdsvsvd/OpenAHU_Windows", "项目"));

        Store.Settings["active_demo_student_id"] = "demo-001";
        Store.Settings["source"] = "LocalSQLiteDemo";

        return this;
    }

    /// <summary>构造一个合法的新增请求。</summary>
    /// <param name="name">课程名。</param>
    /// <param name="weeks">周次。</param>
    /// <returns>草稿。</returns>
    public static CourseDraft Draft(
        string name = "数据结构（演示）",
        IReadOnlyList<int>? weeks = null,
        DayOfWeek dayOfWeek = DayOfWeek.Friday,
        int startSection = 7,
        int endSection = 8,
        string termId = FallTerm)
    {
        return new CourseDraft(name, "示例教师戊", "演示楼E501", dayOfWeek,
            startSection, endSection, weeks ?? new[] { 1, 2, 3 }, termId);
    }
}
