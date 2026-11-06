using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using OpenAHU.Infrastructure.Entities;
using OpenAHU.Infrastructure.Persistence;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OpenAHU.Infrastructure.Verification;

public sealed class DatabaseTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OpenAHU-member05-tests", Guid.NewGuid().ToString("N"));
    private ServiceProvider _services = null!;
    private string DatabasePath => Path.Combine(_root, "private", "campus.db");
    private CampusDatabase Database => _services.GetRequiredService<CampusDatabase>();
    private Task<CampusDbContext> OpenAsync() => _services.GetRequiredService<IDbContextFactory<CampusDbContext>>().CreateDbContextAsync();

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        _services = new ServiceCollection().AddInfrastructure(new CampusDatabaseOptions
        {
            DatabasePath = DatabasePath, BusyTimeoutSeconds = 1, InitializationTimeoutSeconds = 5
        }).BuildServiceProvider(validateScopes: true);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        // Only delete this fixture's GUID directory, never the user's runtime database.
        var fullRoot = Path.GetFullPath(_root);
        var testBase = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OpenAHU-member05-tests")) + Path.DirectorySeparatorChar;
        if (!fullRoot.StartsWith(testBase, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("测试清理路径超出允许目录。");
        Directory.Delete(fullRoot, recursive: true);
    }

    [Fact]
    public async Task ProbeBeforeInitializationDoesNotCreateFiles()
    {
        var result = await Database.CheckAsync();
        Assert.False(result.IsReady);
        Assert.Equal(DatabaseState.NotInitialized, result.State);
        Assert.False(Directory.Exists(Path.GetDirectoryName(DatabasePath)));
    }

    [Fact]
    public async Task MigrationsCreateNineTablesAndCompleteDemoData()
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        Assert.True((await Database.CheckAsync()).IsReady);
        await using var db = await OpenAsync();
        Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(2, await db.Students.CountAsync());
        Assert.Equal(2, await db.Terms.CountAsync());
        Assert.Equal(4, await db.Courses.CountAsync());
        Assert.Equal(56, await db.CourseWeeks.CountAsync());
        Assert.Equal(6, await db.Enrollments.CountAsync());
        Assert.Equal(4, await db.Grades.CountAsync());
        Assert.Equal(3, await db.Exams.CountAsync());
        Assert.Equal(2, await db.CampusLinks.CountAsync());
        Assert.Equal(4, await db.AppSettings.CountAsync());
        Assert.Equal("demo-001", (await db.AppSettings.FindAsync("active_demo_student_id"))!.SettingValue);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task RepeatedInitializationPreservesEditsAndDoesNotRestoreDeletedCourses()
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using (var db = await OpenAsync())
        {
            (await db.Students.FindAsync("demo-001"))!.DisplayName = "修改后的演示资料";
            await db.SaveChangesAsync();
            await db.Courses.ExecuteDeleteAsync();
        }
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using var reopened = await OpenAsync();
        Assert.Equal("修改后的演示资料", (await reopened.Students.FindAsync("demo-001"))!.DisplayName);
        Assert.Empty(await reopened.Courses.ToListAsync());
        Assert.Empty(await reopened.CourseWeeks.ToListAsync());
        Assert.Equal(2, await reopened.Students.CountAsync());
    }

    [Fact]
    public async Task CourseAndWeeksSurviveClosingAndReopeningContext()
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using (var db = await OpenAsync())
        {
            db.Courses.Add(NewCourse("persisted"));
            db.CourseWeeks.AddRange(new CourseWeek { CourseId = "persisted", WeekNumber = 1 },
                new CourseWeek { CourseId = "persisted", WeekNumber = 3 });
            db.Enrollments.Add(new Enrollment { StudentId = "demo-001", CourseId = "persisted" });
            await db.SaveChangesAsync();
        }
        await using (var db = await OpenAsync())
        {
            var course = await db.Courses.Include(x => x.Weeks).SingleAsync(x => x.Id == "persisted");
            Assert.Equal(new[] { 1, 3 }, course.Weeks.Select(x => x.WeekNumber).Order().ToArray());
            course.Name = "修改后的课程";
            db.CourseWeeks.RemoveRange(course.Weeks);
            course.Weeks.Clear();
            course.Weeks.Add(new CourseWeek { CourseId = "persisted", WeekNumber = 5 });
            await db.SaveChangesAsync();
        }
        await using var reopened = await OpenAsync();
        Assert.Equal("修改后的课程", (await reopened.Courses.FindAsync("persisted"))!.Name);
        Assert.Equal(new[] { 5 }, await reopened.CourseWeeks.Where(x => x.CourseId == "persisted")
            .Select(x => x.WeekNumber).ToArrayAsync());
        Assert.True(await reopened.Enrollments.AnyAsync(x => x.CourseId == "persisted" && x.StudentId == "demo-001"));
    }

    [Fact]
    public async Task FailedMultiStepWriteRollsBackWholeTransaction()
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using (var db = await OpenAsync())
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.Courses.Add(NewCourse("rolled-back"));
            db.CourseWeeks.Add(new CourseWeek { CourseId = "rolled-back", WeekNumber = 2 });
            db.Enrollments.Add(new Enrollment { StudentId = "demo-001", CourseId = "rolled-back" });
            await db.SaveChangesAsync();
            db.Grades.Add(new Grade { StudentId = "demo-001", CourseId = "rolled-back", Credits = 3m, Score = 101m });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            await transaction.RollbackAsync();
        }
        await using var reopened = await OpenAsync();
        Assert.False(await reopened.Courses.AnyAsync(x => x.Id == "rolled-back"));
        Assert.False(await reopened.CourseWeeks.AnyAsync(x => x.CourseId == "rolled-back"));
        Assert.False(await reopened.Enrollments.AnyAsync(x => x.CourseId == "rolled-back"));
    }

    [Fact]
    public async Task UnmanagedExistingDatabaseIsRejectedWithoutChangingItsData()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        await using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE legacy(value TEXT); INSERT INTO legacy VALUES ('keep-me');";
            await command.ExecuteNonQueryAsync();
        }
        SqliteConnection.ClearAllPools();
        var before = await File.ReadAllBytesAsync(DatabasePath);
        Assert.Equal(DatabaseState.IncompatibleSchema, (await Database.InitializeAsync()).State);
        SqliteConnection.ClearAllPools();
        Assert.Equal(before, await File.ReadAllBytesAsync(DatabasePath));
    }

    [Fact]
    public async Task NewerMigrationHistoryIsRejectedWithoutDowngrading()
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using (var db = await OpenAsync())
            await db.Database.ExecuteSqlRawAsync("INSERT INTO __EFMigrationsHistory VALUES ('99999999999999_FutureSchema', '99.0.0')");
        Assert.Equal(DatabaseState.IncompatibleSchema, (await Database.InitializeAsync()).State);
        Assert.Equal(DatabaseState.IncompatibleSchema, (await Database.CheckAsync()).State);
        await using var reopened = await OpenAsync();
        Assert.Equal(2, await reopened.Students.CountAsync());
        Assert.Equal(2, (await reopened.Database.GetAppliedMigrationsAsync()).Count());
    }

    [Fact]
    public async Task CorruptFileProducesSafeMessageAndIsNotReplaced()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        var bytes = System.Text.Encoding.UTF8.GetBytes("this is not a SQLite database");
        await File.WriteAllBytesAsync(DatabasePath, bytes);
        var result = await Database.InitializeAsync();
        Assert.False(result.IsReady);
        Assert.Equal(DatabaseState.Unavailable, result.State);
        Assert.DoesNotContain(DatabasePath, result.Message);
        SqliteConnection.ClearAllPools();
        Assert.Equal(bytes, await File.ReadAllBytesAsync(DatabasePath));
    }

    [Fact]
    public async Task ProbeDetectsBrokenSchemaWithoutRepairingIt()
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using (var db = await OpenAsync())
            await db.Database.ExecuteSqlRawAsync("DROP TABLE campus_links");
        Assert.Equal(DatabaseState.IncompatibleSchema, (await Database.CheckAsync()).State);
    }

    [Fact]
    public async Task ExistingNonEmptyDataIsNeverSeededEvenWithoutSeedMarker()
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using (var db = await OpenAsync())
        {
            await db.Courses.ExecuteDeleteAsync();
            await db.Terms.ExecuteDeleteAsync();
            await db.CampusLinks.ExecuteDeleteAsync();
            await db.AppSettings.ExecuteDeleteAsync();
            await db.Students.ExecuteDeleteAsync();
            db.Students.Add(new Student { Id = "custom-demo", DisplayName = "自建虚构资料", CreatedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        Assert.Equal(DatabaseState.InvalidData, (await Database.InitializeAsync()).State);
        await using var reopened = await OpenAsync();
        Assert.Equal("custom-demo", (await reopened.Students.SingleAsync()).Id);
        Assert.Empty(await reopened.Courses.ToListAsync());
        Assert.Empty(await reopened.AppSettings.ToListAsync());
    }

    [Fact]
    public async Task ForeignKeysAndCompositeKeysAreEnforced()
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using var db = await OpenAsync();
        db.Grades.Add(new Grade { StudentId = "demo-002", CourseId = "course-003", Credits = 3m });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.CourseWeeks.Add(new CourseWeek { CourseId = "course-001", WeekNumber = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Courses.Add(NewCourse("invalid-term", termId: "unknown-term"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(8, 1, 2)]
    [InlineData(1, 0, 2)]
    [InlineData(1, 2, 1)]
    [InlineData(1, 1, 21)]
    public async Task ScheduleConstraintsRejectInvalidValues(int day, int start, int end)
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using var db = await OpenAsync();
        var course = NewCourse("invalid");
        course.DayOfWeek = day;
        course.StartSection = start;
        course.EndSection = end;
        db.Courses.Add(course);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task NullGradesAndOffsetTimestampsRoundTrip()
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using var db = await OpenAsync();
        var grade = await db.Grades.SingleAsync(x => x.StudentId == "demo-001" && x.CourseId == "course-003");
        Assert.Null(grade.Score);
        Assert.Null(grade.GradePoint);
        Assert.Equal(3.5m, (await db.Grades.SingleAsync(x => x.StudentId == "demo-001" && x.CourseId == "course-002")).Credits);
        var exam = await db.Exams.FindAsync("exam-001");
        Assert.Equal(TimeSpan.FromHours(8), exam!.StartsAt!.Value.Offset);
        Assert.Equal(new DateOnly(2026, 9, 7), (await db.Terms.FindAsync("term-2026-fall"))!.StartDate);
        Assert.Null((await db.Exams.FindAsync("exam-003"))!.StartsAt);
    }

    [Fact]
    public async Task ExamOrderChecksInstantsAcrossDifferentOffsets()
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using var db = await OpenAsync();
        db.Exams.Add(new Exam { Id = "offset-test", CourseId = "course-001",
            StartsAt = new DateTimeOffset(2026, 12, 21, 9, 0, 0, TimeSpan.FromHours(8)),
            EndsAt = new DateTimeOffset(2026, 12, 21, 2, 0, 0, TimeSpan.Zero) });
        await db.SaveChangesAsync();
        Assert.NotNull(await db.Exams.FindAsync("offset-test"));
    }

    [Fact]
    public async Task OnlyOneCurrentTermIsAllowed()
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using var db = await OpenAsync();
        (await db.Terms.FindAsync("term-2027-spring"))!.IsCurrent = true;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task LockedDatabaseReturnsFailureWithinBoundedTimeout()
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using var lockConnection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
        await lockConnection.OpenAsync();
        await using var lockCommand = lockConnection.CreateCommand();
        lockCommand.CommandText = "BEGIN EXCLUSIVE; UPDATE app_settings SET setting_value = setting_value;";
        await lockCommand.ExecuteNonQueryAsync();
        var result = await Database.InitializeAsync();
        Assert.False(result.IsReady);
        Assert.Equal(DatabaseState.Unavailable, result.State);
        lockCommand.CommandText = "ROLLBACK";
        await lockCommand.ExecuteNonQueryAsync();
        Assert.True((await Database.CheckAsync()).IsReady);
    }

    [Fact]
    public async Task CallerCancellationIsNotDisguisedAsDatabaseFailure()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Database.InitializeAsync(cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Database.CheckAsync(cancelled.Token));
    }

    [Fact]
    public async Task ReadOnlyFileIsReportedWithoutReplacingIt()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.True((await Database.InitializeAsync()).IsReady);
        SqliteConnection.ClearAllPools();
        File.SetAttributes(DatabasePath, File.GetAttributes(DatabasePath) | FileAttributes.ReadOnly);
        try
        {
            Assert.Equal(DatabaseState.Unavailable, (await Database.InitializeAsync()).State);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.SetAttributes(DatabasePath, FileAttributes.Normal);
        }
        Assert.True((await Database.CheckAsync()).IsReady);
    }

    [Fact]
    public async Task AbandonedMigrationLockTimesOutWithoutAutomaticallyUnlocking()
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using (var db = await OpenAsync())
            await db.Database.ExecuteSqlRawAsync("INSERT INTO __EFMigrationsLock (Id, Timestamp) VALUES (1, '2026-11-05T00:00:00Z')");
        Assert.Equal(DatabaseState.Unavailable, (await Database.InitializeAsync()).State);
        await using var reopened = await OpenAsync();
        var locks = await reopened.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM __EFMigrationsLock").SingleAsync();
        Assert.Equal(1, locks);
    }

    [Fact]
    public async Task InitialMigrationCanBeRevertedAndRebuiltOnDisposableDatabase()
    {
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using (var db = await OpenAsync())
        {
            var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
            await migrator.MigrateAsync("0");
            Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
        }
        Assert.False((await Database.CheckAsync()).IsReady);
        Assert.True((await Database.InitializeAsync()).IsReady);
        await using var rebuilt = await OpenAsync();
        Assert.Equal(4, await rebuilt.Courses.CountAsync());
    }

    [Fact]
    public async Task NewWindowsDatabaseDirectoryHasOnlyCurrentUserAccessRule()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.True((await Database.InitializeAsync()).IsReady);
        var acl = new DirectoryInfo(Path.GetDirectoryName(DatabasePath)!).GetAccessControl();
        Assert.True(acl.AreAccessRulesProtected);
        using var identity = WindowsIdentity.GetCurrent();
        var rule = Assert.Single(acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
        Assert.Equal(identity.User, rule.IdentityReference);
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
    }

    [Theory]
    [InlineData("relative.db")]
    [InlineData("")]
    public void RelativeOrEmptyPathsAreRejectedBeforeOpeningDatabase(string path)
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection()
            .AddInfrastructure(new CampusDatabaseOptions { DatabasePath = path }));
    }

    private static Course NewCourse(string id, string termId = "term-2026-fall") => new()
    {
        Id = id, TermId = termId, Name = "虚构测试课程", Teacher = "演示教师", Location = "演示教室",
        DayOfWeek = 7, StartSection = 3, EndSection = 4
    };
}
