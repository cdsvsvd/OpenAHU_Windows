using OpenAHU.Application.Abstractions;
using OpenAHU.Application.Abstractions.Queries;
using OpenAHU.Application.Models;

namespace OpenAHU.Application.Services;

/// <summary>学期列表查询。<see cref="ITermQuery"/> 的唯一实现。</summary>
public sealed class TermQuery : ITermQuery
{
    private readonly ITermRepository _terms;

    /// <summary>创建查询服务。</summary>
    /// <param name="terms">学期仓储。</param>
    public TermQuery(ITermRepository terms)
    {
        _terms = terms;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Term>> GetAllAsync(CancellationToken cancellationToken)
    {
        var terms = await _terms.GetAllAsync(cancellationToken);

        // 最近的学期排最前，前端下拉框默认选中第一项即“当前学期附近”。
        return terms
            .OrderByDescending(term => term.StartDate)
            .ThenBy(term => term.Name, StringComparer.Ordinal)
            .ToArray();
    }
}
