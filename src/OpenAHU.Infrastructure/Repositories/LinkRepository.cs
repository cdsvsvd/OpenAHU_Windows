using Microsoft.EntityFrameworkCore;
using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Models;
using OpenAHU.Infrastructure.Persistence;

namespace OpenAHU.Infrastructure.Repositories;

internal sealed class LinkRepository(CampusDbContext db) : ILinkRepository
{
    public Task<IReadOnlyList<CampusLink>> GetAllAsync(CancellationToken cancellationToken) =>
        StoreAccess.RunAsync<IReadOnlyList<CampusLink>>(async () => await db.CampusLinks.AsNoTracking()
            .Select(x => new CampusLink(x.Id, x.Title, x.Url, x.Category))
            .ToArrayAsync(cancellationToken), cancellationToken);
}
