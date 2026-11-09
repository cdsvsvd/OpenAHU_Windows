using System.Net;
using System.Windows.Input;
using OpenAHU.Desktop.Features.Services;

namespace OpenAHU.Desktop.Features.Common;

public abstract class PageViewModel : ObservableObject, IDisposable
{
    private CancellationTokenSource? _operation;
    private bool _disposed;
    private bool _busy;
    private string _error = "";
    private string _notice = "";
    protected PageViewModel(IFeatureApi api)
    {
        Api = api;
        RefreshCommand = new(RefreshAsync, ShowError, () => IsIdle);
    }
    protected IFeatureApi Api { get; }
    public const string DemoDisclaimer = "本地演示数据，非学校官方信息";
    public bool IsBusy { get => _busy; private set { Set(ref _busy, value); Changed(nameof(IsIdle)); CommandManager.InvalidateRequerySuggested(); } }
    public bool IsIdle => !IsBusy;
    public string Error { get => _error; protected set => Set(ref _error, value); }
    public string Notice { get => _notice; protected set => Set(ref _notice, value); }
    public AsyncCommand RefreshCommand { get; }
    public abstract Task RefreshAsync();

    // Latest request wins, including servers that complete despite cancellation.
    protected async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (_disposed) return;
        _operation?.Cancel();
        using var operation = new CancellationTokenSource();
        _operation = operation;
        IsBusy = true;
        Error = "";
        Notice = "";
        try { await action(operation.Token); }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { }
        catch (Exception ex) { if (!operation.IsCancellationRequested) ShowError(ex); }
        finally
        {
            if (ReferenceEquals(_operation, operation)) { _operation = null; IsBusy = false; }
        }
    }

    protected void ShowError(Exception ex) => Error = ErrorText(ex);
    public static string ErrorText(Exception ex) => ex switch
    {
        FeatureApiException { Status: HttpStatusCode.Unauthorized } => "本地服务会话已失效，请重新连接本地服务。",
        FeatureApiException { Status: HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict } api =>
            $"{api.Error?.Message ?? "请求未完成，请刷新后重试。"}" +
            (string.IsNullOrEmpty(api.Error?.TraceId) ? "" : $"（追踪号：{api.Error.TraceId}）"),
        _ => "本地服务暂不可用，请确认服务已启动后重试。"
    };

    public void Dispose()
    {
        _disposed = true;
        _operation?.Cancel();
        GC.SuppressFinalize(this);
    }
}
