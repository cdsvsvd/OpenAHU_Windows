using System.Collections.ObjectModel;
using System.Windows.Input;
using OpenAHU.Desktop.Features;
using OpenAHU.Desktop.Features.Common;
using OpenAHU.Desktop.Features.Services;

namespace OpenAHU.Desktop.Shell;

public sealed record NavigationItem(string Key, string Title, FeaturePage Page);

public sealed class ShellViewModel : ObservableObject
{
    private readonly BackendProcessHost _backend;
    private SessionApiClient? _api;
    private IReadOnlyList<FeaturePage> _pages = [];
    private FeaturePage? _currentPage;
    private bool _isConnecting;
    private string _serviceStatus = "正在连接";
    private string _serviceMessage = "正在启动本地服务…";
    private string _startupError = "正在启动本地服务，请稍候。";
    private string _dataSourceLabel = "数据来源：LocalSQLite";
    private string _databaseStatus = "数据库：未检查";

    public ShellViewModel(BackendProcessHost backend)
    {
        _backend = backend;
        NavigationItems = [];
        NavigateCommand = new DelegateCommand(parameter =>
        {
            if (parameter is NavigationItem item) _ = NavigateAsync(item.Page);
        }, _ => !_isConnecting);
        ReconnectCommand = new AsyncCommand(InitializeAsync, _ => { }, () => !IsConnecting);
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }
    public FeaturePage? CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (Set(ref _currentPage, value)) Changed(nameof(HasPage));
        }
    }
    public bool HasPage => CurrentPage is not null;
    public bool IsConnecting { get => _isConnecting; private set { Set(ref _isConnecting, value); Changed(nameof(HasPage)); CommandManager.InvalidateRequerySuggested(); } }
    public string ServiceStatus { get => _serviceStatus; private set => Set(ref _serviceStatus, value); }
    public string ServiceMessage { get => _serviceMessage; private set => Set(ref _serviceMessage, value); }
    public string StartupError { get => _startupError; private set => Set(ref _startupError, value); }
    public string DataSourceLabel { get => _dataSourceLabel; private set => Set(ref _dataSourceLabel, value); }
    public string DatabaseStatus { get => _databaseStatus; private set => Set(ref _databaseStatus, value); }
    public ICommand NavigateCommand { get; }
    public AsyncCommand ReconnectCommand { get; }

    public async Task InitializeAsync()
    {
        if (IsConnecting) return;
        IsConnecting = true;
        ServiceStatus = "正在连接";
        DatabaseStatus = "数据库：检查中";
        ServiceMessage = "正在启动本地服务并检查数据库…";
        StartupError = "正在启动本地服务，请稍候。";
        try
        {
            DisposePages();
            _api?.Dispose();
            _api = await _backend.StartAsync().ConfigureAwait(true);
            var healthTask = _api.GetHealthAsync(CancellationToken.None);
            var statusTask = _api.GetDataStatusAsync(CancellationToken.None);
            await Task.WhenAll(healthTask, statusTask).ConfigureAwait(true);
            var health = await healthTask;
            var status = await statusTask;
            var source = status.Provider switch
            {
                "LocalSqlServer" => "SQL Server",
                "LocalSQLite" => "SQLite",
                _ => status.Provider
            };
            if (!health.DatabaseReady)
                throw new InvalidOperationException($"{source}数据库尚未就绪，请检查本地数据库连接配置。");
            _pages = FeatureCatalog.Create(_api);
            NavigationItems.Clear();
            foreach (var page in _pages) NavigationItems.Add(new NavigationItem(page.Key, page.Title, page));
            CurrentPage = _pages.FirstOrDefault();
            ServiceStatus = "已连接";
            DatabaseStatus = $"数据库：{source} 已就绪";
            ServiceMessage = $"本地服务已就绪，当前使用{source}演示数据。";
            DataSourceLabel = $"数据来源：{status.Provider} · 演示数据";
            StartupError = "";
            if (CurrentPage is not null) await CurrentPage.ActivateAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            CurrentPage = null;
            NavigationItems.Clear();
            ServiceStatus = "未连接";
            DatabaseStatus = "数据库：不可用";
            ServiceMessage = "本地服务未就绪，请稍后重试。";
            StartupError = ex is InvalidOperationException ? ex.Message : PageViewModel.ErrorText(ex);
        }
        finally { IsConnecting = false; }
    }

    private async Task NavigateAsync(FeaturePage page)
    {
        if (IsConnecting || !_pages.Contains(page) || ReferenceEquals(CurrentPage, page)) return;
        CurrentPage = page;
        try
        {
            await page.ActivateAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // A page failing to load must not take down the entire shell.
            StartupError = PageViewModel.ErrorText(ex);
            ServiceMessage = "页面刷新失败，请点击页面中的刷新按钮重试。";
        }
    }

    public void SetStartupFailure(Exception ex)
    {
        ServiceStatus = "未连接";
        DatabaseStatus = "数据库：不可用";
        ServiceMessage = "本地服务未就绪，请稍后重试。";
        StartupError = PageViewModel.ErrorText(ex);
        IsConnecting = false;
    }

    public async Task ShutdownAsync()
    {
        DisposePages();
        _api?.Dispose();
        await _backend.DisposeAsync().ConfigureAwait(false);
    }

    private void DisposePages()
    {
        foreach (var page in _pages) page.Dispose();
        _pages = [];
        NavigationItems.Clear();
        CurrentPage = null;
    }

    private sealed class DelegateCommand(Action<object?> execute, Predicate<object?> canExecute) : ICommand
    {
        public bool CanExecute(object? parameter) => canExecute(parameter);
        public void Execute(object? parameter) => execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
