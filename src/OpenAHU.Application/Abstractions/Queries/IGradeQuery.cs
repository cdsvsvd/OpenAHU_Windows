using OpenAHU.Application.Models;

namespace OpenAHU.Application.Abstractions.Queries;

/// <summary>成绩查询用例（<c>GET /api/v1/grades?termId=...</c>）。</summary>
public interface IGradeQuery
{
    /// <summary>
    /// 读取当前演示学生在指定学期的成绩，按课程名升序排列。
    /// </summary>
    /// <remarks>
    /// 未公布成绩保持 <c>null</c>，<b>不代入 0</b>，也不计算 GPA。
    /// </remarks>
    /// <param name="termId">学期主键。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>成绩列表；无记录时返回空列表。</returns>
    /// <exception cref="Errors.CampusResourceNotFoundException">学期不存在 → 404。</exception>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用 → 503。</exception>
    Task<IReadOnlyList<Grade>> GetByTermAsync(string termId, CancellationToken cancellationToken);
}
