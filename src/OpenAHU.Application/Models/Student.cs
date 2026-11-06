namespace OpenAHU.Application.Models;

/// <summary>
/// 一名演示学生的资料（对应 SQLite <c>students</c> 表）。
/// 只承载数据库事实，不判断“是否演示”——那是 <see cref="CurrentProfile"/> 的语义。
/// </summary>
/// <param name="Id">学生主键，例如 <c>demo-001</c>。</param>
/// <param name="DisplayName">演示姓名。</param>
/// <param name="College">学院；数据库可空。</param>
/// <param name="Major">专业；数据库可空。</param>
/// <param name="GradeYear">入学年级；数据库可空。</param>
public sealed record Student(
    string Id,
    string DisplayName,
    string? College,
    string? Major,
    int? GradeYear);

/// <summary>
/// 当前本机演示资料（<c>GET /api/v1/profile</c> 的业务结果）。
/// </summary>
/// <param name="Id">学生主键。</param>
/// <param name="DisplayName">演示姓名。</param>
/// <param name="College">学院。</param>
/// <param name="Major">专业。</param>
/// <param name="GradeYear">入学年级。</param>
/// <param name="IsDemo">恒定 <c>true</c>：本版数据全部虚构，不是学校学籍。</param>
public sealed record CurrentProfile(
    string Id,
    string DisplayName,
    string? College,
    string? Major,
    int? GradeYear,
    bool IsDemo)
{
    /// <summary>由学生资料构造当前演示档案；<see cref="IsDemo"/> 恒定 <c>true</c>。</summary>
    /// <param name="student">数据库中的演示学生。</param>
    /// <returns>可直接映射为 <c>StudentProfileDto</c> 的只读模型。</returns>
    public static CurrentProfile From(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);
        return new CurrentProfile(
            student.Id,
            student.DisplayName,
            student.College,
            student.Major,
            student.GradeYear,
            IsDemo: true);
    }
}
