using OpenAHU.Application.Models;

namespace OpenAHU.Application.Abstractions.Commands;

/// <summary>
/// 本地课程维护写用例（<c>POST/PUT/DELETE /api/v1/local/courses[/{id}]</c>）。
/// </summary>
/// <remarks>
/// 三个方法都<b>不抛</b>可预期的业务错误，而是返回 <see cref="CourseWriteResult"/> 分类结果；
/// 只有数据库不可用才抛 <see cref="Errors.CampusStoreUnavailableException"/>（→ 503）。
/// 事务边界统一由 <see cref="IUnitOfWork"/> 保证。
/// </remarks>
public interface ICourseCommand
{
    /// <summary>
    /// 新增一门本地课程，并自动为<b>当前演示学生</b>建立选课关系。
    /// </summary>
    /// <remarks>
    /// 课程主键由本层生成；返回结果里带新 id，前端用它刷新课表。
    /// 不建立选课关系会导致新增课程在课表中不可见。
    /// </remarks>
    /// <param name="draft">课程字段。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>成功时 <see cref="CourseWriteStatus.Ok"/> 且带新 id。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<CourseWriteResult> CreateAsync(CourseDraft draft, CancellationToken cancellationToken);

    /// <summary>
    /// 编辑一门课程（含整体替换开课周次）。
    /// </summary>
    /// <remarks>
    /// <b>影响面</b>：<c>courses</c> 是共享行，若该课还有其他学生选修（<c>OtherStudentCount &gt; 0</c>），
    /// 修改会同步改变他们的课表。本层不阻止该操作，但结果里返回影响人数供前端警示。
    /// </remarks>
    /// <param name="courseId">课程主键。</param>
    /// <param name="draft">课程字段。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>课程不存在 → <see cref="CourseWriteStatus.NotFound"/>。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<CourseWriteResult> UpdateAsync(string courseId, CourseDraft draft, CancellationToken cancellationToken);

    /// <summary>
    /// 删除一门课程。已有成绩或考试时拒绝删除（409）。
    /// </summary>
    /// <param name="courseId">课程主键。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>冲突时 <see cref="CourseWriteStatus.Conflict"/> 且带 <see cref="CourseWriteResult.ConflictReason"/>。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<CourseWriteResult> DeleteAsync(string courseId, CancellationToken cancellationToken);
}
