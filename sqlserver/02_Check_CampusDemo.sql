USE [OpenAHU_CampusDemo];
GO
-- 首次运行 OpenAHU.Desktop.exe 后，系统会自动录入完全虚构的演示数据。
SELECT DB_NAME() AS [数据库名称];
SELECT COUNT(*) AS [学生数] FROM dbo.students;
SELECT COUNT(*) AS [学期数] FROM dbo.terms;
SELECT COUNT(*) AS [课程数] FROM dbo.courses;
SELECT COUNT(*) AS [选课关系数] FROM dbo.enrollments;
SELECT COUNT(*) AS [成绩记录数] FROM dbo.grades;
SELECT COUNT(*) AS [考试安排数] FROM dbo.exams;
SELECT s.display_name AS [姓名], c.name AS [课程], g.credits AS [学分],
       g.score AS [成绩_未公布则为NULL]
FROM dbo.grades g
JOIN dbo.students s ON g.student_id=s.id
JOIN dbo.courses c ON g.course_id=c.id
ORDER BY s.id,c.name;
GO
