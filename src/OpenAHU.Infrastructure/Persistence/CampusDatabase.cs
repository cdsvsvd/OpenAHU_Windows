using System.Data.Common;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace OpenAHU.Infrastructure.Persistence;

public sealed class CampusDatabase(
    IDbContextFactory<CampusDbContext> factory,
    CampusDatabaseOptions options)
{
    private static readonly HashSet<string> RequiredTables = new(StringComparer.Ordinal)
    {
        "students", "terms", "courses", "course_weeks", "enrollments", "grades", "exams", "campus_links", "app_settings"
    };

    public async Task<DatabaseCheckResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.InitializationTimeoutSeconds));
        var ct = timeout.Token;
        try
        {
            PrepareDirectory();
            await using var db = await factory.CreateDbContextAsync(ct);
            await db.Database.OpenConnectionAsync(ct);
            var connection = db.Database.GetDbConnection();
            var known = db.Database.GetMigrations().ToArray();
            var versionFailure = await CheckSchemaAsync(connection, known, requireCurrent: false, ct);
            if (versionFailure is not null) return versionFailure;
            // Migrations, not EnsureCreated or the reference SQL, own the runtime schema.
            // Both synchronous and asynchronous seed delegates run under EF's migration lock.
            await db.Database.MigrateAsync(ct);
            return await CheckConnectionAsync(connection, known, ct);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DatabaseCheckResult.Unavailable;
        }
        catch (Exception ex) when (IsStoreFailure(ex))
        {
            return DatabaseCheckResult.Unavailable;
        }
    }

    public async Task<DatabaseCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(options.FullPath)) return DatabaseCheckResult.Missing;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.InitializationTimeoutSeconds));
        try
        {
            // A probe must not create a missing database or execute migrations/seed.
            await using var connection = new SqliteConnection(options.ConnectionString(SqliteOpenMode.ReadOnly));
            await connection.OpenAsync(timeout.Token);
            await using var db = await factory.CreateDbContextAsync(timeout.Token);
            return await CheckConnectionAsync(connection, db.Database.GetMigrations().ToArray(), timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DatabaseCheckResult.Unavailable;
        }
        catch (Exception ex) when (IsStoreFailure(ex))
        {
            return DatabaseCheckResult.Unavailable;
        }
    }

    private static async Task<DatabaseCheckResult> CheckConnectionAsync(
        DbConnection connection, string[] knownMigrations, CancellationToken ct)
    {
        var schemaFailure = await CheckSchemaAsync(connection, knownMigrations, requireCurrent: true, ct);
        if (schemaFailure is not null) return schemaFailure;
        if (!string.Equals(await ScalarAsync(connection, "PRAGMA quick_check", ct) as string, "ok", StringComparison.Ordinal))
            return DatabaseCheckResult.Invalid;
        if (await ScalarAsync(connection, "PRAGMA foreign_key_check", ct) is not null)
            return DatabaseCheckResult.Invalid;
        var active = await ScalarAsync(connection, """
            SELECT s.id FROM students s JOIN app_settings a ON a.setting_value = s.id
            WHERE a.setting_key = 'active_demo_student_id'
            """, ct);
        return active is string ? DatabaseCheckResult.Ready : DatabaseCheckResult.Invalid;
    }

    private static async Task<DatabaseCheckResult?> CheckSchemaAsync(
        DbConnection connection, string[] knownMigrations, bool requireCurrent, CancellationToken ct)
    {
        if (knownMigrations.Length == 0) return DatabaseCheckResult.Incompatible;
        var tables = await StringsAsync(connection,
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'", ct);
        if (tables.Count == 0 || tables.All(t => t == "__EFMigrationsLock"))
            return requireCurrent ? DatabaseCheckResult.Missing : null;
        if (!tables.Contains("__EFMigrationsHistory")) return DatabaseCheckResult.Incompatible;
        var applied = await StringsAsync(connection,
            "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId", ct);
        // Refuse newer/divergent history; do not stamp an unmanaged database or downgrade it.
        if (!applied.SequenceEqual(knownMigrations.Take(applied.Count))) return DatabaseCheckResult.Incompatible;
        if (requireCurrent && (applied.Count != knownMigrations.Length || !RequiredTables.IsSubsetOf(tables)))
            return DatabaseCheckResult.Incompatible;
        return null;
    }

    private static async Task<object?> ScalarAsync(DbConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(ct);
    }

    private static async Task<List<string>> StringsAsync(DbConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var values = new List<string>();
        while (await reader.ReadAsync(ct)) values.Add(reader.GetString(0));
        return values;
    }

    private void PrepareDirectory()
    {
        var directory = Path.GetDirectoryName(options.FullPath)!;
        var existed = Directory.Exists(directory);
        Directory.CreateDirectory(directory);
        // Do not rewrite permissions on an existing caller-specified/shared parent directory.
        var isDefault = string.Equals(directory, Path.GetDirectoryName(CampusDatabaseOptions.DefaultPath), StringComparison.OrdinalIgnoreCase);
        if (!existed || isDefault)
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                var security = new DirectorySecurity();
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.SetOwner(identity.User!);
                security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow));
                new DirectoryInfo(directory).SetAccessControl(security);
            }
            else
            {
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
    }

    private static bool IsStoreFailure(Exception exception) => exception is
        DbException or DbUpdateException or IOException or UnauthorizedAccessException or InvalidOperationException
        or System.Security.SecurityException;
}
