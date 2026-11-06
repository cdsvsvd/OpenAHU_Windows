namespace OpenAHU.Application.Abstractions.Queries;

/// <summary>
/// 数据“最后更新时间”查询（供 <c>GET /api/v1/data-status</c> 的 <c>updatedAt</c> 字段）。
/// </summary>
public interface IDataStatusQuery
{
    /// <summary>
    /// 读取库内数据的最后更新时间。
    /// </summary>
    /// <remarks>
    /// <b>首版恒返回 <c>null</c></b>：现有 schema（<c>courses</c>/<c>grades</c>）没有任何 <c>updated_at</c> 列，
    /// 也不宜用数据库文件的 <c>LastWriteTime</c>（会被索引与 WAL 写入污染）。
    /// 这对应 INTERFACE_PLAN.md §6 的选项 3：宁可不展示，也不编造时间。
    /// 若后续迁移新增 <c>updated_at</c>，只改本接口实现，前端与 DTO 不变。
    /// </remarks>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>最后更新时间；不可得时为 <c>null</c>。</returns>
    /// <exception cref="Errors.CampusStoreUnavailableException">数据库不可用。</exception>
    Task<DateTimeOffset?> GetLastUpdatedAsync(CancellationToken cancellationToken);
}
