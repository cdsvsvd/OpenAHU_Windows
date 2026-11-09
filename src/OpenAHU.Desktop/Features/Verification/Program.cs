using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenAHU.Desktop.Features.Services;

namespace OpenAHU.Desktop.Features.Verification;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var application = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var exitCode = 0;
        application.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                if (args.FirstOrDefault() == "--preview")
                {
                    var api = ReadSession(args[1]);
                    var pages = FeatureCatalog.Create(api);
                    var tabs = new TabControl();
                    foreach (var page in pages) tabs.Items.Add(new TabItem { Header = page.Title, Content = page.View, Tag = page });
                    tabs.SelectionChanged += async (_, e) =>
                    {
                        if (e.Source == tabs && tabs.SelectedItem is TabItem { Tag: FeaturePage page }) await page.ActivateAsync();
                    };
                    var window = new Window { Title = "OpenAHU 成员02业务页验收宿主", Width = 1280, Height = 900, MinWidth = 960, MinHeight = 650, Content = tabs };
                    window.Closed += (_, _) => { foreach (var page in pages) page.Dispose(); api.Dispose(); application.Shutdown(); };
                    window.Show();
                    return;
                }
                if (args.FirstOrDefault() == "--integration")
                    await IntegrationChecks.RunAsync(args[1], args[2], args[3]);
                else
                {
                    await BehaviorChecks.RunAsync();
                    if (args.FirstOrDefault() == "--render") await RenderAsync(new FakeApi(), args[1]);
                }
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); exitCode = 1; }
            application.Shutdown();
        });
        application.Run();
        return exitCode;
    }

    internal static SessionApiClient ReadSession(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var uri = new Uri(root.GetProperty("baseUrl").GetString()!);
        if (uri.Port != root.GetProperty("port").GetInt32()) throw new InvalidDataException("握手端口不一致。");
        return new(uri, root.GetProperty("token").GetString()!);
    }

    internal static async Task RenderAsync(IFeatureApi api, string output)
    {
        Directory.CreateDirectory(output);
        var bindings = new BindingTrace();
        PresentationTraceSources.DataBindingSource.Listeners.Add(bindings);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        var pages = FeatureCatalog.Create(api);
        try
        {
            foreach (var page in pages)
            {
                await page.ActivateAsync();
                if (page.ViewModel.Error.Length != 0) throw new Exception(page.ViewModel.Error);
                await RenderViewAsync(page.View, Path.Combine(output, page.Key + ".png"));
                if (page.ViewModel is Timetable.TimetableViewModel timetable)
                {
                    timetable.BeginNew();
                    await RenderViewAsync(page.View, Path.Combine(output, "course-editor.png"));
                }
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            }
            if (bindings.Messages.Count != 0) throw new Exception("WPF binding errors: " + string.Join("\n", bindings.Messages));
            Console.WriteLine("PASS: six WPF views + course editor rendered; zero binding warnings/errors");
        }
        finally
        {
            foreach (var page in pages) page.Dispose();
            PresentationTraceSources.DataBindingSource.Listeners.Remove(bindings);
        }
    }

    private static async Task RenderViewAsync(FrameworkElement view, string path)
    {
        // A presentation source and idle layout pass are needed for DataGrid column virtualization.
        var host = new Window { Content = view, Width = 1200, Height = 820, Left = -20000, Top = -20000,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        try
        {
            host.Show();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            if (view.DataContext is Common.PageViewModel page)
            {
                // Exercise refresh after controls are actually bound, including ComboBox ItemsSource replacement.
                await page.RefreshAsync();
                if (page.Error.Length != 0) throw new Exception(page.Error);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            }
            view.UpdateLayout();
            var image = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            image.Render(view);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
        finally { host.Content = null; host.Close(); }
    }

    private sealed class BindingTrace : TraceListener
    {
        public List<string> Messages { get; } = [];
        public override void Write(string? message) { if (!string.IsNullOrEmpty(message)) Messages.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
}
