using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Abstractions.Queries;
using OpenAHU.Application.Models;

namespace OpenAHU.Application.Services;

/// <summary>课表查询。<see cref="ICourseQuery"/> 的唯一实现。</summary>
public sealed class CourseQuery : ICourseQuery
{
    private readonly ITermRepository _terms;
    private readonly ICourseRepository _courses;
    private readonly ActiveStudentProvider _activeStudent;

    /// <summary>创建查询服务。</summary>
    /// <param name="terms">学期仓储。</param>
    /// <param name="courses">课程仓储。</param>
    /// <param name="activeStudent">演示资料解析器。</param>
    public CourseQuery(ITermRepository terms, ICourseRepository courses, ActiveStudentProvider activeStudent)
    {
        _terms = terms;
        _courses = courses;
        _activeStudent = activeStudent;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Course>> GetByTermAsync(string termId, CancellationToken cancellationToken)
    {
        // 1) 学期必须存在：未知 termId 是 404，不是“空课表”。空课表只由“该学期没选课”产生。
        var term = await _terms.RequireAsync(termId, cancellationToken);

        // 2) 身份由后端活动演示资料决定，不接受外部 studentId。
        var student = await _activeStudent.LoadAsync(cancellationToken);

        var courses = await _courses.GetByStudentAndTermAsync(student.Id, term.Id, cancellationToken);

        // 3) 业务层保证输出形状：周次升序去重、按星期→节次→课程名排序。
        //    仓储约定“顺序不保证”，所以这里不能省略排序，否则前端课表格子会随机跳动。
        //    注意不能直接用 (int)DayOfWeek 排序：那是 周日=0，会把周日排到周一前面。
        //    数据字典要求的是 周一→周日，所以换算成“周一=0 … 周日=6”。
        return courses
            .Select(course => course with { WeekNumbers = WeekNumberNormalizer.Normalize(course.WeekNumbers) })
            .OrderBy(course => MondayFirstIndex(course.DayOfWeek))
            .ThenBy(course => course.StartSection)
            .ThenBy(course => course.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>把 C# 星期（周日=0）换算成“周一=0 … 周日=6”的排序键。</summary>
    /// <param name="dayOfWeek">上课星期。</param>
    /// <returns>排序键。</returns>
    private static int MondayFirstIndex(DayOfWeek dayOfWeek)
    {
        return ((int)dayOfWeek + 6) % 7;
    }
}
