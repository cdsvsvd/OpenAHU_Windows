using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using OpenAHU.Contracts.Dtos;
using OpenAHU.Desktop.Features.Services;

namespace OpenAHU.Desktop.Shell;

/// <summary>Starts the local API companion, waits for its private handshake, and owns shutdown cleanup.</summary>
public sealed class BackendProcessHost : IAsyncDisposable
{
    private readonly string _handshakePath;
    private Process? _process;
    private bool _disposed;

    public BackendProcessHost(string? handshakePath = null)
    {
        _handshakePath = handshakePath ?? SessionHandshake.DefaultPath;
    }

    public async Task<SessionApiClient> StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var existing = await TryReuseExistingAsync(cancellationToken).ConfigureAwait(false);
        if (existing is not null) return existing;

        var startInfo = ResolveStartInfo();
        // Always agree on the handshake path, including alternate paths used by tests.
        startInfo.Environment["OpenAHU__SessionPath"] = _handshakePath;
        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动本地服务进程。");
        SessionApiClient? client = null;
        try
        {
            var handshake = await WaitForHandshakeAsync(_process, cancellationToken).ConfigureAwait(false);
            client = new SessionApiClient(new Uri(handshake.BaseUrl + "/"), handshake.Token);
            await WaitForHealthAsync(client, cancellationToken).ConfigureAwait(false);
            return client;
        }
        catch
        {
            client?.Dispose();
            await StopOwnedProcessAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<SessionApiClient?> TryReuseExistingAsync(CancellationToken ct)
    {
        if (!File.Exists(_handshakePath)) return null;
        try
        {
            var handshake = SessionHandshake.Read(_handshakePath);
            using var process = Process.GetProcessById(handshake.ProcessId);
            if (process.HasExited) return null;
            var client = new SessionApiClient(new Uri(handshake.BaseUrl + "/"), handshake.Token);
            try
            {
                await WaitForHealthAsync(client, ct).ConfigureAwait(false);
                return client;
            }
            catch { client.Dispose(); return null; }
        }
        catch (FileNotFoundException) { return null; }
        catch (JsonException) { return null; }
        catch (InvalidDataException) { return null; }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private async Task<SessionHandshake> WaitForHandshakeAsync(Process process, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (process.HasExited) throw new InvalidOperationException("本地服务启动后立即退出。");
            try
            {
                var handshake = SessionHandshake.Read(_handshakePath);
                // An old session.json can survive a crashed API. Never pair its token
                // with the newly launched process until the process ID matches.
                if (handshake.ProcessId == process.Id) return handshake;
            }
            catch (IOException) { }
            catch (JsonException) { }
            catch (InvalidDataException) { }
            catch (UnauthorizedAccessException) { }
            await Task.Delay(100, ct).ConfigureAwait(false);
        }
        throw new TimeoutException("等待本地服务握手超时。");
    }

    private static async Task WaitForHealthAsync(SessionApiClient client, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                HealthDto health = await client.GetHealthAsync(ct).ConfigureAwait(false);
                if (health.DatabaseReady || health.Status.Equals("degraded", StringComparison.OrdinalIgnoreCase)) return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FeatureApiException)
            {
                last = ex;
            }
            await Task.Delay(200, ct).ConfigureAwait(false);
        }
        throw new TimeoutException("本地服务健康检查超时。", last);
    }

    private static ProcessStartInfo ResolveStartInfo()
    {
        var configured = Environment.GetEnvironmentVariable("OPENAHU_API_PATH");
        var candidates = new[]
        {
            configured,
            Path.Combine(AppContext.BaseDirectory, "OpenAHU.Api.exe"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "OpenAHU.Api", "bin", "Release", "net10.0", "OpenAHU.Api.dll")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "OpenAHU.Api", "OpenAHU.Api.csproj"))
        }.Where(path => !string.IsNullOrWhiteSpace(path)).Select(path => Path.GetFullPath(path!)).ToArray();

        var executable = candidates.FirstOrDefault(path => File.Exists(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        if (executable is not null)
        {
            return new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(executable)! };
        }
        var dll = candidates.FirstOrDefault(path => File.Exists(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        if (dll is not null)
        {
            return new ProcessStartInfo("dotnet", $"\"{dll}\"") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(dll)! };
        }
        var project = candidates.FirstOrDefault(path => File.Exists(path) && path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        if (project is not null)
        {
            return new ProcessStartInfo("dotnet", $"run --project \"{project}\" --no-launch-profile") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(project)! };
        }
        throw new FileNotFoundException("找不到 OpenAHU.Api 本地服务。请设置 OPENAHU_API_PATH 指向后端 exe 或 dll。", configured);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopOwnedProcessAsync().ConfigureAwait(false);
    }

    private async Task StopOwnedProcessAsync()
    {
        var process = Interlocked.Exchange(ref _process, null);
        if (process is null) return;
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        finally { process.Dispose(); }
    }
}
