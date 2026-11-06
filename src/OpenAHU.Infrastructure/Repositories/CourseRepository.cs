using Microsoft.EntityFrameworkCore;
using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Errors;
using OpenAHU.Infrastructure.Persistence;
using CourseModel = OpenAHU.Application.Models.Course;
using CourseEntity = OpenAHU.Infrastructure.Entities.Course;
using CourseWeek = OpenAHU.Infrastructure.Entities.CourseWeek;

namespace OpenAHU.Infrastructure.Repositories;

internal sealed class CourseRepository(CampusDbContext db) : ICourseRepository
{
    public Task<IReadOnlyList<CourseModel>> GetByStudentAndTermAsync(
        string studentId, string termId, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync<IReadOnlyList<CourseModel>>(async () =>
        {
            var rows = await db.Courses.AsNoTracking().Include(x => x.Weeks)
                .Where(x => x.TermId == termId && x.Enrollments.Any(e => e.StudentId == studentId))
                .ToArrayAsync(cancellationToken);
            return rows.Select(ToModel).ToArray();
        }, cancellationToken);

    public Task<CourseModel?> FindAsync(string courseId, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync(async () =>
        {
            var row = await db.Courses.AsNoTracking().Include(x => x.Weeks)
                .SingleOrDefaultAsync(x => x.Id == courseId, cancellationToken);
            return row is null ? null : ToModel(row);
        }, cancellationToken);

    public Task AddAsync(CourseModel course, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync(async () =>
        {
            StoreAccess.RequireTransaction(db);
            var row = new CourseEntity { Id = course.Id };
            ApplyFields(row, course);
            foreach (var week in course.WeekNumbers)
                row.Weeks.Add(new CourseWeek { CourseId = course.Id, WeekNumber = week });
            db.Courses.Add(row);
            await db.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

    public Task UpdateAsync(CourseModel course, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync(async () =>
        {
            StoreAccess.RequireTransaction(db);
            var row = await db.Courses.SingleOrDefaultAsync(x => x.Id == course.Id, cancellationToken);
            if (row is null)
                throw new CampusStoreUnavailableException("课程在保存前已发生变化，请刷新后重试。");

            // Raw set deletion bypasses EF tracking. Detach any weeks left by an earlier write in
            // the same scope before reinserting overlapping keys (e.g. [1,3] -> [1,5]).
            foreach (var entry in db.ChangeTracker.Entries<CourseWeek>().Where(x => x.Entity.CourseId == course.Id).ToArray())
                entry.State = EntityState.Detached;
            row.Weeks.Clear();
            await db.CourseWeeks.Where(x => x.CourseId == course.Id).ExecuteDeleteAsync(cancellationToken);
            ApplyFields(row, course);
            db.CourseWeeks.AddRange(course.WeekNumbers.Select(week =>
                new CourseWeek { CourseId = course.Id, WeekNumber = week }));
            await db.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

    public Task<bool> DeleteAsync(string courseId, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync(async () =>
        {
            StoreAccess.RequireTransaction(db);
            // Application owns the normal conflict response. This atomic predicate also protects
            // records added after its precheck but before this write transaction started.
            var deleted = await db.Courses.Where(x => x.Id == courseId &&
                !db.Grades.Any(g => g.CourseId == x.Id) && !db.Exams.Any(e => e.CourseId == x.Id))
                .ExecuteDeleteAsync(cancellationToken);
            if (deleted == 0 && await db.Courses.AnyAsync(x => x.Id == courseId, cancellationToken))
                throw new CampusStoreUnavailableException("课程关联数据已发生变化，已取消删除，请刷新后重试。");
            // All repository writes are flushed immediately; invalidate the old tracked graph after a bulk delete.
            db.ChangeTracker.Clear();
            return deleted != 0;
        }, cancellationToken);

    private static CourseModel ToModel(CourseEntity row) => new(
        row.Id, row.Name, row.Teacher, row.Location, (DayOfWeek)(row.DayOfWeek % 7),
        row.StartSection, row.EndSection, row.Weeks.Select(x => x.WeekNumber).Order().ToArray(), row.TermId);

    private static void ApplyFields(CourseEntity row, CourseModel course)
    {
        row.Name = course.Name;
        row.Teacher = course.Teacher;
        row.Location = course.Location;
        row.DayOfWeek = course.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)course.DayOfWeek;
        row.StartSection = course.StartSection;
        row.EndSection = course.EndSection;
        row.TermId = course.TermId;
        // CourseCode is not editable through the Application model; preserve the existing value.
    }
}
