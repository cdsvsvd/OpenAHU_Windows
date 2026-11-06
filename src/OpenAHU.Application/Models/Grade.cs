namespace OpenAHU.Application.Models;

/// <summary>
/// 一条成绩记录（<c>students → enrollments → courses → grades</c>）。
/// </summary>
/// <remarks>
/// 只转述数据库中的演示分数：<see cref="Score"/>、<see cref="GradePoint"/> 为 <c>null</c> 时表示未公布，
/// **不得以 0 代入**，也不得据此推导任何“官方绩点”。参见 INTERFACE_PLAN.md §4。
/// </remarks>
/// <param name="CourseId">课程主键。</param>
/// <param name="CourseName">课程名称，便于前端直接展示。</param>
/// <param name="Credits">学分，库约束 0–50；用 <see cref="decimal"/> 承载 <c>REAL</c>，不用 <c>double</c>。</param>
/// <param name="Score">百分制分数；<c>null</c> 表示未公布。</param>
/// <param name="GradePoint">绩点；<c>null</c> 表示未公布或未提供。</param>
/// <param name="TermId">课程所属学期（来自 <c>courses.term_id</c>）。</param>
public sealed record Grade(
    string CourseId,
    string CourseName,
    decimal Credits,
    decimal? Score,
    decimal? GradePoint,
    string TermId);
