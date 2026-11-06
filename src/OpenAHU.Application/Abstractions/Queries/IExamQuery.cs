using OpenAHU.Application.Models;

namespace OpenAHU.Application.Abstractions.Queries;

/// <summary>考试查询用例（<c>GET /api/v1/exams?termId=...</c>）。</summary>
public interface IExamQuery
{
    /// <summary>
    /// 读取当前演示学生在指定学期的考试安排：<b>有时间的按时间升序在前，未公布的排末尾</b>（组内按课程名升序）。
    /// </summary>
    /// <param name="termId">学期主键。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>考试列表；无记录时返回空列表。</returns>
    /// <exception cref="Errors.CampusResourceNotFoundException">学期不存在 → 404。</exception>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用 → 503。</exception>
    Task<IReadOnlyList<Exam>> GetByTermAsync(string termId, CancellationToken cancellationToken);
}
