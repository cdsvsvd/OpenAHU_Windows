using OpenAHU.Application.Models;

namespace OpenAHU.Application.Abstractions;

/// <summary>学期（<c>terms</c> 表）访问。</summary>
public interface ITermRepository
{
    /// <summary>读取全部学期。<b>不排序</b>，排序由业务层决定。</summary>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>学期列表；库为空时返回空列表，不返回 <c>null</c>。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<IReadOnlyList<Term>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>按主键读取一个学期。</summary>
    /// <param name="termId">学期主键。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>学期；不存在时返回 <c>null</c>。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<Term?> FindAsync(string termId, CancellationToken cancellationToken);
}
