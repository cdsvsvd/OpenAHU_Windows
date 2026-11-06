using OpenAHU.Application.Abstractions.Queries;
using OpenAHU.Application.Models;

namespace OpenAHU.Application.Services;

/// <summary>当前演示资料查询。<see cref="IProfileQuery"/> 的唯一实现。</summary>
public sealed class ProfileQuery : IProfileQuery
{
    private readonly ActiveStudentProvider _activeStudent;

    /// <summary>创建查询服务。</summary>
    /// <param name="activeStudent">演示资料解析器。</param>
    public ProfileQuery(ActiveStudentProvider activeStudent)
    {
        _activeStudent = activeStudent;
    }

    /// <inheritdoc />
    public async Task<CurrentProfile> GetActiveAsync(CancellationToken cancellationToken)
    {
        var student = await _activeStudent.LoadAsync(cancellationToken);
        return CurrentProfile.From(student);
    }
}
