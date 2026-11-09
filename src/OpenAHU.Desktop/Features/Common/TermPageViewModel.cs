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
            _ = RefreshAsync(); // RunAsync observes failures and cancels superseded requests.
        }
    }
    protected virtual void TermChanged() { }
    protected abstract void ClearRows();
    protected abstract Task LoadTermAsync(TermDto term, CancellationToken ct);

    public override Task RefreshAsync()
    {
        var selectedId = SelectedTerm?.Id;
        return RunAsync(async ct =>
        {
            ClearRows();
            var terms = await Api.GetTermsAsync(ct);
            ct.ThrowIfCancellationRequested();
            var chosen = terms.FirstOrDefault(t => t.Id == selectedId) ?? terms.FirstOrDefault(t => t.IsCurrent) ?? terms.FirstOrDefault();
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
            if (chosen is null) { Notice = "暂无学期数据。"; return; }
            await LoadTermAsync(chosen, ct);
        });
    }
}
