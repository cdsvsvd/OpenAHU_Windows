using OpenAHU.Application.Models;

namespace OpenAHU.Application.Abstractions;

/// <summary>考试（<c>exams</c> 表）访问。</summary>
public interface IExamRepository
{
    /// <summary>
    /// 读取某学生某学期的考试安排（关联路径 <c>students → enrollments → courses → exams</c>）。
    /// </summary>
    /// <remarks>
    /// 只返回该学生选修课程的考试：未选课者的考试不得出现。
    /// <c>starts_at</c>/<c>ends_at</c> 为 <c>NULL</c> 时返回 <c>null</c>；带偏移的 ISO 文本解析为 <see cref="DateTimeOffset"/>。
    /// 排序由业务层负责（有时间的在前，未公布排末尾）。
    /// </remarks>
    /// <param name="studentId">学生主键。</param>
    /// <param name="termId">学期主键。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>考试列表；顺序不保证。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<IReadOnlyList<Exam>> GetByStudentAndTermAsync(
        string studentId,
        string termId,
        CancellationToken cancellationToken);

    /// <summary>判断某课程是否存在考试安排。</summary>
    /// <remarks>为 <c>true</c> 时删除课程必须被拒绝（409 <c>conflict</c>）。</remarks>
    /// <param name="courseId">课程主键。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>是否存在。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<bool> ExistsByCourseAsync(string courseId, CancellationToken cancellationToken);
}
