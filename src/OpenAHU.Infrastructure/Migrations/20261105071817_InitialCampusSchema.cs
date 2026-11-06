using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenAHU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCampusSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    setting_key = table.Column<string>(type: "TEXT", nullable: false),
                    setting_value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_settings", x => x.setting_key);
                });

            migrationBuilder.CreateTable(
                name: "campus_links",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    url = table.Column<string>(type: "TEXT", nullable: false),
                    category = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "常用")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campus_links", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "students",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", nullable: false),
                    student_number = table.Column<string>(type: "TEXT", nullable: true),
                    college = table.Column<string>(type: "TEXT", nullable: true),
                    major = table.Column<string>(type: "TEXT", nullable: true),
                    grade_year = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_students", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "terms",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    start_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    week_count = table.Column<int>(type: "INTEGER", nullable: false),
                    is_current = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terms", x => x.id);
                    table.CheckConstraint("ck_terms_is_current", "is_current IN (0,1)");
                    table.CheckConstraint("ck_terms_week_count", "week_count BETWEEN 1 AND 30");
                });

            migrationBuilder.CreateTable(
                name: "courses",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    term_id = table.Column<string>(type: "TEXT", nullable: false),
                    course_code = table.Column<string>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    teacher = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    location = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    day_of_week = table.Column<int>(type: "INTEGER", nullable: false),
                    start_section = table.Column<int>(type: "INTEGER", nullable: false),
                    end_section = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courses", x => x.id);
                    table.CheckConstraint("ck_courses_day", "day_of_week BETWEEN 1 AND 7");
                    table.CheckConstraint("ck_courses_end_section", "end_section BETWEEN start_section AND 20");
                    table.CheckConstraint("ck_courses_start_section", "start_section BETWEEN 1 AND 20");
                    table.ForeignKey(
                        name: "FK_courses_terms_term_id",
                        column: x => x.term_id,
                        principalTable: "terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_weeks",
                columns: table => new
                {
                    course_id = table.Column<string>(type: "TEXT", nullable: false),
                    week_number = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_course_weeks", x => new { x.course_id, x.week_number });
                    table.CheckConstraint("ck_course_weeks_number", "week_number BETWEEN 1 AND 30");
                    table.ForeignKey(
                        name: "FK_course_weeks_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "enrollments",
                columns: table => new
                {
                    student_id = table.Column<string>(type: "TEXT", nullable: false),
                    course_id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enrollments", x => new { x.student_id, x.course_id });
                    table.ForeignKey(
                        name: "FK_enrollments_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_enrollments_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exams",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    course_id = table.Column<string>(type: "TEXT", nullable: false),
                    starts_at = table.Column<string>(type: "TEXT", nullable: true),
                    ends_at = table.Column<string>(type: "TEXT", nullable: true),
                    location = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exams", x => x.id);
                    table.CheckConstraint("ck_exams_end", "ends_at IS NULL OR julianday(ends_at) IS NOT NULL");
                    table.CheckConstraint("ck_exams_order", "starts_at IS NULL OR ends_at IS NULL OR julianday(ends_at) > julianday(starts_at)");
                    table.CheckConstraint("ck_exams_start", "starts_at IS NULL OR julianday(starts_at) IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_exams_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grades",
                columns: table => new
                {
                    student_id = table.Column<string>(type: "TEXT", nullable: false),
                    course_id = table.Column<string>(type: "TEXT", nullable: false),
                    credits = table.Column<decimal>(type: "REAL", nullable: false),
                    score = table.Column<decimal>(type: "REAL", nullable: true),
                    grade_point = table.Column<decimal>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grades", x => new { x.student_id, x.course_id });
                    table.CheckConstraint("ck_grades_credits", "credits BETWEEN 0 AND 50");
                    table.CheckConstraint("ck_grades_point", "grade_point IS NULL OR grade_point BETWEEN 0 AND 5");
                    table.CheckConstraint("ck_grades_score", "score IS NULL OR score BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_grades_enrollments_student_id_course_id",
                        columns: x => new { x.student_id, x.course_id },
                        principalTable: "enrollments",
                        principalColumns: new[] { "student_id", "course_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_courses_term_schedule",
                table: "courses",
                columns: new[] { "term_id", "day_of_week", "start_section" });

            migrationBuilder.CreateIndex(
                name: "idx_enrollments_student",
                table: "enrollments",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_course_id",
                table: "enrollments",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "idx_exams_course",
                table: "exams",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "idx_students_number",
                table: "students",
                column: "student_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_terms_one_current",
                table: "terms",
                column: "is_current",
                unique: true,
                filter: "is_current = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "campus_links");

            migrationBuilder.DropTable(
                name: "course_weeks");

            migrationBuilder.DropTable(
                name: "exams");

            migrationBuilder.DropTable(
                name: "grades");

            migrationBuilder.DropTable(
                name: "enrollments");

            migrationBuilder.DropTable(
                name: "courses");

            migrationBuilder.DropTable(
                name: "students");

            migrationBuilder.DropTable(
                name: "terms");
        }
    }
}
