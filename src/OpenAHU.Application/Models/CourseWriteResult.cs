namespace OpenAHU.Application.Models;

/// <summary>写链路的结果分类。<c>CourseWriteStatus.Ok</c> 以外的值都由 Api 映射为 400/404/409。</summary>
public enum CourseWriteStatus
{
    /// <summary>写入成功。</summary>
    Ok = 0,

    /// <summary>字段校验失败 → 400 <c>invalid_request</c>（含排课时间冲突）。</summary>
    ValidationFailed = 1,

    /// <summary><c>termId</c> 在库内不存在 → 404 <c>not_found</c>。</summary>
    UnknownTerm = 2,

    /// <summary>课程 id 不存在 → 404 <c>not_found</c>。</summary>
    NotFound = 3,

    /// <summary>关联冲突，禁止操作 → 409 <c>conflict</c>。</summary>
    Conflict = 4,
}

/// <summary>409 冲突的具体原因，供 6 号做断言、供前端选择文案。</summary>
public enum CourseConflictReason
{
    /// <summary>课程已有成绩记录（删除会带动 <c>grade_point/score</c> 一起消失）。</summary>
    HasGrades = 0,

    /// <summary>课程已有考试安排。</summary>
    HasExams = 1,
}

/// <summary>
/// 课程写操作（新增/编辑/删除）的业务结果。用显式分类代替异常：
/// 校验失败与关联冲突是<b>可预期的业务结果</b>，必须能被 Api 映射成确定的 400/404/409，
/// 而数据库不可用属于真正的异常（<c>CampusStoreUnavailableException</c> → 503）。
/// </summary>
/// <remarks>
/// 只有静态工厂可构造，因此不存在“状态是 Ok 但没有 CourseId”这类非法组合。
/// </remarks>
public sealed class CourseWriteResult
{
    private CourseWriteResult(
        CourseWriteStatus status,
        string? courseId,
        Course? course,
        string? field,
        string? message,
        int enrolledStudentCount,
        int otherStudentCount,
        CourseConflictReason? conflictReason)
    {
        Status = status;
        CourseId = courseId;
        Course = course;
        Field = field;
        Message = message;
        EnrolledStudentCount = enrolledStudentCount;
        OtherStudentCount = otherStudentCount;
        ConflictReason = conflictReason;
    }

    /// <summary>结果分类。</summary>
    public CourseWriteStatus Status { get; }

    /// <summary>
    /// 被操作的课程 id：<see cref="CourseWriteStatus.Ok"/> 时为生效的 id（新增即后端生成的 id），
    /// <see cref="CourseWriteStatus.NotFound"/> 时回显请求里的 id，其余情况为 <c>null</c>。
    /// </summary>
    public string? CourseId { get; }

    /// <summary>
    /// 写入后的课程完整状态，供 Api 直接映射为 <c>CourseDto</c>。
    /// </summary>
    /// <remarks>
    /// 对应 03 号在 <c>Contracts/README.md</c> 冻结的写接口响应：
    /// <c>POST</c> → 201 + <c>CourseDto</c>、<c>PUT</c> → 200 + 更新后的 <c>CourseDto</c>、<c>DELETE</c> → 204（无响应体）。
    /// 因此新增与编辑会带回课程，删除返回 <c>null</c>（<c>DELETE</c> 不需要响应体）。
    /// </remarks>
    public Course? Course { get; }

    /// <summary>校验失败的字段名，使用 JSON 字段名（<c>name</c>/<c>startSection</c>/<c>weekNumbers</c> …），供前端定位输入框。</summary>
    public string? Field { get; }

    /// <summary>面向用户的文案；<b>不含</b>数据库路径、SQL、连接串。</summary>
    public string? Message { get; }

    /// <summary>操作后（删除时为操作前）该课程的选课人数，供 UI 展示影响面。</summary>
    public int EnrolledStudentCount { get; }

    /// <summary>除当前演示学生外的选课人数。<b>大于 0 时前端必须警示“修改/删除会影响其他演示学生”</b>。</summary>
    public int OtherStudentCount { get; }

