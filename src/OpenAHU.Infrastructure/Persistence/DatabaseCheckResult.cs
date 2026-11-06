namespace OpenAHU.Infrastructure.Persistence;

// Infrastructure diagnostic result only; this is not an HTTP DTO or an Application contract.
public sealed record DatabaseCheckResult(bool IsReady, DatabaseState State, string Message)
{
    internal static DatabaseCheckResult Ready { get; } = new(true, DatabaseState.Ready, "本地数据库已就绪。");
    internal static DatabaseCheckResult Unavailable { get; } = new(false, DatabaseState.Unavailable, "本地数据库暂不可用，请检查文件权限或稍后重试。");
    internal static DatabaseCheckResult Incompatible { get; } = new(false, DatabaseState.IncompatibleSchema, "数据库版本不兼容或未经迁移管理，请先备份并检查升级说明。");
    internal static DatabaseCheckResult Invalid { get; } = new(false, DatabaseState.InvalidData, "本地数据校验未通过，请先备份并检查数据。");
    internal static DatabaseCheckResult Missing { get; } = new(false, DatabaseState.NotInitialized, "本地数据库尚未初始化。");
}

public enum DatabaseState { Ready, NotInitialized, Unavailable, IncompatibleSchema, InvalidData }
