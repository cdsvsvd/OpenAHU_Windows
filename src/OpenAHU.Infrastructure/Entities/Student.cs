namespace OpenAHU.Infrastructure.Entities;

public sealed class Student
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? StudentNumber { get; set; }
    public string? College { get; set; }
    public string? Major { get; set; }
    public int? GradeYear { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
