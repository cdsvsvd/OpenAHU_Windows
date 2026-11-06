using Microsoft.EntityFrameworkCore;
using OpenAHU.Application.Abstractions;
using OpenAHU.Infrastructure.Persistence;

namespace OpenAHU.Infrastructure.Repositories;

internal sealed class AppSettingsRepository(CampusDbContext db) : IAppSettingsRepository
{
    public Task<string?> GetActiveDemoStudentIdAsync(CancellationToken cancellationToken) =>
        StoreAccess.RunAsync(() => db.AppSettings.AsNoTracking()
            .Where(x => x.SettingKey == "active_demo_student_id")
            .Select(x => x.SettingValue).SingleOrDefaultAsync(cancellationToken), cancellationToken);
}
