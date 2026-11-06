namespace OpenAHU.Application.Errors;

/// <summary>
/// 请求的资源在本地数据库中不存在（未知学期、未知课程）。
/// Api 层映射为 <c>404</c> + <c>not_found</c>。参见 INTERFACE_PLAN.md §5。
/// </summary>
/// <remarks>
/// 这是<b>读路径</b>的异常：查询签名只能返回列表或单值，无法优雅表达“学期不存在”，
/// 且该情况在正常使用中频率很低。写路径的同类情况改用 <c>CourseWriteResult.NotFound</c>。
/// </remarks>
public sealed class CampusResourceNotFoundException : Exception
{
    /// <summary>创建异常。</summary>
    /// <param name="resource">资源种类，例如 <c>term</c>、<c>course</c>。</param>
    /// <param name="key">被查询的标识。</param>
    public CampusResourceNotFoundException(string resource, string key)
        : base($"资源不存在：{resource} / {key}")
    {
        Resource = resource;
        Key = key;
    }

    /// <summary>资源种类，供 Api 生成安全文案时分支。</summary>
    public string Resource { get; }

    /// <summary>被查询的标识。**不得**原样返回给前端以外的日志以外的位置时要注意脱敏。</summary>
    public string Key { get; }
}
