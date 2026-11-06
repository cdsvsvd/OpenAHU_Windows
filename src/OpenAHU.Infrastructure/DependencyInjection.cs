using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAHU.Application.Abstractions;
using OpenAHU.Infrastructure.Persistence;
using OpenAHU.Infrastructure.Repositories;

namespace OpenAHU.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services.AddInfrastructure(new CampusDatabaseOptions
        {
            DatabasePath = configuration["OpenAHU:DatabasePath"] ?? CampusDatabaseOptions.DefaultPath
        });

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, CampusDatabaseOptions options)
    {
        options.Validate();
        services.AddLogging();
        services.AddSingleton(options);
        services.AddDbContextFactory<CampusDbContext>(builder => ConfigureDatabase(builder, options));
        services.AddSingleton<CampusDatabase>();
        // AddDbContextFactory also registers CampusDbContext as Scoped. Repositories inject that
        // scoped instance; the singleton factory is only for initialization and independent probes.
        services.AddScoped<IAppSettingsRepository, AppSettingsRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<ITermRepository, TermRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        services.AddScoped<IGradeRepository, GradeRepository>();
        services.AddScoped<IExamRepository, ExamRepository>();
        services.AddScoped<ILinkRepository, LinkRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        return services;
    }

    internal static void ConfigureDatabase(DbContextOptionsBuilder builder, CampusDatabaseOptions options)
    {
        builder.UseSqlite(options.ConnectionString(), sqlite => sqlite.CommandTimeout(options.BusyTimeoutSeconds))
            .UseSeeding((context, _) => DemoData.Seed(context))
            .UseAsyncSeeding((context, _, cancellationToken) => DemoData.SeedAsync(context, cancellationToken));
    }
}
