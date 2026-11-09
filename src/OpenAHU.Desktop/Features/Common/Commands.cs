using System.Windows.Input;

namespace OpenAHU.Desktop.Features.Common;

public sealed class ActionCommand(Action action, Func<bool>? enabled = null) : ICommand
{
    public bool CanExecute(object? parameter) => enabled?.Invoke() ?? true;
    public void Execute(object? parameter) { if (CanExecute(parameter)) action(); }
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

public sealed class AsyncCommand(Func<Task> action, Action<Exception> failure, Func<bool>? enabled = null) : ICommand
{
    private bool _running;
    public bool CanExecute(object? parameter) => !_running && (enabled?.Invoke() ?? true);
    public async void Execute(object? parameter) => await ExecuteAsync();
    public async Task ExecuteAsync()
    {
        if (!CanExecute(null)) return;
        _running = true;
        CommandManager.InvalidateRequerySuggested();
        try { await action(); }
        catch (Exception ex) { failure(ex); }
        finally { _running = false; CommandManager.InvalidateRequerySuggested(); }
    }
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