    /// <summary>仅当 <see cref="Status"/> 为 <see cref="CourseWriteStatus.Conflict"/> 时有值。</summary>
    public CourseConflictReason? ConflictReason { get; }

    /// <summary>写入成功。</summary>
    /// <param name="courseId">课程 id（新增时为后端生成的新 id）。</param>
    /// <param name="enrolledStudentCount">操作后的选课人数。</param>
    /// <param name="otherStudentCount">除当前演示学生外的选课人数。</param>
    /// <returns>成功结果。</returns>
    public static CourseWriteResult Ok(string courseId, int enrolledStudentCount, int otherStudentCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(courseId);
        return new CourseWriteResult(
            CourseWriteStatus.Ok,
            courseId,
            course: null,
            field: null,
            message: null,
            Math.Max(0, enrolledStudentCount),
            Math.Max(0, otherStudentCount),
            conflictReason: null);
    }

    /// <summary>写入成功，并带回写入后的课程（供 <c>POST</c>/<c>PUT</c> 直接映射 <c>CourseDto</c>）。</summary>
    /// <param name="course">写入后的完整课程。</param>
    /// <param name="enrolledStudentCount">操作后的选课人数。</param>
    /// <param name="otherStudentCount">除当前演示学生外的选课人数。</param>
    /// <returns>成功结果。</returns>
    public static CourseWriteResult Ok(Course course, int enrolledStudentCount, int otherStudentCount)
    {
        ArgumentNullException.ThrowIfNull(course);
        ArgumentException.ThrowIfNullOrWhiteSpace(course.Id);
        return new CourseWriteResult(
            CourseWriteStatus.Ok,
            course.Id,
            course,
            field: null,
            message: null,
            Math.Max(0, enrolledStudentCount),
            Math.Max(0, otherStudentCount),
            conflictReason: null);
    }

    /// <summary>字段校验失败（含排课时间冲突）。</summary>
    /// <param name="field">JSON 字段名。</param>
    /// <param name="message">面向用户的说明。</param>
    /// <returns>400 对应的结果。</returns>
    public static CourseWriteResult ValidationFailed(string field, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new CourseWriteResult(
            CourseWriteStatus.ValidationFailed,
            courseId: null,
            course: null,
            field,
            message,
            enrolledStudentCount: 0,
            otherStudentCount: 0,
            conflictReason: null);
    }

    /// <summary>学期不存在。</summary>
    /// <param name="termId">请求里的学期 id。</param>
    /// <returns>404 对应的结果。</returns>
    public static CourseWriteResult UnknownTerm(string termId)
    {
        return new CourseWriteResult(
            CourseWriteStatus.UnknownTerm,
            courseId: null,
            course: null,
            field: "termId",
            message: "所选学期不存在，请刷新学期列表后重试。",
            enrolledStudentCount: 0,
            otherStudentCount: 0,
            conflictReason: null);
    }

    /// <summary>课程不存在（或不在当前演示学生的课表里）。</summary>
    /// <param name="courseId">请求里的课程 id。</param>
    /// <returns>404 对应的结果。</returns>
    public static CourseWriteResult NotFound(string courseId)
    {
        return new CourseWriteResult(
            CourseWriteStatus.NotFound,
            courseId,
            course: null,
            field: null,
            message: "课程不存在，可能已被删除，请刷新课表。",
            enrolledStudentCount: 0,
            otherStudentCount: 0,
            conflictReason: null);
    }

    /// <summary>关联冲突，操作被拒绝。</summary>
    /// <param name="reason">冲突原因。</param>
    /// <param name="message">面向用户的说明，需说明“为什么不能删”和“可以怎么做”。</param>
    /// <returns>409 对应的结果。</returns>
    public static CourseWriteResult Conflict(CourseConflictReason reason, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new CourseWriteResult(
            CourseWriteStatus.Conflict,
            courseId: null,
            course: null,
            field: null,
            message,
            enrolledStudentCount: 0,
            otherStudentCount: 0,
            conflictReason: reason);
    }
}
