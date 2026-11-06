using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Abstractions.Queries;
using OpenAHU.Application.Models;

namespace OpenAHU.Application.Services;

/// <summary>考试查询。<see cref="IExamQuery"/> 的唯一实现。</summary>
public sealed class ExamQuery : IExamQuery
{
    private readonly ITermRepository _terms;
    private readonly IExamRepository _exams;
    private readonly ActiveStudentProvider _activeStudent;

    /// <summary>创建查询服务。</summary>
    /// <param name="terms">学期仓储。</param>
    /// <param name="exams">考试仓储。</param>
    /// <param name="activeStudent">演示资料解析器。</param>
    public ExamQuery(ITermRepository terms, IExamRepository exams, ActiveStudentProvider activeStudent)
    {
        _terms = terms;
        _exams = exams;
        _activeStudent = activeStudent;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Exam>> GetByTermAsync(string termId, CancellationToken cancellationToken)
    {
        var term = await _terms.RequireAsync(termId, cancellationToken);
        var student = await _activeStudent.LoadAsync(cancellationToken);

        var exams = await _exams.GetByStudentAndTermAsync(student.Id, term.Id, cancellationToken);

        // 已公布时间的按时间升序在前；未公布的（StartsAt 为 null）整体排末尾，组内按课程名，
        // 避免“未公布”的考试插在已公布考试中间让用户误以为漏排。
        return exams
            .OrderBy(exam => exam.StartsAt.HasValue ? 0 : 1)
            .ThenBy(exam => exam.StartsAt ?? DateTimeOffset.MaxValue)
            .ThenBy(exam => exam.CourseName, StringComparer.Ordinal)
            .ToArray();
    }
}
