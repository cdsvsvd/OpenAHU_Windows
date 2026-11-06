using Microsoft.EntityFrameworkCore;
using OpenAHU.Application.Abstractions;
using OpenAHU.Infrastructure.Persistence;

namespace OpenAHU.Infrastructure.Repositories;

internal sealed class EnrollmentRepository(CampusDbContext db) : IEnrollmentRepository
{
    public Task AddAsync(string studentId, string courseId, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync(async () =>
        {
            StoreAccess.RequireTransaction(db);
            // Target only duplicate enrollment keys; foreign-key/check failures must still be reported.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO enrollments (student_id, course_id) VALUES ({studentId}, {courseId})
                ON CONFLICT (student_id, course_id) DO NOTHING
                """, cancellationToken);
        }, cancellationToken);

    public Task<int> CountByCourseAsync(string courseId, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync(() => db.Enrollments.CountAsync(x => x.CourseId == courseId, cancellationToken), cancellationToken);

    public Task<int> CountOthersByCourseAsync(string courseId, string excludedStudentId, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync(() => db.Enrollments.CountAsync(
            x => x.CourseId == courseId && x.StudentId != excludedStudentId, cancellationToken), cancellationToken);
}
