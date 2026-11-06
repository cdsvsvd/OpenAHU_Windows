using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Models;

namespace OpenAHU.Application.Tests.Fakes;

/// <summary>假设置仓储：直接读 <see cref="FakeStore.Settings"/>。</summary>
internal sealed class FakeAppSettingsRepository(FakeStore store) : IAppSettingsRepository
{
    public Task<string?> GetActiveDemoStudentIdAsync(CancellationToken cancellationToken)
    {
        store.Settings.TryGetValue("active_demo_student_id", out var value);
        return Task.FromResult(value);
    }
}

/// <summary>假学生仓储。</summary>
internal sealed class FakeStudentRepository(FakeStore store) : IStudentRepository
{
    public Task<Student?> FindAsync(string studentId, CancellationToken cancellationToken)
    {
        return Task.FromResult(store.Students.FirstOrDefault(x => x.Id == studentId));
    }
}

/// <summary>假学期仓储。刻意不排序，用来验证业务层自己会排序。</summary>
internal sealed class FakeTermRepository(FakeStore store) : ITermRepository
{
    public Task<IReadOnlyList<Term>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Term>>(store.Terms.ToList());
    }

    public Task<Term?> FindAsync(string termId, CancellationToken cancellationToken)
    {
        return Task.FromResult(store.Terms.FirstOrDefault(x => x.Id == termId));
    }
}

/// <summary>假课程仓储，记录写入次数供断言。</summary>
internal sealed class FakeCourseRepository(FakeStore store) : ICourseRepository
{
    public int AddCalls { get; private set; }

    public int UpdateCalls { get; private set; }

    public int DeleteCalls { get; private set; }

    public Task<IReadOnlyList<Course>> GetByStudentAndTermAsync(
        string studentId,
        string termId,
        CancellationToken cancellationToken)
    {
        var courses = store.Courses
            .Where(course => course.TermId == termId
                && store.Enrollments.Contains((studentId, course.Id)))
            .ToList();

        return Task.FromResult<IReadOnlyList<Course>>(courses);
    }

    public Task<Course?> FindAsync(string courseId, CancellationToken cancellationToken)
    {
        return Task.FromResult(store.Courses.FirstOrDefault(x => x.Id == courseId));
    }

    public Task AddAsync(Course course, CancellationToken cancellationToken)
    {
        AddCalls++;
        store.Courses.Add(course);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Course course, CancellationToken cancellationToken)
    {
        UpdateCalls++;
        var index = store.Courses.FindIndex(x => x.Id == course.Id);
        if (index >= 0)
        {
            store.Courses[index] = course;
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string courseId, CancellationToken cancellationToken)
    {
        DeleteCalls++;
        store.BeforeCourseDelete?.Invoke();
        var removed = store.Courses.RemoveAll(x => x.Id == courseId) > 0;
        store.Enrollments.RemoveWhere(x => x.CourseId == courseId);
        return Task.FromResult(removed);
    }
}

/// <summary>假选课仓储。</summary>
internal sealed class FakeEnrollmentRepository(FakeStore store) : IEnrollmentRepository
{
    public Task AddAsync(string studentId, string courseId, CancellationToken cancellationToken)
    {
        store.Enrollments.Add((studentId, courseId));
        return Task.CompletedTask;
    }

    public Task<int> CountByCourseAsync(string courseId, CancellationToken cancellationToken)
    {
        return Task.FromResult(store.Enrollments.Count(x => x.CourseId == courseId));
    }

    public Task<int> CountOthersByCourseAsync(
        string courseId,
        string excludedStudentId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(store.Enrollments.Count(
            x => x.CourseId == courseId && x.StudentId != excludedStudentId));
    }
}

/// <summary>假成绩仓储。</summary>
internal sealed class FakeGradeRepository(FakeStore store) : IGradeRepository
{
    public Task<IReadOnlyList<Grade>> GetByStudentAndTermAsync(
        string studentId,
        string termId,
        CancellationToken cancellationToken)
    {
        var grades = new List<Grade>();
        foreach (var grade in store.Grades)
        {
            var course = store.Courses.FirstOrDefault(x => x.Id == grade.CourseId);
            if (course is null || course.TermId != termId)
            {
                continue;
            }

            if (!store.Enrollments.Contains((studentId, course.Id)))
            {
                continue;
            }

            grades.Add(grade with { CourseName = course.Name, TermId = course.TermId });
        }

        return Task.FromResult<IReadOnlyList<Grade>>(grades);
    }

    public Task<bool> ExistsByCourseAsync(string courseId, CancellationToken cancellationToken)
    {
        return Task.FromResult(store.Grades.Any(x => x.CourseId == courseId));
    }
}

/// <summary>假考试仓储。</summary>
internal sealed class FakeExamRepository(FakeStore store) : IExamRepository
{
    public Task<IReadOnlyList<Exam>> GetByStudentAndTermAsync(
        string studentId,
        string termId,
        CancellationToken cancellationToken)
    {
        var exams = new List<Exam>();
        foreach (var (courseId, exam) in store.Exams)
        {
            var course = store.Courses.FirstOrDefault(x => x.Id == courseId);
            if (course is null || course.TermId != termId)
            {
                continue;
            }

            if (!store.Enrollments.Contains((studentId, courseId)))
            {
                continue;
            }

            exams.Add(exam with { CourseName = course.Name, TermId = course.TermId });
        }

        return Task.FromResult<IReadOnlyList<Exam>>(exams);
    }

    public Task<bool> ExistsByCourseAsync(string courseId, CancellationToken cancellationToken)
    {
        return Task.FromResult(store.Exams.Any(x => x.CourseId == courseId));
    }
}

/// <summary>假链接仓储。</summary>
internal sealed class FakeLinkRepository(FakeStore store) : ILinkRepository
{
    public Task<IReadOnlyList<CampusLink>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<CampusLink>>(store.Links.ToList());
    }
}

/// <summary>
/// 假事务：快照 → 执行 → 异常则还原。
/// 用于验证两件事：① 写链路确实把多条写入交给了事务；② 失败时不会留下半套数据。
/// </summary>
internal sealed class FakeUnitOfWork(FakeStore store) : IUnitOfWork
{
    public int Executions { get; private set; }

    public int Rollbacks { get; private set; }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        Executions++;
        var snapshot = store.Capture();
        try
        {
            return await operation(cancellationToken);
        }
        catch
        {
            store.Restore(snapshot);
            Rollbacks++;
            throw;
        }
    }
}
