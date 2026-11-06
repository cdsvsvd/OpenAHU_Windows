using Microsoft.EntityFrameworkCore;
using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Models;
using OpenAHU.Infrastructure.Persistence;

namespace OpenAHU.Infrastructure.Repositories;

internal sealed class StudentRepository(CampusDbContext db) : IStudentRepository
{
    public Task<Student?> FindAsync(string studentId, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync(() => db.Students.AsNoTracking().Where(x => x.Id == studentId)
            .Select(x => new Student(x.Id, x.DisplayName, x.College, x.Major, x.GradeYear))
            .SingleOrDefaultAsync(cancellationToken), cancellationToken);
}
