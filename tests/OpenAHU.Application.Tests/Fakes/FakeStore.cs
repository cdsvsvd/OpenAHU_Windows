using OpenAHU.Application.Models;

namespace OpenAHU.Application.Tests.Fakes;

/// <summary>
/// 内存版“数据库”，供假仓储共享。所有假仓储读写同一份集合，因此能测出跨仓储的业务流程。
/// </summary>
internal sealed class FakeStore
{
    public List<Student> Students { get; } = new();

    public List<Term> Terms { get; } = new();

    public List<Course> Courses { get; } = new();

    public List<Grade> Grades { get; } = new();

    /// <summary>
    /// 考试与其所属课程的关联。领域模型 <see cref="Exam"/> 不含 <c>CourseId</c>（与 <c>ExamDto</c> 保持一致），
    /// 因此假数据把归属关系单独记一份，模拟数据库里 <c>exams.course_id</c> 的外键。
    /// </summary>
    public List<(string CourseId, Exam Exam)> Exams { get; } = new();

    public List<CampusLink> Links { get; } = new();

    public Dictionary<string, string> Settings { get; } = new(StringComparer.Ordinal);

    /// <summary>选课关系，键为 (studentId, courseId)。</summary>
    public HashSet<(string StudentId, string CourseId)> Enrollments { get; } = new();

    /// <summary>检查并写入之间是否发生过“别人抢先写入”——用于模拟事务内检查的价值。</summary>
    public Action? BeforeCourseDelete { get; set; }

    /// <summary>抓取快照，供假事务在异常时回滚。</summary>
    /// <returns>与当前状态等价的一份拷贝。</returns>
    public State Capture() => new()
    {
        Students = Students.ToList(),
        Terms = Terms.ToList(),
        Courses = Courses.ToList(),
        Grades = Grades.ToList(),
        Exams = Exams.ToList(),
        Links = Links.ToList(),
        Enrollments = new HashSet<(string, string)>(Enrollments),
        Settings = new Dictionary<string, string>(Settings, StringComparer.Ordinal),
    };

    /// <summary>用快照覆盖当前状态。</summary>
    /// <param name="state">先前通过 <see cref="Capture"/> 取得的状态。</param>
    public void Restore(State state)
    {
        Students.Clear();
        Students.AddRange(state.Students);
        Terms.Clear();
        Terms.AddRange(state.Terms);
        Courses.Clear();
        Courses.AddRange(state.Courses);
        Grades.Clear();
        Grades.AddRange(state.Grades);
        Exams.Clear();
        Exams.AddRange(state.Exams);
        Links.Clear();
        Links.AddRange(state.Links);
        Enrollments.Clear();
        Enrollments.UnionWith(state.Enrollments);
        Settings.Clear();
        foreach (var pair in state.Settings)
        {
            Settings[pair.Key] = pair.Value;
        }
    }

    /// <summary>状态快照。</summary>
    public sealed class State
    {
        public List<Student> Students { get; init; } = new();

        public List<Term> Terms { get; init; } = new();

        public List<Course> Courses { get; init; } = new();

        public List<Grade> Grades { get; init; } = new();

        public List<(string CourseId, Exam Exam)> Exams { get; init; } = new();

        public List<CampusLink> Links { get; init; } = new();

        public HashSet<(string StudentId, string CourseId)> Enrollments { get; init; } = new();

        public Dictionary<string, string> Settings { get; init; } = new(StringComparer.Ordinal);
    }
}
