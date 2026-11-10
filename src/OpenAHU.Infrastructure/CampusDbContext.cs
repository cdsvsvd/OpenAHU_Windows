using Microsoft.EntityFrameworkCore;

namespace OpenAHU.Infrastructure;

/// <summary>映射 v3 九张表；字段与 v3 SQL 参考结构保持一致。</summary>
public sealed class CampusDbContext : DbContext
{
    public CampusDbContext(DbContextOptions<CampusDbContext> options) : base(options) { }
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Term> Terms => Set<Term>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseWeek> CourseWeeks => Set<CourseWeek>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<CampusLink> CampusLinks => Set<CampusLink>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Student>(e => {
            e.ToTable("students"); e.HasKey(x=>x.Id);
            e.Property(x=>x.Id).HasColumnName("id");
            e.Property(x=>x.DisplayName).HasColumnName("display_name").IsRequired();
            e.Property(x=>x.StudentNumber).HasColumnName("student_number"); e.HasIndex(x=>x.StudentNumber).IsUnique();
            e.Property(x=>x.College).HasColumnName("college"); e.Property(x=>x.Major).HasColumnName("major");
            e.Property(x=>x.GradeYear).HasColumnName("grade_year"); e.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        });
        b.Entity<Term>(e => {
            e.ToTable("terms"); e.HasKey(x=>x.Id);
            e.Property(x=>x.Id).HasColumnName("id"); e.Property(x=>x.Name).HasColumnName("name").IsRequired();
            e.Property(x=>x.StartDate).HasColumnName("start_date").IsRequired();
            e.Property(x=>x.WeekCount).HasColumnName("week_count");
            e.Property(x=>x.IsCurrent).HasColumnName("is_current");
            e.ToTable(t => t.HasCheckConstraint("ck_terms_week_count", "week_count BETWEEN 1 AND 30"));
        });
        b.Entity<Course>(e => {
            e.ToTable("courses"); e.HasKey(x=>x.Id);
            e.Property(x=>x.Id).HasColumnName("id"); e.Property(x=>x.TermId).HasColumnName("term_id");
            e.Property(x=>x.CourseCode).HasColumnName("course_code");
            e.Property(x=>x.Name).HasColumnName("name").IsRequired();
            e.Property(x=>x.Teacher).HasColumnName("teacher").IsRequired();
            e.Property(x=>x.Location).HasColumnName("location").IsRequired();
            e.Property(x=>x.DayOfWeek).HasColumnName("day_of_week");
            e.Property(x=>x.StartSection).HasColumnName("start_section");
            e.Property(x=>x.EndSection).HasColumnName("end_section");
            e.HasOne<Term>().WithMany().HasForeignKey(x=>x.TermId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x=>new{x.TermId,x.DayOfWeek,x.StartSection}).HasDatabaseName("idx_courses_term_schedule");
            e.HasMany(x=>x.Weeks).WithOne().HasForeignKey(x=>x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x=>x.Enrollments).WithOne().HasForeignKey(x=>x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("ck_courses_day", "day_of_week BETWEEN 1 AND 7"));
            e.ToTable(t => t.HasCheckConstraint("ck_courses_sections", "start_section BETWEEN 1 AND 20 AND end_section BETWEEN start_section AND 20"));
        });
        b.Entity<CourseWeek>(e => {
            e.ToTable("course_weeks"); e.HasKey(x=>new{x.CourseId,x.WeekNumber});
            e.Property(x=>x.CourseId).HasColumnName("course_id");
            e.Property(x=>x.WeekNumber).HasColumnName("week_number");
            e.ToTable(t=>t.HasCheckConstraint("ck_course_weeks", "week_number BETWEEN 1 AND 30"));
        });
        b.Entity<Enrollment>(e => {
            e.ToTable("enrollments"); e.HasKey(x=>new{x.StudentId,x.CourseId});
            e.Property(x=>x.StudentId).HasColumnName("student_id");
            e.Property(x=>x.CourseId).HasColumnName("course_id");
            e.HasOne<Student>().WithMany().HasForeignKey(x=>x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x=>x.StudentId).HasDatabaseName("idx_enrollments_student");
        });
        b.Entity<Grade>(e => {
            e.ToTable("grades"); e.HasKey(x=>new{x.StudentId,x.CourseId});
            e.Property(x=>x.StudentId).HasColumnName("student_id");
            e.Property(x=>x.CourseId).HasColumnName("course_id");
            e.Property(x=>x.Credits).HasColumnName("credits").HasColumnType(Database.IsSqlServer()?"float":"REAL");
            e.Property(x=>x.Score).HasColumnName("score").HasColumnType(Database.IsSqlServer()?"float":"REAL");
            e.Property(x=>x.GradePoint).HasColumnName("grade_point").HasColumnType(Database.IsSqlServer()?"float":"REAL");
            e.HasOne<Enrollment>().WithOne().HasForeignKey<Grade>(x=>new{x.StudentId,x.CourseId}).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t=>t.HasCheckConstraint("ck_grades_range", "credits BETWEEN 0 AND 50 AND (score IS NULL OR score BETWEEN 0 AND 100) AND (grade_point IS NULL OR grade_point BETWEEN 0 AND 5)"));
        });
        b.Entity<Exam>(e => {
            e.ToTable("exams"); e.HasKey(x=>x.Id);
            e.Property(x=>x.Id).HasColumnName("id"); e.Property(x=>x.CourseId).HasColumnName("course_id");
            e.Property(x=>x.StartsAt).HasColumnName("starts_at"); e.Property(x=>x.EndsAt).HasColumnName("ends_at");
            e.Property(x=>x.Location).HasColumnName("location");
            e.HasOne<Course>().WithMany().HasForeignKey(x=>x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x=>x.CourseId).HasDatabaseName("idx_exams_course");
            e.ToTable(t=>t.HasCheckConstraint("ck_exams_times", "starts_at IS NULL OR ends_at IS NULL OR ends_at > starts_at"));
        });
        b.Entity<CampusLink>(e => {
            e.ToTable("campus_links"); e.HasKey(x=>x.Id);
            e.Property(x=>x.Id).HasColumnName("id"); e.Property(x=>x.Title).HasColumnName("title");
            e.Property(x=>x.Url).HasColumnName("url"); e.Property(x=>x.Category).HasColumnName("category");
        });
        b.Entity<AppSetting>(e => {
            e.ToTable("app_settings"); e.HasKey(x=>x.SettingKey);
            e.Property(x=>x.SettingKey).HasColumnName("setting_key"); e.Property(x=>x.SettingValue).HasColumnName("setting_value");
        });
    }
}
