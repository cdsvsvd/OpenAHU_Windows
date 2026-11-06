using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OpenAHU.Infrastructure.Entities;

namespace OpenAHU.Infrastructure.Persistence;

public sealed class CampusDbContext(DbContextOptions<CampusDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Term> Terms => Set<Term>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseWeek> CourseWeeks => Set<CourseWeek>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<CampusLink> CampusLinks => Set<CampusLink>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var isoTime = new ValueConverter<DateTimeOffset, string>(
            value => value.ToString("O", CultureInfo.InvariantCulture),
            value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.None));

        model.Entity<Student>(b =>
        {
            b.ToTable("students");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.StudentNumber).IsUnique().HasDatabaseName("idx_students_number");
            b.Property(x => x.DisplayName).IsRequired();
            b.Property(x => x.CreatedAtUtc).HasConversion(isoTime);
        });
        model.Entity<Term>(b =>
        {
            b.ToTable("terms", table =>
            {
                table.HasCheckConstraint("ck_terms_week_count", "week_count BETWEEN 1 AND 30");
                table.HasCheckConstraint("ck_terms_is_current", "is_current IN (0,1)");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired();
            b.Property(x => x.IsCurrent).HasDefaultValue(false);
            b.HasIndex(x => x.IsCurrent).IsUnique().HasFilter("is_current = 1")
                .HasDatabaseName("idx_terms_one_current");
        });
        model.Entity<Course>(b =>
        {
            b.ToTable("courses", table =>
            {
                table.HasCheckConstraint("ck_courses_day", "day_of_week BETWEEN 1 AND 7");
                table.HasCheckConstraint("ck_courses_start_section", "start_section BETWEEN 1 AND 20");
                table.HasCheckConstraint("ck_courses_end_section", "end_section BETWEEN start_section AND 20");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired();
            b.Property(x => x.Teacher).IsRequired().HasDefaultValue("");
            b.Property(x => x.Location).IsRequired().HasDefaultValue("");
            b.HasOne(x => x.Term).WithMany(x => x.Courses).HasForeignKey(x => x.TermId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TermId, x.DayOfWeek, x.StartSection })
                .HasDatabaseName("idx_courses_term_schedule");
        });
        model.Entity<CourseWeek>(b =>
        {
            b.ToTable("course_weeks", table =>
                table.HasCheckConstraint("ck_course_weeks_number", "week_number BETWEEN 1 AND 30"));
            b.HasKey(x => new { x.CourseId, x.WeekNumber });
            b.HasOne(x => x.Course).WithMany(x => x.Weeks).HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<Enrollment>(b =>
        {
            b.ToTable("enrollments");
            b.HasKey(x => new { x.StudentId, x.CourseId });
            b.HasOne(x => x.Student).WithMany(x => x.Enrollments).HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Course).WithMany(x => x.Enrollments).HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.StudentId).HasDatabaseName("idx_enrollments_student");
        });
        model.Entity<Grade>(b =>
        {
            b.ToTable("grades", table =>
            {
                table.HasCheckConstraint("ck_grades_credits", "credits BETWEEN 0 AND 50");
                table.HasCheckConstraint("ck_grades_score", "score IS NULL OR score BETWEEN 0 AND 100");
                table.HasCheckConstraint("ck_grades_point", "grade_point IS NULL OR grade_point BETWEEN 0 AND 5");
            });
            b.HasKey(x => new { x.StudentId, x.CourseId });
            // Keep the reference schema's REAL affinity; C# and the repository boundary use decimal.
            b.Property(x => x.Credits).HasColumnType("REAL");
            b.Property(x => x.Score).HasColumnType("REAL");
            b.Property(x => x.GradePoint).HasColumnType("REAL");
            b.HasOne(x => x.Enrollment).WithOne(x => x.Grade)
                .HasForeignKey<Grade>(x => new { x.StudentId, x.CourseId })
                .OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<Exam>(b =>
        {
            b.ToTable("exams", table =>
            {
                table.HasCheckConstraint("ck_exams_start", "starts_at IS NULL OR julianday(starts_at) IS NOT NULL");
                table.HasCheckConstraint("ck_exams_end", "ends_at IS NULL OR julianday(ends_at) IS NOT NULL");
                // Compare instants, not lexicographic ISO strings with potentially different offsets.
                table.HasCheckConstraint("ck_exams_order",
                    "starts_at IS NULL OR ends_at IS NULL OR julianday(ends_at) > julianday(starts_at)");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.StartsAt).HasConversion(isoTime);
            b.Property(x => x.EndsAt).HasConversion(isoTime);
            b.HasOne(x => x.Course).WithMany(x => x.Exams).HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.CourseId).HasDatabaseName("idx_exams_course");
        });
        model.Entity<CampusLink>(b =>
        {
            b.ToTable("campus_links");
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).IsRequired();
            b.Property(x => x.Url).IsRequired();
            b.Property(x => x.Category).IsRequired().HasDefaultValue("常用");
        });
        model.Entity<AppSetting>(b =>
        {
            b.ToTable("app_settings");
            b.HasKey(x => x.SettingKey);
            b.Property(x => x.SettingValue).IsRequired();
        });

        foreach (var entity in model.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
            property.SetColumnName(ToSnakeCase(property.Name));
    }

    private static string ToSnakeCase(string name) => string.Concat(name.Select((letter, index) =>
        char.IsUpper(letter) ? (index > 0 ? "_" : "") + char.ToLowerInvariant(letter) : letter.ToString()));
}
