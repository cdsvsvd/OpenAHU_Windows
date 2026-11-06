using Microsoft.EntityFrameworkCore;
using OpenAHU.Infrastructure.Entities;

namespace OpenAHU.Infrastructure.Persistence;

internal static class DemoData
{
    // A permanent marker prevents deleting courses from causing the demo set to be restored.
    internal const string SeedMarker = "demo_seed_version";

    internal static void Seed(DbContext context)
    {
        // Tooling also invokes seeding after a downgrade to 0, when application tables no longer exist.
        if (!context.Database.GetAppliedMigrations().Any()) return;
        if (HasAnyData((CampusDbContext)context)) return;
        context.AddRange(CreateEntities());
        context.SaveChanges();
    }

    internal static async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (!(await context.Database.GetAppliedMigrationsAsync(cancellationToken)).Any()) return;
        if (await HasAnyDataAsync((CampusDbContext)context, cancellationToken)) return;
        context.AddRange(CreateEntities());
        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool HasAnyData(CampusDbContext db) =>
        db.AppSettings.Any() || db.Students.Any() || db.Terms.Any() || db.Courses.Any() ||
        db.CourseWeeks.Any() || db.Enrollments.Any() || db.Grades.Any() || db.Exams.Any() || db.CampusLinks.Any();

    private static async Task<bool> HasAnyDataAsync(CampusDbContext db, CancellationToken ct) =>
        await db.AppSettings.AnyAsync(ct) || await db.Students.AnyAsync(ct) || await db.Terms.AnyAsync(ct) ||
        await db.Courses.AnyAsync(ct) || await db.CourseWeeks.AnyAsync(ct) || await db.Enrollments.AnyAsync(ct) ||
        await db.Grades.AnyAsync(ct) || await db.Exams.AnyAsync(ct) || await db.CampusLinks.AnyAsync(ct);

    private static object[] CreateEntities()
    {
        var entities = new List<object>
        {
            new Student { Id = "demo-001", DisplayName = "演示学生甲", StudentNumber = "DEMO001", College = "示例学院", Major = "示例专业", GradeYear = 2024, CreatedAtUtc = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero) },
            new Student { Id = "demo-002", DisplayName = "演示学生乙", StudentNumber = "DEMO002", College = "示例学院", Major = "示例专业", GradeYear = 2025, CreatedAtUtc = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero) },
            new Term { Id = "term-2026-fall", Name = "2026年秋季学期（演示）", StartDate = new(2026, 9, 7), WeekCount = 18, IsCurrent = true },
            new Term { Id = "term-2027-spring", Name = "2027年春季学期（演示）", StartDate = new(2027, 3, 1), WeekCount = 18 },
            new Course { Id = "course-001", TermId = "term-2026-fall", CourseCode = "D-COMP-001", Name = "软件工程实践", Teacher = "示例教师甲", Location = "演示楼A101", DayOfWeek = 1, StartSection = 1, EndSection = 2 },
            new Course { Id = "course-002", TermId = "term-2026-fall", CourseCode = "D-COMP-002", Name = "数据库原理", Teacher = "示例教师乙", Location = "演示楼B202", DayOfWeek = 2, StartSection = 3, EndSection = 4 },
            new Course { Id = "course-003", TermId = "term-2026-fall", CourseCode = "D-COMP-003", Name = "计算机网络", Teacher = "示例教师丙", Location = "演示楼C303", DayOfWeek = 4, StartSection = 5, EndSection = 6 },
            new Course { Id = "course-004", TermId = "term-2027-spring", CourseCode = "D-COMP-004", Name = "程序设计训练", Teacher = "示例教师丁", Location = "演示楼A202", DayOfWeek = 3, StartSection = 1, EndSection = 2 },
            new Grade { StudentId = "demo-001", CourseId = "course-001", Credits = 3m, Score = 88m },
            new Grade { StudentId = "demo-001", CourseId = "course-002", Credits = 3.5m, Score = 91m },
            new Grade { StudentId = "demo-001", CourseId = "course-003", Credits = 3m },
            new Grade { StudentId = "demo-002", CourseId = "course-001", Credits = 3m, Score = 79m },
            new Exam { Id = "exam-001", CourseId = "course-001", StartsAt = new(2026, 12, 21, 9, 0, 0, TimeSpan.FromHours(8)), EndsAt = new(2026, 12, 21, 11, 0, 0, TimeSpan.FromHours(8)), Location = "演示楼D401" },
            new Exam { Id = "exam-002", CourseId = "course-002", StartsAt = new(2026, 12, 23, 14, 0, 0, TimeSpan.FromHours(8)), EndsAt = new(2026, 12, 23, 16, 0, 0, TimeSpan.FromHours(8)), Location = "演示楼D402" },
            new Exam { Id = "exam-003", CourseId = "course-003" },
            new CampusLink { Id = "link-001", Title = "项目仓库", Url = "https://github.com/cdsvsvd/OpenAHU_Windows", Category = "项目" },
            new CampusLink { Id = "link-002", Title = "GitHub", Url = "https://github.com/", Category = "开发" },
            new AppSetting { SettingKey = "active_demo_student_id", SettingValue = "demo-001" },
            new AppSetting { SettingKey = "source", SettingValue = "LocalSQLiteDemo" },
            new AppSetting { SettingKey = "source_label", SettingValue = "本地演示数据，非学校官方信息" },
            new AppSetting { SettingKey = SeedMarker, SettingValue = "v1" }
        };
        foreach (var (course, weeks) in new (string, IEnumerable<int>)[]
        {
            ("course-001", Enumerable.Range(1, 16)), ("course-002", Enumerable.Range(1, 18)),
            ("course-003", Enumerable.Range(1, 8).Select(x => x * 2)), ("course-004", Enumerable.Range(1, 14))
        })
            entities.AddRange(weeks.Select(week => new CourseWeek { CourseId = course, WeekNumber = week }));
        foreach (var course in new[] { "course-001", "course-002", "course-003", "course-004" })
            entities.Add(new Enrollment { StudentId = "demo-001", CourseId = course });
        foreach (var course in new[] { "course-001", "course-002" })
            entities.Add(new Enrollment { StudentId = "demo-002", CourseId = course });
        return entities.ToArray();
    }
}
