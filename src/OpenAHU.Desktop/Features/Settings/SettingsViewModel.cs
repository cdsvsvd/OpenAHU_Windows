using OpenAHU.Contracts.Dtos;
using OpenAHU.Desktop.Features.Common;
using OpenAHU.Desktop.Features.Services;

namespace OpenAHU.Desktop.Features.Settings;

public sealed class SettingsViewModel(IFeatureApi api) : PageViewModel(api)
{
    private HealthDto? _health;
    private DataStatusDto? _status;
    public HealthDto? Health { get => _health; private set { Set(ref _health, value); Changed(nameof(HealthText)); } }
    public DataStatusDto? Status { get => _status; private set => Set(ref _status, value); }
    public string HealthText => Health is null ? "尚未检查" : Health.DatabaseReady ? "本地服务与数据库已就绪" : "本地服务暂不可用：数据库未就绪";
    public override Task RefreshAsync() => RunAsync(async ct =>
    {
        Health = null;
        Status = null;
        var healthTask = Api.GetHealthAsync(ct);
        var statusTask = Api.GetDataStatusAsync(ct);
        await Task.WhenAll(healthTask, statusTask);
        ct.ThrowIfCancellationRequested();
        Health = await healthTask;
        Status = await statusTask;
    });
}
