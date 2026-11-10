using OpenAHU.Contracts.Dtos;
using OpenAHU.Desktop.Features.Services;

namespace OpenAHU.Desktop.Features.Common;

public abstract class TermPageViewModel(IFeatureApi api) : PageViewModel(api)
{
    private TermDto[] _terms = [];
    private TermDto? _term;
    private bool _updatingTerms;
    public TermDto[] Terms { get => _terms; private set => Set(ref _terms, value); }
    public TermDto? SelectedTerm
    {
        get => _term;
        set
        {
            // Replacing ItemsSource can temporarily clear ComboBox.SelectedItem.
            if (_updatingTerms) return;
            if (!Set(ref _term, value)) return;
            TermChanged();
            // Switching the selected term only reloads that term's rows.
            // The term list is fetched again only by the explicit Refresh command.
            _ = RunAsync(LoadCurrentTermAsync);
        }
    }
    protected virtual void TermChanged() { }
    protected abstract void ClearRows();
    protected abstract Task LoadTermAsync(TermDto term, CancellationToken ct);

    public override Task RefreshAsync() => RunAsync(async ct =>
    {
        var selectedId = _term?.Id;
        var terms = await Api.GetTermsAsync(ct);
        ct.ThrowIfCancellationRequested();
        var chosen = terms.FirstOrDefault(t => t.Id == selectedId)
            ?? terms.FirstOrDefault(t => t.IsCurrent) ?? terms.FirstOrDefault();
        var changed = _term?.Id != chosen?.Id;
        _updatingTerms = true;
        try
        {
            Terms = terms;
            _term = chosen;
            Changed(nameof(SelectedTerm));
        }
        finally { _updatingTerms = false; }
        if (changed) TermChanged();
        await LoadCurrentTermAsync(ct);
    });

    private async Task LoadCurrentTermAsync(CancellationToken ct)
    {
        ClearRows();
        var term = _term;
        if (term is null) { Notice = "暂无学期数据。"; return; }
        await LoadTermAsync(term, ct);
    }
}
