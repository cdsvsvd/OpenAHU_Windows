namespace OpenAHU.Application.Models;

/// <summary>
/// 课程新增/编辑进入业务层的输入（由 Api 从 <c>CourseUpsertRequest</c> 映射而来）。
/// </summary>
/// <remarks>
/// 与 <see cref="Course"/> 的区别：<b>没有 <c>Id</c></b> —— 新增由 Application 生成主键，编辑以路径参数为准。
/// 字段刻意允许为 <c>null</c>：JSON 反序列化可能给出缺失值，判空属业务校验
/// （<c>CourseWriteValidator</c>），不靠可空注解兜底。
/// </remarks>
/// <param name="Name">课程名称，必填。</param>
/// <param name="Teacher">任课教师，可空字符串。</param>
/// <param name="Location">上课地点，可空字符串。</param>
/// <param name="DayOfWeek">上课星期，C# 语义（周日=0）。</param>
/// <param name="StartSection">起始节次 1–20。</param>
/// <param name="EndSection">结束节次，不小于起始节次且不超过 20。</param>
/// <param name="WeekNumbers">开课周次，去重后非空，且必须落在学期 <c>WeekCount</c> 内。</param>
/// <param name="TermId">所属学期，必须存在于库内。</param>
public sealed record CourseDraft(
    string? Name,
    string? Teacher,
    string? Location,
    DayOfWeek DayOfWeek,
    int StartSection,
    int EndSection,
    IReadOnlyList<int>? WeekNumbers,
    string? TermId);
