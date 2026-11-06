using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Abstractions.Queries;
using OpenAHU.Application.Models;

namespace OpenAHU.Application.Services;

/// <summary>成绩查询。<see cref="IGradeQuery"/> 的唯一实现。</summary>
public sealed class GradeQuery : IGradeQuery
{
    private readonly ITermRepository _terms;
    private readonly IGradeRepository _grades;
    private readonly ActiveStudentProvider _activeStudent;

    /// <summary>创建查询服务。</summary>
    /// <param name="terms">学期仓储。</param>
    /// <param name="grades">成绩仓储。</param>
    /// <param name="activeStudent">演示资料解析器。</param>
    public GradeQuery(ITermRepository terms, IGradeRepository grades, ActiveStudentProvider activeStudent)
    {
        _terms = terms;
        _grades = grades;
        _activeStudent = activeStudent;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Grade>> GetByTermAsync(string termId, CancellationToken cancellationToken)
    {
        var term = await _terms.RequireAsync(termId, cancellationToken);
        var student = await _activeStudent.LoadAsync(cancellationToken);

        var grades = await _grades.GetByStudentAndTermAsync(student.Id, term.Id, cancellationToken);

        // 空成绩原样保留：这里不做任何 ?? 0 的兜底，也不做绩点换算（文档禁止宣称官方算法）。
        return grades
            .OrderBy(grade => grade.CourseName, StringComparer.Ordinal)
            .ToArray();
    }
}
