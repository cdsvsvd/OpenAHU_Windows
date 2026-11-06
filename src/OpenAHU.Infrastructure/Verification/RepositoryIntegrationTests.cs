using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Abstractions.Commands;
using OpenAHU.Application.Abstractions.Queries;
using OpenAHU.Application.Errors;
using OpenAHU.Application.Models;
using OpenAHU.Application.Services;
using OpenAHU.Infrastructure.Persistence;
using Xunit;
using Entity = OpenAHU.Infrastructure.Entities;

namespace OpenAHU.Infrastructure.Verification;

public sealed class RepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OpenAHU-member05-repository-tests", Guid.NewGuid().ToString("N"));
    private ServiceProvider _services = null!;
    private string DatabasePath => Path.Combine(_root, "private", "campus.db");
    private static readonly CancellationToken Ct = CancellationToken.None;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        _services = BuildServices(DatabasePath);
        Assert.True((await _services.GetRequiredService<CampusDatabase>().InitializeAsync()).IsReady);
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        var root = Path.GetFullPath(_root);
        var testBase = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OpenAHU-member05-repository-tests")) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(testBase, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("测试清理目录越界。");
        Directory.Delete(root, recursive: true);
    }

    private static ServiceProvider BuildServices(string path)
    {
        var services = new ServiceCollection().AddInfrastructure(new CampusDatabaseOptions
        {
            DatabasePath = path, BusyTimeoutSeconds = 1, InitializationTimeoutSeconds = 5
        });
        // API owns production Application registration. This is its equivalent for integration verification.
        services.AddScoped<ActiveStudentProvider>();
        services.AddScoped<IProfileQuery, ProfileQuery>();
        services.AddScoped<ITermQuery, TermQuery>();
        services.AddScoped<ICourseQuery, CourseQuery>();
        services.AddScoped<IGradeQuery, GradeQuery>();
        services.AddScoped<IExamQuery, ExamQuery>();
        services.AddScoped<ILinkQuery, LinkQuery>();
        services.AddScoped<IDataStatusQuery, DataStatusQuery>();
        services.AddScoped<ICourseCommand, CourseCommand>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static CourseDraft Draft(string name = "虚构新增课程", IReadOnlyList<int>? weeks = null) =>
        new(name, " 示例教师 ", " 演示教室 ", DayOfWeek.Sunday, 3, 4, weeks ?? new[] { 1, 3 }, "term-2026-fall");

    private static Course Model(string id, IReadOnlyList<int>? weeks = null) =>
        new(id, "仓储测试课程", "示例教师", "演示教室", DayOfWeek.Sunday, 3, 4, weeks ?? new[] { 1, 3 }, "term-2026-fall");

    [Fact]
    public async Task AllNineInterfacesAndDbContextAreScoped()
    {
        using var scope = _services.CreateScope();
        using var other = _services.CreateScope();
        foreach (var type in new[] { typeof(IAppSettingsRepository), typeof(IStudentRepository), typeof(ITermRepository),
            typeof(ICourseRepository), typeof(IEnrollmentRepository), typeof(IGradeRepository), typeof(IExamRepository),
            typeof(ILinkRepository), typeof(IUnitOfWork), typeof(CampusDbContext) })
        {
            Assert.Same(scope.ServiceProvider.GetRequiredService(type), scope.ServiceProvider.GetRequiredService(type));
            Assert.NotSame(scope.ServiceProvider.GetRequiredService(type), other.ServiceProvider.GetRequiredService(type));
            Assert.Throws<InvalidOperationException>(() => _services.GetRequiredService(type));
        }
        Assert.True((await _services.GetRequiredService<CampusDatabase>().CheckAsync()).IsReady);
    }

    [Fact]
    public async Task ProfileTermsAndLinksAreReadFromSeededSqlite()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var profile = await sp.GetRequiredService<IProfileQuery>().GetActiveAsync(Ct);
        Assert.Equal("demo-001", profile.Id);
        Assert.True(profile.IsDemo);
        var terms = await sp.GetRequiredService<ITermQuery>().GetAllAsync(Ct);
        Assert.Equal(new[] { "term-2027-spring", "term-2026-fall" }, terms.Select(x => x.Id));
        Assert.Equal(2, (await sp.GetRequiredService<ILinkQuery>().GetAllAsync(Ct)).Count);
        Assert.Null(await sp.GetRequiredService<IStudentRepository>().FindAsync("missing", Ct));
        Assert.Null(await sp.GetRequiredService<ITermRepository>().FindAsync("missing", Ct));
        Assert.Null(await sp.GetRequiredService<ICourseRepository>().FindAsync("missing", Ct));
        Assert.Null(await sp.GetRequiredService<IDataStatusQuery>().GetLastUpdatedAsync(Ct));
    }

    [Fact]
    public async Task CourseGradeAndExamQueriesRespectActiveStudentAndTerm()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var courses = sp.GetRequiredService<ICourseQuery>();
        var grades = sp.GetRequiredService<IGradeQuery>();
        var exams = sp.GetRequiredService<IExamQuery>();
        Assert.Equal(3, (await courses.GetByTermAsync("term-2026-fall", Ct)).Count);
        Assert.Single(await courses.GetByTermAsync("term-2027-spring", Ct));
        var initialGrades = await grades.GetByTermAsync("term-2026-fall", Ct);
        Assert.Equal(3, initialGrades.Count);
        Assert.Null(initialGrades.Single(x => x.CourseId == "course-003").Score);
        Assert.Equal(3.5m, initialGrades.Single(x => x.CourseId == "course-002").Credits);
        var before = await exams.GetByTermAsync("term-2026-fall", Ct);
        Assert.Equal(3, before.Count);
        Assert.Null(before.Last().StartsAt);
        Assert.Equal(TimeSpan.FromHours(8), before.First().StartsAt!.Value.Offset);

        var db = sp.GetRequiredService<CampusDbContext>();
        (await db.AppSettings.FindAsync("active_demo_student_id"))!.SettingValue = "demo-002";
        await db.SaveChangesAsync();
        Assert.Equal(2, (await courses.GetByTermAsync("term-2026-fall", Ct)).Count);
        Assert.Empty(await courses.GetByTermAsync("term-2027-spring", Ct));
        var secondGrades = await grades.GetByTermAsync("term-2026-fall", Ct);
        Assert.Single(secondGrades);
        Assert.Equal(79m, secondGrades.Single().Score);
        Assert.Equal(2, (await exams.GetByTermAsync("term-2026-fall", Ct)).Count);
    }

    [Fact]
    public async Task CreateWritesCourseWeeksAndEnrollmentAndSurvivesProviderRestart()
    {
        string id;
        await using (var scope = _services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ICourseCommand>()
                .CreateAsync(Draft(" 新课程 ", new[] { 3, 1, 3 }), Ct);
            Assert.Equal(CourseWriteStatus.Ok, result.Status);
            Assert.StartsWith("course-local-", result.CourseId);
            id = result.CourseId!;
            var db = scope.ServiceProvider.GetRequiredService<CampusDbContext>();
            Assert.Equal(7, (await db.Courses.SingleAsync(x => x.Id == id)).DayOfWeek);
            Assert.True(await db.Enrollments.AnyAsync(x => x.StudentId == "demo-001" && x.CourseId == id));
            Assert.False(await db.Enrollments.AnyAsync(x => x.StudentId == "demo-002" && x.CourseId == id));
        }
        await _services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        _services = BuildServices(DatabasePath);
        Assert.True((await _services.GetRequiredService<CampusDatabase>().InitializeAsync()).IsReady);
        await using var reopened = _services.CreateAsyncScope();
        var courses = await reopened.ServiceProvider.GetRequiredService<ICourseQuery>().GetByTermAsync("term-2026-fall", Ct);
        var course = Assert.Single(courses, x => x.Id == id);
        Assert.Equal("新课程", course.Name);
        Assert.Equal("示例教师", course.Teacher);
        Assert.Equal(DayOfWeek.Sunday, course.DayOfWeek);
        Assert.Equal(new[] { 1, 3 }, course.WeekNumbers);
        Assert.Equal(id, courses.Last().Id);
    }

    [Fact]
    public async Task RepeatedUpdatesReplaceOverlappingWeeksInSameScope()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var command = sp.GetRequiredService<ICourseCommand>();
        var created = await command.CreateAsync(Draft(), Ct);
        var id = created.CourseId!;
        Assert.Equal(CourseWriteStatus.Ok, (await command.UpdateAsync(id, Draft("第一次修改", new[] { 1, 5, 7 }), Ct)).Status);
        Assert.Equal(CourseWriteStatus.Ok, (await command.UpdateAsync(id, Draft("第二次修改", new[] { 1, 5 }), Ct)).Status);
        var course = await sp.GetRequiredService<ICourseRepository>().FindAsync(id, Ct);
        Assert.Equal("第二次修改", course!.Name);
        Assert.Equal(new[] { 1, 5 }, course.WeekNumbers);
        Assert.Equal(1, await sp.GetRequiredService<IEnrollmentRepository>().CountByCourseAsync(id, Ct));
    }

    [Fact]
    public async Task DeleteRemovesCourseWeeksAndEnrollmentAndReturnsNotFoundOnRetry()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var command = sp.GetRequiredService<ICourseCommand>();
        var id = (await command.CreateAsync(Draft(), Ct)).CourseId!;
        Assert.Equal(CourseWriteStatus.Ok, (await command.DeleteAsync(id, Ct)).Status);
        var db = sp.GetRequiredService<CampusDbContext>();
        Assert.False(await db.Courses.AnyAsync(x => x.Id == id));
        Assert.False(await db.CourseWeeks.AnyAsync(x => x.CourseId == id));
        Assert.False(await db.Enrollments.AnyAsync(x => x.CourseId == id));
        Assert.Equal(CourseWriteStatus.NotFound, (await command.DeleteAsync(id, Ct)).Status);
    }

    [Fact]
    public async Task EditingSharedCoursePreservesEnrollmentsAndCourseCode()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var repo = sp.GetRequiredService<ICourseRepository>();
        var existing = (await repo.FindAsync("course-002", Ct))!;
        var draft = new CourseDraft("共享课程修改", existing.Teacher, existing.Location, existing.DayOfWeek,
            existing.StartSection, existing.EndSection, existing.WeekNumbers, existing.TermId);
        var result = await sp.GetRequiredService<ICourseCommand>().UpdateAsync(existing.Id, draft, Ct);
        Assert.Equal(CourseWriteStatus.Ok, result.Status);
        Assert.Equal(2, result.EnrolledStudentCount);
        Assert.Equal(1, result.OtherStudentCount);
        var otherCourses = await repo.GetByStudentAndTermAsync("demo-002", existing.TermId, Ct);
        Assert.Equal("共享课程修改", otherCourses.Single(x => x.Id == existing.Id).Name);
        var db = sp.GetRequiredService<CampusDbContext>();
        Assert.Equal("D-COMP-002", (await db.Courses.FindAsync(existing.Id))!.CourseCode);
        Assert.Equal(2, await db.Enrollments.CountAsync(x => x.CourseId == existing.Id));
    }

    [Fact]
    public async Task SharedCourseDeletionReportsImpactAndCascadesBothEnrollments()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var command = sp.GetRequiredService<ICourseCommand>();
        var id = (await command.CreateAsync(Draft(), Ct)).CourseId!;
        await sp.GetRequiredService<IUnitOfWork>().ExecuteAsync(async ct =>
        {
            await sp.GetRequiredService<IEnrollmentRepository>().AddAsync("demo-002", id, ct);
            return true;
        }, Ct);
        var result = await command.DeleteAsync(id, Ct);
        Assert.Equal(CourseWriteStatus.Ok, result.Status);
        Assert.Equal(2, result.EnrolledStudentCount);
        Assert.Equal(1, result.OtherStudentCount);
        Assert.Equal(0, await sp.GetRequiredService<IEnrollmentRepository>().CountByCourseAsync(id, Ct));
    }

    [Fact]
    public async Task GradesIncludingOtherStudentsPreventDeletion()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var command = sp.GetRequiredService<ICourseCommand>();
        var original = await command.DeleteAsync("course-003", Ct);
        Assert.Equal(CourseConflictReason.HasGrades, original.ConflictReason);
        var id = (await command.CreateAsync(Draft(), Ct)).CourseId!;
        await sp.GetRequiredService<IUnitOfWork>().ExecuteAsync(async ct =>
        {
            await sp.GetRequiredService<IEnrollmentRepository>().AddAsync("demo-002", id, ct);
            return true;
        }, Ct);
        var db = sp.GetRequiredService<CampusDbContext>();
        db.Grades.Add(new Entity.Grade { StudentId = "demo-002", CourseId = id, Credits = 3m });
        await db.SaveChangesAsync();
        var result = await command.DeleteAsync(id, Ct);
        Assert.Equal(CourseWriteStatus.Conflict, result.Status);
        Assert.Equal(CourseConflictReason.HasGrades, result.ConflictReason);
        Assert.True(await db.Courses.AnyAsync(x => x.Id == id));
    }

    [Fact]
    public async Task ExamsWithoutGradesAlsoPreventDeletion()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var command = sp.GetRequiredService<ICourseCommand>();
        var id = (await command.CreateAsync(Draft(), Ct)).CourseId!;
        var db = sp.GetRequiredService<CampusDbContext>();
        db.Exams.Add(new Entity.Exam { Id = "test-exam", CourseId = id });
        await db.SaveChangesAsync();
        var result = await command.DeleteAsync(id, Ct);
        Assert.Equal(CourseWriteStatus.Conflict, result.Status);
        Assert.Equal(CourseConflictReason.HasExams, result.ConflictReason);
        Assert.True(await db.Courses.AnyAsync(x => x.Id == id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DatabaseDeleteGuardPreservesAssociationsAddedAfterBusinessPrecheck(bool addExam)
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var id = (await sp.GetRequiredService<ICourseCommand>().CreateAsync(Draft(), Ct)).CourseId!;
        Assert.False(await sp.GetRequiredService<IGradeRepository>().ExistsByCourseAsync(id, Ct));
        // Represents a separate request committing after the application's precheck.
        await using (var concurrentScope = _services.CreateAsyncScope())
        {
            var db = concurrentScope.ServiceProvider.GetRequiredService<CampusDbContext>();
            if (addExam) db.Exams.Add(new Entity.Exam { Id = "concurrent-exam", CourseId = id });
            else db.Grades.Add(new Entity.Grade { StudentId = "demo-001", CourseId = id, Credits = 3m });
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<CampusStoreUnavailableException>(() => sp.GetRequiredService<IUnitOfWork>()
            .ExecuteAsync(ct => sp.GetRequiredService<ICourseRepository>().DeleteAsync(id, ct), Ct));
        var check = sp.GetRequiredService<CampusDbContext>();
        Assert.True(await check.Courses.AnyAsync(x => x.Id == id));
        if (addExam) Assert.True(await check.Exams.AnyAsync(x => x.CourseId == id));
        else Assert.True(await check.Grades.AnyAsync(x => x.CourseId == id));
    }

    [Fact]
    public async Task UnitOfWorkRollsBackCourseAndWeeksWhenEnrollmentFails()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var uow = sp.GetRequiredService<IUnitOfWork>();
        var courses = sp.GetRequiredService<ICourseRepository>();
        var enrollments = sp.GetRequiredService<IEnrollmentRepository>();
        var error = await Assert.ThrowsAsync<CampusStoreUnavailableException>(() => uow.ExecuteAsync(async ct =>
        {
            await courses.AddAsync(Model("rollback-enrollment"), ct);
            await enrollments.AddAsync("missing-student", "rollback-enrollment", ct);
            return true;
        }, Ct));
        Assert.DoesNotContain(DatabasePath, error.Message);
        Assert.IsType<SqliteException>(error.InnerException);
        Assert.Null(await courses.FindAsync("rollback-enrollment", Ct));
        // A later successful write in the SAME scope must not flush the failed graph back into the DB.
        await uow.ExecuteAsync(async ct =>
        {
            await courses.AddAsync(Model("after-rollback"), ct);
            await enrollments.AddAsync("demo-001", "after-rollback", ct);
            return true;
        }, Ct);
        Assert.Null(await courses.FindAsync("rollback-enrollment", Ct));
        Assert.NotNull(await courses.FindAsync("after-rollback", Ct));
        var db = sp.GetRequiredService<CampusDbContext>();
        Assert.False(await db.CourseWeeks.AnyAsync(x => x.CourseId == "rollback-enrollment"));
    }

    [Fact]
    public async Task FailedWeekReplacementRestoresOriginalFieldsAndAllWeeks()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var uow = sp.GetRequiredService<IUnitOfWork>();
        var repo = sp.GetRequiredService<ICourseRepository>();
        var id = (await sp.GetRequiredService<ICourseCommand>().CreateAsync(Draft(), Ct)).CourseId!;
        var before = (await repo.FindAsync(id, Ct))!;
        await Assert.ThrowsAsync<CampusStoreUnavailableException>(() => uow.ExecuteAsync(async ct =>
        {
            await repo.UpdateAsync(before with { Name = "不应保存", WeekNumbers = new[] { 1, 31 } }, ct);
            return true;
        }, Ct));
        var after = (await repo.FindAsync(id, Ct))!;
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.WeekNumbers, after.WeekNumbers);
    }

    [Fact]
    public async Task UnitOfWorkPreservesOriginalExceptionAndRollsBack()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var expected = new InvalidOperationException("expected sentinel");
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => sp.GetRequiredService<IUnitOfWork>()
            .ExecuteAsync<bool>(async ct =>
            {
                await sp.GetRequiredService<ICourseRepository>().AddAsync(Model("rollback-sentinel"), ct);
                throw expected;
            }, Ct));
        Assert.Same(expected, thrown);
        Assert.Null(await sp.GetRequiredService<ICourseRepository>().FindAsync("rollback-sentinel", Ct));
    }

    [Fact]
    public async Task CancellationDuringWriteRollsBackWithUncancelledCleanup()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sp.GetRequiredService<IUnitOfWork>()
            .ExecuteAsync(async ct =>
            {
                await sp.GetRequiredService<ICourseRepository>().AddAsync(Model("cancelled-write"), ct);
                await sp.GetRequiredService<IEnrollmentRepository>().AddAsync("demo-001", "cancelled-write", ct);
                cancellation.Cancel();
                ct.ThrowIfCancellationRequested();
                return true;
            }, cancellation.Token));
        Assert.Null(await sp.GetRequiredService<ICourseRepository>().FindAsync("cancelled-write", Ct));
        Assert.Equal(CourseWriteStatus.Ok, (await sp.GetRequiredService<ICourseCommand>().CreateAsync(Draft(), Ct)).Status);
    }

    [Fact]
    public async Task EnrollmentAddIsIdempotentWithoutIgnoringForeignKeyFailures()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var enrollments = sp.GetRequiredService<IEnrollmentRepository>();
        await sp.GetRequiredService<IUnitOfWork>().ExecuteAsync(async ct =>
        {
            await enrollments.AddAsync("demo-001", "course-001", ct);
            await enrollments.AddAsync("demo-001", "course-001", ct);
            return true;
        }, Ct);
        Assert.Equal(2, await enrollments.CountByCourseAsync("course-001", Ct));
        Assert.Equal(1, await enrollments.CountOthersByCourseAsync("course-001", "demo-001", Ct));
        await Assert.ThrowsAsync<CampusStoreUnavailableException>(() => sp.GetRequiredService<IUnitOfWork>()
            .ExecuteAsync(async ct => { await enrollments.AddAsync("missing", "course-001", ct); return true; }, Ct));
    }

    [Fact]
    public async Task RepositoryWritesRequireAnExplicitUnitOfWork()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var courses = sp.GetRequiredService<ICourseRepository>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => courses.AddAsync(Model("outside-transaction"), Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => courses.UpdateAsync(Model("course-001"), Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => courses.DeleteAsync("course-004", Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sp.GetRequiredService<IEnrollmentRepository>().AddAsync("demo-001", "course-001", Ct));
        Assert.Null(await courses.FindAsync("outside-transaction", Ct));
    }

    [Fact]
    public async Task NestedUnitOfWorkIsRejectedAndOuterWriteIsRolledBack()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var uow = sp.GetRequiredService<IUnitOfWork>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteAsync(async ct =>
        {
            await sp.GetRequiredService<ICourseRepository>().AddAsync(Model("nested"), ct);
            return await uow.ExecuteAsync(_ => Task.FromResult(true), ct);
        }, Ct));
        Assert.Null(await sp.GetRequiredService<ICourseRepository>().FindAsync("nested", Ct));
    }

    [Fact]
    public async Task UninitializedDatabaseErrorsAreWrappedByEveryRepositoryAndUnitOfWork()
    {
        await using var uninitialized = BuildServices(Path.Combine(_root, "missing-parent", "campus.db"));
        await using var scope = uninitialized.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        Func<Task>[] operations =
        {
            () => sp.GetRequiredService<IAppSettingsRepository>().GetActiveDemoStudentIdAsync(Ct),
            () => sp.GetRequiredService<IStudentRepository>().FindAsync("demo-001", Ct),
            () => sp.GetRequiredService<ITermRepository>().GetAllAsync(Ct),
            () => sp.GetRequiredService<ICourseRepository>().FindAsync("course-001", Ct),
            () => sp.GetRequiredService<IEnrollmentRepository>().CountByCourseAsync("course-001", Ct),
            () => sp.GetRequiredService<IGradeRepository>().GetByStudentAndTermAsync("demo-001", "term-2026-fall", Ct),
            () => sp.GetRequiredService<IExamRepository>().GetByStudentAndTermAsync("demo-001", "term-2026-fall", Ct),
            () => sp.GetRequiredService<ILinkRepository>().GetAllAsync(Ct),
            () => sp.GetRequiredService<IUnitOfWork>().ExecuteAsync(_ => Task.FromResult(true), Ct)
        };
        foreach (var operation in operations)
        {
            var error = await Assert.ThrowsAsync<CampusStoreUnavailableException>(operation);
            Assert.DoesNotContain(_root, error.Message);
        }
    }

    [Fact]
    public async Task QueryAndWriteBusinessValidationRejectUnknownTermsAndConflictsWithoutWriting()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        await Assert.ThrowsAsync<CampusResourceNotFoundException>(() => sp.GetRequiredService<ICourseQuery>().GetByTermAsync("missing", Ct));
        Assert.Equal(CourseWriteStatus.UnknownTerm,
            (await sp.GetRequiredService<ICourseCommand>().CreateAsync(Draft() with { TermId = "missing" }, Ct)).Status);
        var conflicting = Draft() with { DayOfWeek = DayOfWeek.Monday, StartSection = 1, EndSection = 2 };
        Assert.Equal(CourseWriteStatus.ValidationFailed,
            (await sp.GetRequiredService<ICourseCommand>().CreateAsync(conflicting, Ct)).Status);
        var db = sp.GetRequiredService<CampusDbContext>();
        Assert.Equal(4, await db.Courses.CountAsync());
    }

    [Theory]
    [InlineData("name")]
    [InlineData("weekNumbers")]
    [InlineData("dayOfWeek")]
    [InlineData("startSection")]
    [InlineData("endSection")]
    public async Task InvalidDraftsDoNotWriteDatabase(string invalidField)
    {
        await using var scope = _services.CreateAsyncScope();
        var draft = invalidField switch
        {
            "name" => Draft() with { Name = " " },
            "weekNumbers" => Draft() with { WeekNumbers = new[] { 19 } },
            "dayOfWeek" => Draft() with { DayOfWeek = (DayOfWeek)99 },
            "startSection" => Draft() with { StartSection = 0 },
            _ => Draft() with { EndSection = 1 }
        };
        var result = await scope.ServiceProvider.GetRequiredService<ICourseCommand>().CreateAsync(draft, Ct);
        Assert.Equal(CourseWriteStatus.ValidationFailed, result.Status);
        Assert.Equal(invalidField, result.Field);
        Assert.Equal(4, await scope.ServiceProvider.GetRequiredService<CampusDbContext>().Courses.CountAsync());
    }

    [Fact]
    public async Task CancelledRepositoryCallDoesNotBecomeUnavailableError()
    {
        await using var scope = _services.CreateAsyncScope();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.ServiceProvider.GetRequiredService<ICourseRepository>()
            .GetByStudentAndTermAsync("demo-001", "term-2026-fall", cancelled.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrInvalidActiveStudentIsReportedWithoutReseeding(bool missingKey)
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CampusDbContext>();
        var setting = (await db.AppSettings.FindAsync("active_demo_student_id"))!;
        if (missingKey) db.AppSettings.Remove(setting);
        else setting.SettingValue = "nonexistent-student";
        await db.SaveChangesAsync();
        if (missingKey) Assert.Null(await sp.GetRequiredService<IAppSettingsRepository>().GetActiveDemoStudentIdAsync(Ct));
        await Assert.ThrowsAsync<CampusStoreUnavailableException>(() => sp.GetRequiredService<IProfileQuery>().GetActiveAsync(Ct));
        Assert.Equal(4, await db.Courses.CountAsync());
    }

    [Fact]
    public async Task BusyWriteTransactionIsWrappedAndCanBeRetriedAfterUnlocking()
    {
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        await using var locker = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
        await locker.OpenAsync();
        await using var command = locker.CreateCommand();
        command.CommandText = "BEGIN IMMEDIATE";
        await command.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<CampusStoreUnavailableException>(() => sp.GetRequiredService<IUnitOfWork>()
            .ExecuteAsync(_ => Task.FromResult(true), Ct));
        command.CommandText = "ROLLBACK";
        await command.ExecuteNonQueryAsync();
        Assert.Equal(CourseWriteStatus.Ok, (await sp.GetRequiredService<ICourseCommand>().CreateAsync(Draft(), Ct)).Status);
    }
}
