namespace OpenAHU.Infrastructure.Entities;

public sealed class Exam
{
    public string Id { get; set; } = "";
    public string CourseId { get; set; } = "";
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public string? Location { get; set; }
    public Course Course { get; set; } = null!;
}
