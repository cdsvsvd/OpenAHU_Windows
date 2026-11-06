namespace OpenAHU.Application.Abstractions;

/// <summary>
/// 选课关系（<c>enrollments</c>）。课程与学生的多对多关联，也是“删除/编辑影响面”的唯一数据来源。
/// </summary>
public interface IEnrollmentRepository
{
    /// <summary>
    /// 为某学生建立一条选课关系。
    /// </summary>
    /// <remarks>
    /// 必须在事务内调用。新增本地课程后<b>必须</b>调用本方法，
    /// 否则该课程不会出现在任何学生的课表里（课表按选修关系过滤）。
    /// 若关系已存在，应静默成功（幂等），不要抛唯一约束异常。
    /// </remarks>
    /// <param name="studentId">学生主键。</param>
    /// <param name="courseId">课程主键。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task AddAsync(string studentId, string courseId, CancellationToken cancellationToken);

    /// <summary>统计选修某课程的学生总数（用于删除前的影响面提示）。</summary>
    /// <param name="courseId">课程主键。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>人数；无人选修时为 0。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<int> CountByCourseAsync(string courseId, CancellationToken cancellationToken);

    /// <summary>统计选修某课程、但<b>不是</b>当前演示学生的其他学生人数。</summary>
    /// <remarks>结果大于 0 表示编辑/删除会波及其他演示学生的课表，前端须警示。</remarks>
    /// <param name="courseId">课程主键。</param>
    /// <param name="excludedStudentId">要排除的学生（当前演示资料）。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>其他学生人数。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<int> CountOthersByCourseAsync(
        string courseId,
        string excludedStudentId,
        CancellationToken cancellationToken);
}
