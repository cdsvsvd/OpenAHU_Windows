using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace OpenAHU.Infrastructure;

/// <summary>
/// Read-only check before migrations: never adopt unrelated databases, downgrade a newer
/// schema or modify an incomplete EF-managed database. Only a missing/empty database is new.
/// </summary>
public static class DatabaseCompatibilityGuard
{
    private static readonly HashSet<string> RequiredTables = new(StringComparer.Ordinal)
    {
        "students", "terms", "courses", "course_weeks", "enrollments",
        "grades", "exams", "campus_links", "app_settings"
    };

    public static async Task AssertCompatibleAsync(
        CampusDbContext context, string databasePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(databasePath)) return;

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
            ForeignKeys = true
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var tables = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                tables.Add(reader.GetString(0));
        }
        if (tables.Count == 0 || tables.SetEquals(new[] { "__EFMigrationsLock" }))
            return;

        if (!tables.Contains("__EFMigrationsHistory"))
            throw new InvalidOperationException("数据库不属于本应用已管理的迁移版本，拒绝自动接管或覆盖。");

        var applied = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                applied.Add(reader.GetString(0));
        }
        var known = context.Database.GetMigrations().ToArray();
        if (known.Length == 0 || applied.Count > known.Length ||
            !applied.SequenceEqual(known.Take(applied.Count)))
            throw new InvalidOperationException("发现高于或偏离当前程序的数据库迁移版本，拒绝自动降级。");

        if (applied.Count == 0 &&
            tables.Any(t => t is not "__EFMigrationsHistory" and not "__EFMigrationsLock"))
            throw new InvalidOperationException("数据库内已有业务表但缺少迁移版本记录，拒绝自动修改。");
        if (applied.Count > 0 && !RequiredTables.IsSubsetOf(tables))
            throw new InvalidOperationException("数据库迁移历史存在但业务表不完整，请人工核对备份。");

        // Existing managed databases are checked read-only before migration.
        await using (var quickCheck=connection.CreateCommand())
        {
            quickCheck.CommandText="PRAGMA quick_check";
            var value=await quickCheck.ExecuteScalarAsync(cancellationToken);
            if (!string.Equals(value as string,"ok",StringComparison.Ordinal))
                throw new InvalidOperationException("SQLite完整性检查未通过，拒绝迁移。");
        }
        await using (var foreignKeys=connection.CreateCommand())
        {
            foreignKeys.CommandText="PRAGMA foreign_key_check";
            await using var reader=await foreignKeys.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("SQLite外键检查未通过，拒绝迁移。");
        }
    }
}
