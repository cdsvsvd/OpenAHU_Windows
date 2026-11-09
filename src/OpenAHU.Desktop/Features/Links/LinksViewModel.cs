using OpenAHU.Contracts.Dtos;
using OpenAHU.Desktop.Features.Common;
using OpenAHU.Desktop.Features.Services;

namespace OpenAHU.Desktop.Features.Links;

public sealed class LinksViewModel(IFeatureApi api) : PageViewModel(api)
{
    private LinkDto[] _items = [];
    public LinkDto[] Items { get => _items; private set => Set(ref _items, value); }
    // Read-only URL text intentionally has no navigation handler in this v3 offline release.
    public override Task RefreshAsync() => RunAsync(async ct =>
    {
        Items = [];
        var items = await Api.GetLinksAsync(ct);
        ct.ThrowIfCancellationRequested();
        Items = items.OrderBy(l => l.Category).ThenBy(l => l.Title).ToArray();
        if (items.Length == 0) Notice = "暂无常用链接。";
    });
}
