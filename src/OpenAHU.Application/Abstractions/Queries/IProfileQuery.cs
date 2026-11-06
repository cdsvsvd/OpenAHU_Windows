using OpenAHU.Application.Models;

namespace OpenAHU.Application.Abstractions.Queries;

/// <summary>当前本机演示资料的查询用例（<c>GET /api/v1/profile</c>）。</summary>
public interface IProfileQuery
{
    /// <summary>读取当前激活的演示资料。</summary>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>演示档案，<c>IsDemo</c> 恒为 <c>true</c>。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">
    /// 数据库不可用，或库中缺少 <c>active_demo_student_id</c>／对应学生（视为“尚未初始化”）。
    /// </exception>
    Task<CurrentProfile> GetActiveAsync(CancellationToken cancellationToken);
}
