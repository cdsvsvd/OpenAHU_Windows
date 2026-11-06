namespace OpenAHU.Application.Abstractions;

/// <summary>
/// 本机设置（<c>app_settings</c>）。只暴露业务真正需要的一项：
/// 当前激活的演示学生，身份由后端决定，<b>不接受</b>前端传入的 studentId。
/// </summary>
public interface IAppSettingsRepository
{
    /// <summary>
    /// 读取 <c>app_settings.active_demo_student_id</c>。
    /// </summary>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>学生 id；键不存在时返回 <c>null</c>（由业务层判定为“尚未初始化”）。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<string?> GetActiveDemoStudentIdAsync(CancellationToken cancellationToken);
}
