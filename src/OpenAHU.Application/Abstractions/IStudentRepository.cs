using OpenAHU.Application.Models;

namespace OpenAHU.Application.Abstractions;

/// <summary>演示学生资料（<c>students</c> 表）的只读访问。</summary>
public interface IStudentRepository
{
    /// <summary>按主键读取一名学生。</summary>
    /// <param name="studentId">学生主键，例如 <c>demo-001</c>。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>学生资料；不存在时返回 <c>null</c>。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<Student?> FindAsync(string studentId, CancellationToken cancellationToken);
}
