using OpenAHU.Application.Models;

namespace OpenAHU.Application.Abstractions;

/// <summary>成绩（<c>grades</c> 表）访问。</summary>
public interface IGradeRepository
{
    /// <summary>
    /// 读取某学生某学期的全部成绩（关联路径 <c>students → enrollments → courses → grades</c>）。
    /// </summary>
    /// <remarks>
    /// 学分来自 <c>grades.credits</c>，课程名与所属学期来自 <c>courses</c>；
    /// <c>score</c>/<c>grade_point</c> 为 <c>NULL</c> 时必须原样返回 <c>null</c>，<b>不得填 0</b>。
    /// 金额/学分一律用 <see cref="decimal"/> 承载 <c>REAL</c>。
    /// </remarks>
    /// <param name="studentId">学生主键。</param>
    /// <param name="termId">学期主键。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>成绩列表；顺序不保证。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<IReadOnlyList<Grade>> GetByStudentAndTermAsync(
        string studentId,
        string termId,
        CancellationToken cancellationToken);

    /// <summary>判断某课程是否存在任何成绩记录（不区分学生）。</summary>
    /// <remarks>为 <c>true</c> 时删除课程必须被拒绝（409 <c>conflict</c>）。</remarks>
    /// <param name="courseId">课程主键。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>是否存在。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<bool> ExistsByCourseAsync(string courseId, CancellationToken cancellationToken);
}
