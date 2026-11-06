using Microsoft.EntityFrameworkCore;
using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Models;
using OpenAHU.Infrastructure.Persistence;

namespace OpenAHU.Infrastructure.Repositories;

internal sealed class GradeRepository(CampusDbContext db) : IGradeRepository
{
    public Task<IReadOnlyList<Grade>> GetByStudentAndTermAsync(
        string studentId, string termId, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync<IReadOnlyList<Grade>>(async () => await db.Grades.AsNoTracking()
            .Where(x => x.StudentId == studentId && x.Enrollment.Course.TermId == termId)
            .Select(x => new Grade(x.CourseId, x.Enrollment.Course.Name, x.Credits,
                x.Score, x.GradePoint, x.Enrollment.Course.TermId))
            .ToArrayAsync(cancellationToken), cancellationToken);

    public Task<bool> ExistsByCourseAsync(string courseId, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync(() => db.Grades.AnyAsync(x => x.CourseId == courseId, cancellationToken), cancellationToken);
}
