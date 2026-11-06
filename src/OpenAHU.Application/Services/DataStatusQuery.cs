using OpenAHU.Application.Abstractions.Queries;

namespace OpenAHU.Application.Services;

/// <summary>数据状态查询。<see cref="IDataStatusQuery"/> 的唯一实现。</summary>
/// <remarks>
/// 首版<b>不访问数据库</b>：schema 没有 <c>updated_at</c> 列，唯一诚实的结果就是“不可得”。
/// 一旦 05 号迁移加了时间列，把这里换成仓储查询即可，接口与 DTO 都不用动。
/// </remarks>
public sealed class DataStatusQuery : IDataStatusQuery
{
    /// <inheritdoc />
    public Task<DateTimeOffset?> GetLastUpdatedAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<DateTimeOffset?>(null);
    }
}
