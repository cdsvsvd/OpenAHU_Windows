using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Errors;
using OpenAHU.Application.Models;

namespace OpenAHU.Application.Services;

/// <summary>
/// 解析“当前本机演示资料”。身份<b>只</b>来自 <c>app_settings.active_demo_student_id</c>，
/// 任何接口都不接受前端传入的 studentId —— 这是文档里反复强调的数据隔离底线。
/// </summary>
/// <remarks>
/// 该服务被 5 个查询用例和写链路共用，避免每处各写一遍“设置 → 学生”的两段式查找。
/// </remarks>
public sealed class ActiveStudentProvider
{
    private readonly IAppSettingsRepository _settings;
    private readonly IStudentRepository _students;

    /// <summary>创建解析器。</summary>
    /// <param name="settings">设置仓储。</param>
    /// <param name="students">学生仓储。</param>
    public ActiveStudentProvider(IAppSettingsRepository settings, IStudentRepository students)
    {
        _settings = settings;
        _students = students;
    }

    /// <summary>读取当前演示学生。</summary>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>学生资料。</returns>
    /// <exception cref="CampusStoreUnavailableException">
    /// 设置缺失（库未初始化）或指向的学生不存在（设置与数据不一致）。
    /// 这两种情况都意味着“本地数据还没准备好”，按 503 处理，而不是 404。
    /// </exception>
    public async Task<Student> LoadAsync(CancellationToken cancellationToken)
    {
        var studentId = await _settings.GetActiveDemoStudentIdAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(studentId))
        {
            throw new CampusStoreUnavailableException(
                "本地数据库缺少 app_settings.active_demo_student_id，演示资料尚未初始化。");
        }

        var student = await _students.FindAsync(studentId, cancellationToken);
        if (student is null)
        {
            throw new CampusStoreUnavailableException(
                "app_settings.active_demo_student_id 指向的学生不存在，演示资料与设置不一致。");
        }

        return student;
    }
}
