using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Abstractions.Queries;
using OpenAHU.Application.Models;

namespace OpenAHU.Application.Services;

/// <summary>常用入口查询。<see cref="ILinkQuery"/> 的唯一实现。</summary>
public sealed class LinkQuery : ILinkQuery
{
    private readonly ILinkRepository _links;

    /// <summary>创建查询服务。</summary>
    /// <param name="links">链接仓储。</param>
    public LinkQuery(ILinkRepository links)
    {
        _links = links;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CampusLink>> GetAllAsync(CancellationToken cancellationToken)
    {
        var links = await _links.GetAllAsync(cancellationToken);

        return links
            .OrderBy(link => link.Category, StringComparer.Ordinal)
            .ThenBy(link => link.Title, StringComparer.Ordinal)
            .ToArray();
    }
}
