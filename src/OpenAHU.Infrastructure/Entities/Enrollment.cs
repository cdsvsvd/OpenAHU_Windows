namespace OpenAHU.Infrastructure.Entities;

public sealed class Enrollment
{
    public string StudentId { get; set; } = "";
    public string CourseId { get; set; } = "";
    public Student Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public Grade? Grade { get; set; }
}
