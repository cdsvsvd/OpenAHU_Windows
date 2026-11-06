using OpenAHU.Application.Models;

namespace OpenAHU.Application.Abstractions.Queries;

/// <summary>常用入口查询用例（<c>GET /api/v1/links</c>）。</summary>
public interface ILinkQuery
{
    /// <summary>读取全部本地入口，按分组、标题升序。</summary>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>链接列表；库为空时返回空列表。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<IReadOnlyList<CampusLink>> GetAllAsync(CancellationToken cancellationToken);
}
