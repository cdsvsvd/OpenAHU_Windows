namespace OpenAHU.Application.Errors;

/// <summary>
/// 本地数据库不可用：文件被占用、只读、迁移未完成、尚未初始化演示资料。
/// Api 层映射为 <c>503</c> + <c>db_unavailable</c>。
/// </summary>
/// <remarks>
/// 存在的意义是让 Api <b>不必引用 EF Core</b> 也能区分“数据库挂了”和“代码 bug”：
/// 05 号在仓储实现里把 <c>SqliteException</c> / <c>DbUpdateException</c> 包装成本异常，
/// 04 号在业务层原样放行，03 号只做一次类型映射。参见 INTERFACE_PLAN.md 先决问题 B（采异常方案）。
/// </remarks>
public sealed class CampusStoreUnavailableException : Exception
{
    /// <summary>创建异常。</summary>
    /// <param name="message">面向日志与排障的说明；不得包含数据库绝对路径。</param>
    /// <param name="innerException">底层异常（仅用于日志，不进入响应体）。</param>
    public CampusStoreUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
