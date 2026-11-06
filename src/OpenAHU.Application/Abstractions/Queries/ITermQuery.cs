using OpenAHU.Application.Models;

namespace OpenAHU.Application.Abstractions.Queries;

/// <summary>学期列表查询用例（<c>GET /api/v1/terms</c>）。</summary>
public interface ITermQuery
{
    /// <summary>读取全部学期，按开始日期倒序（最近的学期在前），同日期按名称升序。</summary>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>学期列表；库为空时返回空列表。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<IReadOnlyList<Term>> GetAllAsync(CancellationToken cancellationToken);
}
