using Microsoft.EntityFrameworkCore;

namespace OpenAHU.Infrastructure;

/// <summary>首次建库运行迁移并录入完全虚构的演示资料；已有库不被清空或重复灌入。</summary>
public static class DemoDatabaseInitializer
{
    public static async Task InitializeAsync(CampusDbContext db, CancellationToken ct)
    {
        if (db.Database.IsSqlite())
            await db.Database.MigrateAsync(ct);
        // SQL Server schema is provisioned explicitly by sqlserver/01_Create_CampusDemo.sql.
        // Never run the SQLite-only initial migration against SQL Server.
        // Do not restore deliberately deleted demo records or overwrite partly filled databases.
        // The marker and the demo records are written in the same transaction below.
        if (await db.AppSettings.AnyAsync(x => x.SettingKey == "demo_seed_version", ct)) return;
        if (await db.Students.AnyAsync(ct) || await db.Terms.AnyAsync(ct) ||
            await db.Courses.AnyAsync(ct) || await db.CourseWeeks.AnyAsync(ct) ||
            await db.Enrollments.AnyAsync(ct) || await db.Grades.AnyAsync(ct) ||
            await db.Exams.AnyAsync(ct) || await db.CampusLinks.AnyAsync(ct) ||
            await db.AppSettings.AnyAsync(ct)) return;
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        db.Students.AddRange(
            new Student {Id="demo-001",DisplayName="演示学生甲",StudentNumber="DEMO001",College="示例学院",Major="示例专业",GradeYear=2024,CreatedAtUtc="2026-11-02T00:00:00Z"},
            new Student {Id="demo-002",DisplayName="演示学生乙",StudentNumber="DEMO002",College="示例学院",Major="示例专业",GradeYear=2025,CreatedAtUtc="2026-11-02T00:00:00Z"});
        db.Terms.AddRange(
            new Term {Id="term-2026-fall",Name="2026年秋季学期（演示）",StartDate="2026-09-07",WeekCount=18,IsCurrent=true},
            new Term {Id="term-2027-spring",Name="2027年春季学期（演示）",StartDate="2027-03-01",WeekCount=18,IsCurrent=false});
        db.Courses.AddRange(
            DemoCourse("course-001","term-2026-fall","D-COMP-001","软件工程实践","示例教师甲","演示楼A101",1,1,2,Enumerable.Range(1,16)),
            DemoCourse("course-002","term-2026-fall","D-COMP-002","数据库原理","示例教师乙","演示楼B202",2,3,4,Enumerable.Range(1,18)),
            DemoCourse("course-003","term-2026-fall","D-COMP-003","计算机网络","示例教师丙","演示楼C303",4,5,6,Enumerable.Range(1,8).Select(x=>x*2)),
            DemoCourse("course-004","term-2027-spring","D-COMP-004","程序设计训练","示例教师丁","演示楼A202",3,1,2,Enumerable.Range(1,14)));
        await db.SaveChangesAsync(ct);
        db.Enrollments.AddRange(
            new Enrollment {StudentId="demo-001",CourseId="course-001"},
            new Enrollment {StudentId="demo-001",CourseId="course-002"},
            new Enrollment {StudentId="demo-001",CourseId="course-003"},
            new Enrollment {StudentId="demo-001",CourseId="course-004"},
            new Enrollment {StudentId="demo-002",CourseId="course-001"},
            new Enrollment {StudentId="demo-002",CourseId="course-002"});
        await db.SaveChangesAsync(ct);
        db.Grades.AddRange(
            new Grade {StudentId="demo-001",CourseId="course-001",Credits=3,Score=88},
            new Grade {StudentId="demo-001",CourseId="course-002",Credits=3.5,Score=91},
            new Grade {StudentId="demo-001",CourseId="course-003",Credits=3,Score=null},
            new Grade {StudentId="demo-002",CourseId="course-001",Credits=3,Score=79});
        db.Exams.AddRange(
            new Exam {Id="exam-001",CourseId="course-001",StartsAt="2026-12-21T09:00:00+08:00",EndsAt="2026-12-21T11:00:00+08:00",Location="演示楼D401"},
            new Exam {Id="exam-002",CourseId="course-002",StartsAt="2026-12-23T14:00:00+08:00",EndsAt="2026-12-23T16:00:00+08:00",Location="演示楼D402"},
            new Exam {Id="exam-003",CourseId="course-003"});
        db.CampusLinks.AddRange(
            new CampusLink {Id="link-001",Title="项目仓库",Url="https://github.com/cdsvsvd/OpenAHU_Windows",Category="项目"},
            new CampusLink {Id="link-002",Title="GitHub",Url="https://github.com/",Category="开发"});
        db.AppSettings.AddRange(
            new AppSetting {SettingKey="active_demo_student_id",SettingValue="demo-001"},
            new AppSetting {SettingKey="source",SettingValue=db.Database.IsSqlServer()?"LocalSqlServerDemo":"LocalSQLiteDemo"},
            new AppSetting {SettingKey="source_label",SettingValue="本地演示数据，非学校官方信息"},
            new AppSetting {SettingKey="demo_seed_version",SettingValue="v1"});
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    private static Course DemoCourse(string id,string termId,string code,string name,string teacher,string location,int day,int first,int last,IEnumerable<int> weeks) => new()
    {
        Id=id,TermId=termId,CourseCode=code,Name=name,Teacher=teacher,Location=location,DayOfWeek=day,
        StartSection=first,EndSection=last,Weeks=weeks.Select(w=>new CourseWeek {CourseId=id,WeekNumber=w}).ToList()
    };
}
