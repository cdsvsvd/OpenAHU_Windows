using OpenAHU.Application.Models;

namespace OpenAHU.Application.Abstractions;

/// <summary>
/// 课程与开课周次（<c>courses</c> + <c>course_weeks</c>）的读写访问。
/// </summary>
/// <remarks>
/// <b>写方法必须在事务中调用</b>（见 <see cref="IUnitOfWork"/>）：
/// 一次新增要落 <c>courses</c> 行 + 多条 <c>course_weeks</c>，一次编辑要整表替换周次，
/// 任何一步失败都不得留下半条课程。
/// </remarks>
public interface ICourseRepository
{
    /// <summary>
    /// 读取某学生某学期选修的全部课程（含各自周次）。
    /// </summary>
    /// <remarks>
    /// 关联路径固定为 <c>students → enrollments → courses → course_weeks</c>；
    /// 周次需按 <c>week_number</c> 升序聚合进 <see cref="Course.WeekNumbers"/>。
    /// 未选修任何课程时返回空列表。
    /// </remarks>
    /// <param name="studentId">学生主键（由后端活动演示资料决定）。</param>
    /// <param name="termId">学期主键。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>课程列表；<b>顺序不保证</b>，排序由业务层负责。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<IReadOnlyList<Course>> GetByStudentAndTermAsync(
        string studentId,
        string termId,
        CancellationToken cancellationToken);

    /// <summary>按主键读取一门课程（含周次），不校验选修关系。</summary>
    /// <param name="courseId">课程主键。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>课程；不存在时返回 <c>null</c>。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<Course?> FindAsync(string courseId, CancellationToken cancellationToken);

    /// <summary>
    /// 插入一门课程及其全部周次。主键由调用方生成（<see cref="Course.Id"/> 已赋值）。
    /// </summary>
    /// <remarks>必须在事务内调用。不负责建立选课关系，选课由 <see cref="IEnrollmentRepository.AddAsync"/> 单独写入。</remarks>
    /// <param name="course">要写入的课程，<see cref="Course.WeekNumbers"/> 非空且升序去重。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task AddAsync(Course course, CancellationToken cancellationToken);

    /// <summary>
    /// 更新课程行并用 <see cref="Course.WeekNumbers"/> <b>整体替换</b>该课的周次集合。
    /// </summary>
    /// <remarks>
    /// 必须在事务内调用。替换语义：删除该课原有 <c>course_weeks</c> 行后按新集合重插，
    /// 不要做差量比对（同一周次可能被“先删后加”，差量实现容易漏）。
    /// 不修改 <c>enrollments</c>：编辑课程不会改变谁在选修它。
    /// </remarks>
    /// <param name="course">已包含原 <c>Id</c> 与最终字段值的课程。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task UpdateAsync(Course course, CancellationToken cancellationToken);

    /// <summary>
    /// 删除课程。<c>course_weeks</c> / <c>enrollments</c> 随之删除（数据库已定义 <c>ON DELETE CASCADE</c>）。
    /// </summary>
    /// <remarks>
    /// 必须在事务内调用。调用方（<c>CourseCommand</c>）已先行校验“无成绩、无考试”，
    /// 因此这里<b>不需要</b>再判断冲突；但实现仍应保证失败时整体回滚。
    /// </remarks>
    /// <param name="courseId">课程主键。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>是否确实删除了记录（不存在时返回 <c>false</c>）。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<bool> DeleteAsync(string courseId, CancellationToken cancellationToken);
}
