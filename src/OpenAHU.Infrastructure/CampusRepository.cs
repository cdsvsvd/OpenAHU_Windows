using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenAHU.Application;
using OpenAHU.Contracts.Dtos;
using OpenAHU.Contracts.Local;

namespace OpenAHU.Infrastructure;

/// <summary>负责所有数据库访问。API与Application均不执行SQL。</summary>
public sealed class CampusRepository : ICampusRepository
{
    private readonly CampusDbContext _db;
    public CampusRepository(CampusDbContext db) => _db = db;

    private static CampusException Error(CampusErrorKind kind, string message) => new(kind, message);
    private static async Task<T> Guard<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (DbUpdateConcurrencyException) { throw Error(CampusErrorKind.Conflict, "记录已被修改，请刷新后重试。"); }
        catch (DbUpdateException e) when (e.InnerException is SqliteException s && s.SqliteErrorCode == 19)
        { throw Error(CampusErrorKind.Conflict, "数据约束冲突，请检查课程关联或输入内容。"); }
        catch (SqliteException e) when (e.SqliteErrorCode == 19)
        { throw Error(CampusErrorKind.Conflict, "数据约束冲突，请检查课程关联或输入内容。"); }
        catch (Exception e) when (e is DbUpdateException or SqliteException or InvalidOperationException or IOException or UnauthorizedAccessException)
        { throw Error(CampusErrorKind.DbUnavailable, "本地数据库暂时不可用，请稍后重试。"); }
    }
    private async Task<string> ActiveStudentAsync(CancellationToken ct)
    {
        var sid = await _db.AppSettings.AsNoTracking()
            .Where(x=>x.SettingKey=="active_demo_student_id").Select(x=>x.SettingValue).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(sid) || !await _db.Students.AnyAsync(x=>x.Id==sid,ct))
            throw Error(CampusErrorKind.DbUnavailable, "本地演示档案缺失，请检查初始化数据。");
        return sid;
    }

    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        try
        {
            // An orphaned active-student setting must not mark the store healthy.
            return await _db.Database.CanConnectAsync(ct)
                && await (from setting in _db.AppSettings.AsNoTracking()
                          join student in _db.Students.AsNoTracking()
                            on setting.SettingValue equals student.Id
                          where setting.SettingKey=="active_demo_student_id"
                          select student.Id).AnyAsync(ct);
        }
        catch (Exception e) when (e is SqliteException or DbUpdateException or InvalidOperationException)
        { return false; }
    }

    public Task<StudentProfileDto?> GetProfileAsync(CancellationToken ct) => Guard<StudentProfileDto?>(async () =>
    {
        var sid=await ActiveStudentAsync(ct);
        var s=await _db.Students.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==sid,ct);
        return s is null ? null : new StudentProfileDto(s.Id,s.DisplayName,s.College,s.Major,s.GradeYear,true);
    });

    public Task<IReadOnlyList<TermDto>> GetTermsAsync(CancellationToken ct) => Guard<IReadOnlyList<TermDto>>(async () =>
    {
        var terms=await _db.Terms.AsNoTracking().OrderByDescending(x=>x.StartDate).ToListAsync(ct);
        return terms.Select(x=>new TermDto(x.Id,x.Name,DateOnly.ParseExact(x.StartDate,"yyyy-MM-dd",CultureInfo.InvariantCulture),x.WeekCount,x.IsCurrent)).ToList();
    });

    public Task<TermDto?> GetTermAsync(string id,CancellationToken ct) => Guard<TermDto?>(async () =>
    {
        var x=await _db.Terms.AsNoTracking().FirstOrDefaultAsync(t=>t.Id==id,ct);
        return x is null ? null : new TermDto(x.Id,x.Name,DateOnly.ParseExact(x.StartDate,"yyyy-MM-dd",CultureInfo.InvariantCulture),x.WeekCount,x.IsCurrent);
    });

    private static CourseDto Map(Course c) => new(c.Id,c.Name,c.Teacher,c.Location,
        (DayOfWeek)(c.DayOfWeek % 7),c.StartSection,c.EndSection,
        c.Weeks.Select(w=>w.WeekNumber).OrderBy(x=>x).ToArray(),c.TermId);

    public Task<IReadOnlyList<CourseDto>> GetCoursesAsync(string termId,CancellationToken ct) => Guard<IReadOnlyList<CourseDto>>(async () =>
    {
        var sid=await ActiveStudentAsync(ct);
        var values=await _db.Courses.AsNoTracking().Include(c=>c.Weeks)
            .Where(c=>c.TermId==termId && c.Enrollments.Any(e=>e.StudentId==sid))
            .OrderBy(c=>c.DayOfWeek).ThenBy(c=>c.StartSection).ThenBy(c=>c.Name).ToListAsync(ct);
        return values.Select(Map).ToList();
    });

    public Task<IReadOnlyList<GradeDto>> GetGradesAsync(string termId,CancellationToken ct) => Guard<IReadOnlyList<GradeDto>>(async () =>
    {
        var sid=await ActiveStudentAsync(ct);
        var rows=await (from grade in _db.Grades.AsNoTracking()
            join course in _db.Courses.AsNoTracking() on grade.CourseId equals course.Id
            where grade.StudentId==sid && course.TermId==termId
            orderby course.Name
            select new {grade,course}).ToListAsync(ct);
        return rows.Select(x=>new GradeDto(x.course.Id,x.course.Name,(decimal)x.grade.Credits,
            x.grade.Score is null?null:(decimal?)x.grade.Score.Value,
            x.grade.GradePoint is null?null:(decimal?)x.grade.GradePoint.Value,x.course.TermId)).ToList();
    });

    public Task<IReadOnlyList<ExamDto>> GetExamsAsync(string termId,CancellationToken ct) => Guard<IReadOnlyList<ExamDto>>(async () =>
    {
        var sid=await ActiveStudentAsync(ct);
        var rows=await (from exam in _db.Exams.AsNoTracking()
            join course in _db.Courses.AsNoTracking() on exam.CourseId equals course.Id
            join enrollment in _db.Enrollments.AsNoTracking() on course.Id equals enrollment.CourseId
            where course.TermId==termId && enrollment.StudentId==sid
            select new {exam,course}).ToListAsync(ct);
        return rows.Select(x=>new ExamDto(x.exam.Id,x.course.Name,
            x.exam.StartsAt is null?null:DateTimeOffset.Parse(x.exam.StartsAt,CultureInfo.InvariantCulture),
            x.exam.EndsAt is null?null:DateTimeOffset.Parse(x.exam.EndsAt,CultureInfo.InvariantCulture),
            x.exam.Location,x.course.TermId))
            .OrderBy(x=>x.StartsAt is null).ThenBy(x=>x.StartsAt).ThenBy(x=>x.CourseName).ToList();
    });

    public Task<IReadOnlyList<LinkDto>> GetLinksAsync(CancellationToken ct) => Guard<IReadOnlyList<LinkDto>>(async () =>
    {
        var rows=await _db.CampusLinks.AsNoTracking().OrderBy(x=>x.Category).ThenBy(x=>x.Title).ToListAsync(ct);
        return rows.Select(x=>new LinkDto(x.Id,x.Title,x.Url,x.Category)).ToList();
    });

    private async Task EnsureNoScheduleOverlapAsync(string sid,string? exceptId,CourseUpsertRequest r,CancellationToken ct)
    {
        var dbDay=r.DayOfWeek==DayOfWeek.Sunday?7:(int)r.DayOfWeek;
        var candidates=await _db.Courses.Include(x=>x.Weeks)
            .Where(x=>x.TermId==r.TermId && x.DayOfWeek==dbDay && x.Id!=exceptId &&
                x.StartSection<=r.EndSection && x.EndSection>=r.StartSection &&
                x.Enrollments.Any(e=>e.StudentId==sid)).ToListAsync(ct);
        if (candidates.Any(x=>x.Weeks.Any(w=>r.WeekNumbers.Contains(w.WeekNumber))))
            throw Error(CampusErrorKind.Conflict, "所选时段与已有课程存在上课周次冲突。");
    }

    public Task<CourseDto> CreateCourseAsync(CourseUpsertRequest r,CancellationToken ct) => Guard(async () =>
    {
        var sid=await ActiveStudentAsync(ct);
        await EnsureNoScheduleOverlapAsync(sid,null,r,ct);
        await using var trx=await _db.Database.BeginTransactionAsync(ct);
        var course=new Course {
            Id="local-"+Guid.NewGuid().ToString("N"),TermId=r.TermId,Name=r.Name.Trim(),
            Teacher=r.Teacher.Trim(),Location=r.Location.Trim(),
            DayOfWeek=r.DayOfWeek==DayOfWeek.Sunday?7:(int)r.DayOfWeek,
            StartSection=r.StartSection,EndSection=r.EndSection,
            Weeks=r.WeekNumbers.OrderBy(x=>x).Select(x=>new CourseWeek {WeekNumber=x}).ToList(),
            Enrollments=new List<Enrollment> {new(){StudentId=sid}}
        };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync(ct);
        await trx.CommitAsync(ct);
        return Map(course);
    });

    private async Task<Course> RequireOwnCourseAsync(string sid,string id,CancellationToken ct)
    {
        // /local/courses is only permitted to mutate user-created local records.
        if (!id.StartsWith("local-",StringComparison.Ordinal))
            throw Error(CampusErrorKind.NotFound,"未找到可修改的本地课程。");
        var course=await _db.Courses.Include(x=>x.Weeks).Include(x=>x.Enrollments)
            .FirstOrDefaultAsync(x=>x.Id==id && x.Enrollments.Any(e=>e.StudentId==sid),ct);
        if (course is null) throw Error(CampusErrorKind.NotFound,"未找到本机演示资料中的该课程。");
        if (course.Enrollments.Count>1)
            throw Error(CampusErrorKind.Conflict,"此课程关联多个演示学生，请先建立个人课程，不允许修改共享课程。");
        return course;
    }

    public Task<CourseDto> UpdateCourseAsync(string id,CourseUpsertRequest r,CancellationToken ct) => Guard(async () =>
    {
        var sid=await ActiveStudentAsync(ct);
        var c=await RequireOwnCourseAsync(sid,id,ct);
        if (c.TermId!=r.TermId && (await _db.Grades.AnyAsync(g=>g.CourseId==id,ct) ||
                                      await _db.Exams.AnyAsync(e=>e.CourseId==id,ct)))
            throw Error(CampusErrorKind.Conflict,"该课程已有成绩或考试，不能更换学期。");
        await EnsureNoScheduleOverlapAsync(sid,id,r,ct);
        await using var trx=await _db.Database.BeginTransactionAsync(ct);
        c.TermId=r.TermId;c.Name=r.Name.Trim();c.Teacher=r.Teacher.Trim();c.Location=r.Location.Trim();
        c.DayOfWeek=r.DayOfWeek==DayOfWeek.Sunday?7:(int)r.DayOfWeek;
        c.StartSection=r.StartSection;c.EndSection=r.EndSection;
        // Replacing the aggregate as one unit avoids EF tracking collisions on
        // repeated edits with overlapping week keys in the same scope.
        var wanted=r.WeekNumbers.OrderBy(w=>w).ToArray();
        foreach(var trackedWeek in c.Weeks.ToArray())
            _db.Entry(trackedWeek).State=EntityState.Detached;
        c.Weeks.Clear();
        await _db.CourseWeeks.Where(w=>w.CourseId==id).ExecuteDeleteAsync(ct);
        foreach(var week in wanted)
            c.Weeks.Add(new CourseWeek {CourseId=id,WeekNumber=week});
        await _db.SaveChangesAsync(ct);
        await trx.CommitAsync(ct);
        return Map(c);
    });

    public async Task DeleteCourseAsync(string id,CancellationToken ct)
    {
        await Guard(async () =>
    {
        var sid=await ActiveStudentAsync(ct);
        var course=await RequireOwnCourseAsync(sid,id,ct);
        if (await _db.Grades.AnyAsync(x=>x.CourseId==id,ct) ||
            await _db.Exams.AnyAsync(x=>x.CourseId==id,ct))
            throw Error(CampusErrorKind.Conflict,"该课程仍有成绩或考试记录，请先处理关联记录。");
        await using var trx=await _db.Database.BeginTransactionAsync(ct);
        // Atomic predicate protects newly added grades/exams or enrollments
        // between the application's precheck and the actual DELETE statement.
        var affected=await _db.Courses.Where(c=>c.Id==id
            && c.Enrollments.Any(e=>e.StudentId==sid)
            && !c.Enrollments.Any(e=>e.StudentId!=sid)
            && !_db.Grades.Any(g=>g.CourseId==c.Id)
            && !_db.Exams.Any(e=>e.CourseId==c.Id))
            .ExecuteDeleteAsync(ct);
        if (affected!=1)
        {
            if (await _db.Courses.AsNoTracking().AnyAsync(c=>c.Id==id,ct))
                throw Error(CampusErrorKind.Conflict,"课程关联数据已变化，已取消删除，请刷新后重试。");
            throw Error(CampusErrorKind.NotFound,"课程已不存在，请刷新后重试。");
        }
        await trx.CommitAsync(ct);
        _db.ChangeTracker.Clear(); // ExecuteDelete bypasses the change tracker.
        return true;
        });
    }
}
