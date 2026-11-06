namespace OpenAHU.Application.Abstractions;

/// <summary>
/// 事务边界。业务层用它把“多条写操作”包成一个原子单元，而无需知道底层是 EF Core 还是别的什么。
/// </summary>
/// <remarks>
/// <b>05 号实现要求</b>：用 EF Core 的 <c>Database.BeginTransactionAsync()</c> 包住 <c>operation</c>，
/// 正常返回则 <c>CommitAsync</c>，抛出任何异常或取消则 <c>RollbackAsync</c> 并原样向上抛。
/// 实现必须是 <b>Scoped</b> 生命周期，与被包装的仓储共享同一个 <c>DbContext</c> 实例，
/// 否则各仓储会各自开连接，事务包不住它们（这是最常见的踩坑点）。
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>在单个事务中执行一组写操作。</summary>
    /// <param name="operation">
    /// 写操作集合。参数是事务令牌，实现时应把它与外部 <paramref name="cancellationToken"/> 合并后传给仓储。
    /// </param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <typeparam name="T">操作返回值类型。</typeparam>
    /// <returns>操作结果。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用（实现负责包装）。</exception>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
}
