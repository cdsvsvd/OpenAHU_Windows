using OpenAHU.Application.Models;

namespace OpenAHU.Application.Abstractions.Queries;

/// <summary>课表查询用例（<c>GET /api/v1/courses?termId=...</c>）。</summary>
public interface ICourseQuery
{
    /// <summary>
    /// 读取当前演示学生在指定学期的课表，按 <c>DayOfWeek → StartSection → Name</c> 升序排列。
    /// </summary>
    /// <param name="termId">学期主键。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>课程列表；该学期无课时返回空列表（不是 404）。</returns>
    /// <exception cref="Errors.CampusResourceNotFoundException">学期不存在 → 404。</exception>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用 → 503。</exception>
    Task<IReadOnlyList<Course>> GetByTermAsync(string termId, CancellationToken cancellationToken);
}
