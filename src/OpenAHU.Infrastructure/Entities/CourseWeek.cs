namespace OpenAHU.Infrastructure.Entities;

public sealed class CourseWeek
{
    public string CourseId { get; set; } = "";
    public int WeekNumber { get; set; }
    public Course Course { get; set; } = null!;
}
