using OpenAHU.Application.Models;

namespace OpenAHU.Application.Abstractions;

/// <summary>本地常用入口（<c>campus_links</c> 表）访问。</summary>
public interface ILinkRepository
{
    /// <summary>读取全部本地入口。<b>不排序</b>，排序由业务层决定。</summary>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>链接列表；库为空时返回空列表。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<IReadOnlyList<CampusLink>> GetAllAsync(CancellationToken cancellationToken);
}
