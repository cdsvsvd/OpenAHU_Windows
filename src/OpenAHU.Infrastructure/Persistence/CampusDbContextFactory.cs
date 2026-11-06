using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenAHU.Infrastructure.Persistence;

public sealed class CampusDbContextFactory : IDesignTimeDbContextFactory<CampusDbContext>
{
    public CampusDbContext CreateDbContext(string[] args)
    {
        var index = Array.IndexOf(args, "--database-path");
        if (index >= 0 && index + 1 == args.Length)
            throw new ArgumentException("--database-path 后必须提供绝对文件路径。");
        var options = new CampusDatabaseOptions
        {
            DatabasePath = index >= 0 ? args[index + 1] : CampusDatabaseOptions.DefaultPath
        };
        options.Validate();
        var builder = new DbContextOptionsBuilder<CampusDbContext>();
        DependencyInjection.ConfigureDatabase(builder, options);
        return new CampusDbContext(builder.Options);
    }
}
