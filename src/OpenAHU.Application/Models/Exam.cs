namespace OpenAHU.Application.Models;

/// <summary>
/// 一场考试安排（<c>students → enrollments → courses → exams</c>）。
/// </summary>
/// <remarks>
/// 未公布的考试允许时间与地点全为 <c>null</c>；排序规则见 <c>ExamQuery</c>（有时间的在前，其余排末尾）。
/// </remarks>
/// <param name="Id">考试主键。</param>
/// <param name="CourseName">课程名称。</param>
/// <param name="StartsAt">开考时间（带偏移）；未公布为 <c>null</c>。</param>
/// <param name="EndsAt">结束时间；未公布为 <c>null</c>。</param>
/// <param name="Location">考场；未公布为 <c>null</c>。</param>
/// <param name="TermId">课程所属学期主键。</param>
public sealed record Exam(
    string Id,
    string CourseName,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    string? Location,
    string TermId);
