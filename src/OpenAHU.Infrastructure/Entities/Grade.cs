namespace OpenAHU.Infrastructure.Entities;

public sealed class Grade
{
    public string StudentId { get; set; } = "";
    public string CourseId { get; set; } = "";
    public decimal Credits { get; set; }
    public decimal? Score { get; set; }
    public decimal? GradePoint { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
}
