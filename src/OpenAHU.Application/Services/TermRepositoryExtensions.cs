using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Errors;
using OpenAHU.Application.Models;

namespace OpenAHU.Application.Services;

/// <summary>
/// 读路径的学期校验：学期必须存在，否则视为 404。
/// 三个按学期过滤的查询用例共用，避免各自写出不一致的判定顺序。
/// </summary>
internal static class TermRepositoryExtensions
{
    /// <summary>读取学期；不存在或为空则抛 <see cref="CampusResourceNotFoundException"/>。</summary>
    /// <param name="terms">学期仓储。</param>
    /// <param name="termId">学期主键，可为 <c>null</c>（来自查询串）。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>学期。</returns>
    public static async Task<Term> RequireAsync(
        this ITermRepository terms,
        string? termId,
        CancellationToken cancellationToken)
    {
        // 空白 termId 由 Api 先行拦截为 400；这里兜底成 404，避免漏到 500。
        if (string.IsNullOrWhiteSpace(termId))
        {
            throw new CampusResourceNotFoundException("term", termId ?? string.Empty);
        }

        var term = await terms.FindAsync(termId, cancellationToken);
        if (term is null)
        {
            throw new CampusResourceNotFoundException("term", termId);
        }

        return term;
    }
}
