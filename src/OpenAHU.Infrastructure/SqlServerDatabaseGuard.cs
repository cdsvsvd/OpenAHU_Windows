using System.Data;
using Microsoft.EntityFrameworkCore;

namespace OpenAHU.Infrastructure;

/// <summary>
/// SQL Server mode only operates on an explicitly created, complete demo schema.
/// It does not create, migrate, alter or drop SQL Server tables automatically.
/// </summary>
public static class SqlServerDatabaseGuard
{
    public static async Task AssertReadyAsync(CampusDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer())
            throw new InvalidOperationException("SQL Server guard invoked with a different provider.");
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*) FROM sys.tables
                WHERE schema_id = SCHEMA_ID(N'dbo')
                  AND name IN (N'students',N'terms',N'courses',N'course_weeks',
                    N'enrollments',N'grades',N'exams',N'campus_links',N'app_settings')
                """;
            var count = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
            if (count != 9)
                throw new InvalidOperationException(
                    "SQL Server演示库未完成建表。请先在SSMS执行sqlserver/01_Create_CampusDemo.sql。");
        }
        finally { await connection.CloseAsync(); }
    }
}
