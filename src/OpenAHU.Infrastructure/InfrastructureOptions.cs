using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using OpenAHU.Application;

namespace OpenAHU.Infrastructure;

/// <summary>Two independent demo modes: SQLite by default, or an explicitly provisioned SQL Server database.</summary>
public static class InfrastructureOptions
{
    public static string DatabaseProvider(IConfiguration configuration)
    {
        var provider = configuration["OpenAHU:DatabaseProvider"] ?? "Sqlite";
        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)) return "Sqlite";
        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)) return "SqlServer";
        throw new InvalidOperationException("OpenAHU:DatabaseProvider 仅支持 Sqlite 或 SqlServer。");
    }

    public static string DatabasePath(IConfiguration configuration)
    {
        var path = configuration["OpenAHU:DatabasePath"];
        if (string.IsNullOrWhiteSpace(path))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenAHU", "campus.db");
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("OpenAHU:DatabasePath 必须是绝对路径。");
        return path;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        if (DatabaseProvider(configuration) == "SqlServer")
        {
            var connectionString = configuration["OpenAHU:SqlServerConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "SQL Server 模式尚未配置 OpenAHU:SqlServerConnectionString。");
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.DataSource) ||
                string.IsNullOrWhiteSpace(builder.InitialCatalog) ||
                new[] { "master", "model", "msdb", "tempdb" }
                    .Contains(builder.InitialCatalog, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "SQL Server 必须指定独立的应用数据库，不能连接系统数据库。");
            services.AddDbContext<CampusDbContext>(options => options.UseSqlServer(connectionString));
        }
        else
        {
            var path = DatabasePath(configuration);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            services.AddDbContext<CampusDbContext>(options =>
            {
                options.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                {
                    DataSource = path, ForeignKeys = true, Pooling = true
                }.ToString());
                // Existing SQLite migration is hand-authored SQL. Rebuild its snapshot
                // before removing this warning suppression; never reset users' databases.
                options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            });
        }
        services.AddScoped<ICampusRepository, CampusRepository>();
        return services;
    }
}
