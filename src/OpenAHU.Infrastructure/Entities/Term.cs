namespace OpenAHU.Infrastructure.Entities;

public sealed class Term
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public int WeekCount { get; set; }
    public bool IsCurrent { get; set; }
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
