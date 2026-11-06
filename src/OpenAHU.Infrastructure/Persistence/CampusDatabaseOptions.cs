using Microsoft.Data.Sqlite;

namespace OpenAHU.Infrastructure.Persistence;

public sealed class CampusDatabaseOptions
{
    public string DatabasePath { get; init; } = DefaultPath;
    public int BusyTimeoutSeconds { get; init; } = 5;
    public int InitializationTimeoutSeconds { get; init; } = 30;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAHU", "campus.db");

    internal string FullPath => Path.GetFullPath(Environment.ExpandEnvironmentVariables(DatabasePath));

    internal string ConnectionString(SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = FullPath,
            Mode = mode,
            ForeignKeys = true,
            Pooling = true,
            DefaultTimeout = BusyTimeoutSeconds
        }.ToString();

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(DatabasePath) ||
            !Path.IsPathFullyQualified(Environment.ExpandEnvironmentVariables(DatabasePath)) ||
            string.IsNullOrEmpty(Path.GetFileName(DatabasePath)))
            throw new ArgumentException("数据库路径必须是专用目录内的绝对文件路径。", nameof(DatabasePath));
        if (BusyTimeoutSeconds is < 1 or > 60 || InitializationTimeoutSeconds is < 1 or > 300)
            throw new ArgumentOutOfRangeException(nameof(BusyTimeoutSeconds), "数据库超时配置超出允许范围。");
    }
}
