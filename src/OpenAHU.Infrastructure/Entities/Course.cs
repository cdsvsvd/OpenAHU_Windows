namespace OpenAHU.Infrastructure.Entities;

public sealed class Course
{
    public string Id { get; set; } = "";
    public string TermId { get; set; } = "";
    public string? CourseCode { get; set; }
    public string Name { get; set; } = "";
    public string Teacher { get; set; } = "";
    public string Location { get; set; } = "";
    // Storage uses Monday=1 ... Sunday=7. Application's adapter will convert once to DayOfWeek.
    public int DayOfWeek { get; set; }
    public int StartSection { get; set; }
    public int EndSection { get; set; }
    public Term Term { get; set; } = null!;
    public ICollection<CourseWeek> Weeks { get; set; } = new List<CourseWeek>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
}
