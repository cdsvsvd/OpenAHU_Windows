using OpenAHU.Contracts.Dtos;
using OpenAHU.Desktop.Features.Common;
using OpenAHU.Desktop.Features.Services;

namespace OpenAHU.Desktop.Features.Home;

public sealed class HomeViewModel(IFeatureApi api) : PageViewModel(api)
{
    private StudentProfileDto? _profile;
    private DataStatusDto? _status;
    public StudentProfileDto? Profile { get => _profile; private set => Set(ref _profile, value); }
    public DataStatusDto? Status { get => _status; private set { Set(ref _status, value); Changed(nameof(UpdatedAtText)); } }
    public string UpdatedAtText => Status?.UpdatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "尚未提供数据更新时间";
    public override Task RefreshAsync() => RunAsync(async ct =>
    {
        Profile = null;
        Status = null;
        var profileTask = Api.GetProfileAsync(ct);
        var statusTask = Api.GetDataStatusAsync(ct);
        await Task.WhenAll(profileTask, statusTask);
        ct.ThrowIfCancellationRequested();
        Profile = await profileTask;
        Status = await statusTask;
    });
}
