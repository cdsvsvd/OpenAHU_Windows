using Microsoft.EntityFrameworkCore;
using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Models;
using OpenAHU.Infrastructure.Persistence;

namespace OpenAHU.Infrastructure.Repositories;

internal sealed class ExamRepository(CampusDbContext db) : IExamRepository
{
    public Task<IReadOnlyList<Exam>> GetByStudentAndTermAsync(
        string studentId, string termId, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync<IReadOnlyList<Exam>>(async () => await db.Exams.AsNoTracking()
            .Where(x => x.Course.TermId == termId && x.Course.Enrollments.Any(e => e.StudentId == studentId))
            .Select(x => new Exam(x.Id, x.Course.Name, x.StartsAt, x.EndsAt, x.Location, x.Course.TermId))
            .ToArrayAsync(cancellationToken), cancellationToken);

    public Task<bool> ExistsByCourseAsync(string courseId, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync(() => db.Exams.AnyAsync(x => x.CourseId == courseId, cancellationToken), cancellationToken);
}
