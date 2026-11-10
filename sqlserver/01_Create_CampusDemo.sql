/*
 OpenAHU Windows - SQL Server 演示数据库初始化
 仅用于本地开发/课堂演示。无任何真实学生数据。
 在 SSMS 22 中连接你自己的 SQL Server 实例，整体执行此脚本。
 如现有表结构不完整，本脚本不会删除任何数据，而是抛出错误。
*/
IF DB_ID(N'OpenAHU_CampusDemo') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE [OpenAHU_CampusDemo]');
END;
GO
USE [OpenAHU_CampusDemo];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @ExistingTableCount INT =
(
  SELECT COUNT(*) FROM sys.tables
  WHERE schema_id = SCHEMA_ID(N'dbo')
    AND name IN (N'students',N'terms',N'courses',N'course_weeks',
                 N'enrollments',N'grades',N'exams',N'campus_links',N'app_settings')
);
IF @ExistingTableCount = 9
BEGIN
    PRINT N'九张数据表已经存在；未修改任何现有数据。';
END
ELSE IF @ExistingTableCount > 0
BEGIN
    THROW 51001, N'发现部分业务表，拒绝覆盖；请先核对现有数据库。', 1;
END
ELSE
BEGIN
    BEGIN TRANSACTION;
    CREATE TABLE dbo.students (
        id NVARCHAR(100) NOT NULL CONSTRAINT PK_students PRIMARY KEY,
        display_name NVARCHAR(120) NOT NULL,
        student_number NVARCHAR(50) NULL,
        college NVARCHAR(120) NULL,
        major NVARCHAR(120) NULL,
        grade_year INT NULL,
        created_at_utc NVARCHAR(40) NOT NULL
    );
    CREATE UNIQUE INDEX UX_students_number ON dbo.students(student_number)
      WHERE student_number IS NOT NULL;

    CREATE TABLE dbo.terms (
        id NVARCHAR(100) NOT NULL CONSTRAINT PK_terms PRIMARY KEY,
        name NVARCHAR(120) NOT NULL,
        start_date NVARCHAR(10) NOT NULL,
        week_count INT NOT NULL CONSTRAINT CK_terms_weeks CHECK(week_count BETWEEN 1 AND 30),
        is_current BIT NOT NULL CONSTRAINT DF_terms_current DEFAULT(0)
    );

    CREATE TABLE dbo.courses (
        id NVARCHAR(100) NOT NULL CONSTRAINT PK_courses PRIMARY KEY,
        term_id NVARCHAR(100) NOT NULL,
        course_code NVARCHAR(60) NULL,
        name NVARCHAR(120) NOT NULL,
        teacher NVARCHAR(100) NOT NULL CONSTRAINT DF_courses_teacher DEFAULT(N''),
        location NVARCHAR(150) NOT NULL CONSTRAINT DF_courses_location DEFAULT(N''),
        day_of_week INT NOT NULL CONSTRAINT CK_courses_day CHECK(day_of_week BETWEEN 1 AND 7),
        start_section INT NOT NULL CONSTRAINT CK_courses_start CHECK(start_section BETWEEN 1 AND 20),
        end_section INT NOT NULL,
        CONSTRAINT CK_courses_end CHECK(end_section BETWEEN start_section AND 20),
        CONSTRAINT FK_courses_terms FOREIGN KEY(term_id) REFERENCES dbo.terms(id)
    );
    CREATE INDEX idx_courses_term_schedule ON dbo.courses(term_id,day_of_week,start_section);

    CREATE TABLE dbo.course_weeks (
        course_id NVARCHAR(100) NOT NULL,
        week_number INT NOT NULL CONSTRAINT CK_course_weeks_range CHECK(week_number BETWEEN 1 AND 30),
        CONSTRAINT PK_course_weeks PRIMARY KEY(course_id,week_number),
        CONSTRAINT FK_course_weeks_courses FOREIGN KEY(course_id)
          REFERENCES dbo.courses(id) ON DELETE CASCADE
    );

    CREATE TABLE dbo.enrollments (
        student_id NVARCHAR(100) NOT NULL,
        course_id NVARCHAR(100) NOT NULL,
        CONSTRAINT PK_enrollments PRIMARY KEY(student_id,course_id),
        CONSTRAINT FK_enrollments_students FOREIGN KEY(student_id)
          REFERENCES dbo.students(id) ON DELETE CASCADE,
        CONSTRAINT FK_enrollments_courses FOREIGN KEY(course_id)
          REFERENCES dbo.courses(id) ON DELETE CASCADE
    );
    CREATE INDEX idx_enrollments_student ON dbo.enrollments(student_id);

    CREATE TABLE dbo.grades (
        student_id NVARCHAR(100) NOT NULL,
        course_id NVARCHAR(100) NOT NULL,
        credits FLOAT NOT NULL CONSTRAINT CK_grades_credits CHECK(credits BETWEEN 0 AND 50),
        score FLOAT NULL CONSTRAINT CK_grades_score CHECK(score BETWEEN 0 AND 100),
        grade_point FLOAT NULL CONSTRAINT CK_grades_points CHECK(grade_point BETWEEN 0 AND 5),
        CONSTRAINT PK_grades PRIMARY KEY(student_id,course_id),
        CONSTRAINT FK_grades_enrollments FOREIGN KEY(student_id,course_id)
          REFERENCES dbo.enrollments(student_id,course_id) ON DELETE CASCADE
    );

    CREATE TABLE dbo.exams (
        id NVARCHAR(100) NOT NULL CONSTRAINT PK_exams PRIMARY KEY,
        course_id NVARCHAR(100) NOT NULL,
        starts_at NVARCHAR(40) NULL,
        ends_at NVARCHAR(40) NULL,
        location NVARCHAR(150) NULL,
        CONSTRAINT CK_exams_times CHECK(starts_at IS NULL OR ends_at IS NULL OR ends_at > starts_at),
        CONSTRAINT FK_exams_courses FOREIGN KEY(course_id)
          REFERENCES dbo.courses(id) ON DELETE CASCADE
    );
    CREATE INDEX idx_exams_course ON dbo.exams(course_id);

    CREATE TABLE dbo.campus_links (
        id NVARCHAR(100) NOT NULL CONSTRAINT PK_campus_links PRIMARY KEY,
        title NVARCHAR(120) NOT NULL,
        url NVARCHAR(1000) NOT NULL,
        category NVARCHAR(50) NOT NULL CONSTRAINT DF_campus_links_category DEFAULT(N'常用')
    );

    CREATE TABLE dbo.app_settings (
        setting_key NVARCHAR(120) NOT NULL CONSTRAINT PK_app_settings PRIMARY KEY,
        setting_value NVARCHAR(400) NOT NULL
    );
    COMMIT TRANSACTION;
    PRINT N'已成功创建OpenAHU_CampusDemo的9张业务表。启动客户端后将自动注入虚构演示数据。';
END;
GO
SELECT name AS [数据表] FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo') ORDER BY name;
GO
