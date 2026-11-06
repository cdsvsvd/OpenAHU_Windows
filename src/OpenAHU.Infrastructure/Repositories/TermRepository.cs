using Microsoft.EntityFrameworkCore;
using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Models;
using OpenAHU.Infrastructure.Persistence;

namespace OpenAHU.Infrastructure.Repositories;

internal sealed class TermRepository(CampusDbContext db) : ITermRepository
{
    public Task<IReadOnlyList<Term>> GetAllAsync(CancellationToken cancellationToken) =>
        StoreAccess.RunAsync<IReadOnlyList<Term>>(async () => await db.Terms.AsNoTracking()
            .Select(x => new Term(x.Id, x.Name, x.StartDate, x.WeekCount, x.IsCurrent))
            .ToArrayAsync(cancellationToken), cancellationToken);

    public Task<Term?> FindAsync(string termId, CancellationToken cancellationToken) =>
        StoreAccess.RunAsync(() => db.Terms.AsNoTracking().Where(x => x.Id == termId)
            .Select(x => new Term(x.Id, x.Name, x.StartDate, x.WeekCount, x.IsCurrent))
            .SingleOrDefaultAsync(cancellationToken), cancellationToken);
}
