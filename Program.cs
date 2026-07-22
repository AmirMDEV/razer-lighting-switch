using System.IO.Pipes;

namespace RazerLightingSwitch;

internal static class Program
{
    internal const string MutexName = "Local\\Amir.RazerLightingSwitch.Host";
    internal const string PipeName = "Amir.RazerLightingSwitch.Commands";
    private static readonly TimeSpan StartupConnectionRetryWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StartupRetryDelay = TimeSpan.FromSeconds(5);

    [STAThread]
    private static void Main(string[] args)
    {
        AppPaths.EnsureCreated();
        var command = ParseCommand(args);

        using var mutex = new Mutex(true, MutexName, out var ownsMutex);
        if (!ownsMutex)
        {
            SendToHostAsync(command ?? "show").GetAwaiter().GetResult();
            return;
        }

        ApplicationConfiguration.Initialize();
        using var lifetime = new CancellationTokenSource();
        using var chroma = new ChromaClient(AppPaths.Log);
        if (!ConnectForLaunchAsync(chroma, command == "startup", lifetime.Token).GetAwaiter().GetResult()) return;

        var settings = AppSettings.Load();
        var initialCommand = command is "black" or "white" ? command : settings.LastMode;
        chroma.ApplyCommandAsync(initialCommand, settings, lifetime.Token).GetAwaiter().GetResult();

        using var context = new TrayAppContext(chroma, settings, lifetime);
        var heartbeat = chroma.RunHeartbeatAsync(lifetime.Token);
        var pipe = RunPipeServerAsync(context, lifetime.Token);

        if (command is null or "show") context.ShowPickerSoon();
        Application.Run(context);

        lifetime.Cancel();
        IgnoreCancellation(heartbeat).GetAwaiter().GetResult();
        IgnoreCancellation(pipe).GetAwaiter().GetResult();
    }

    private static async Task<bool> ConnectForLaunchAsync(ChromaClient chroma, bool isStartupLaunch, CancellationToken token)
    {
        var retryWindow = System.Diagnostics.Stopwatch.StartNew();
        for (var attempt = 1; ; attempt++)
        {
            if (await chroma.ConnectAsync(token)) return true;
            if (!isStartupLaunch) return false;
            if (retryWindow.Elapsed >= StartupConnectionRetryWindow)
            {
                AppPaths.Log($"Chroma did not become ready within {StartupConnectionRetryWindow.TotalMinutes:0} minutes after Windows startup");
                return false;
            }

            AppPaths.Log($"Chroma was not ready at startup; retrying in {StartupRetryDelay.TotalSeconds:0}s (attempt {attempt}; up to {StartupConnectionRetryWindow.TotalMinutes:0} minutes)");
            await Task.Delay(StartupRetryDelay, token);
        }
    }

    private static string? ParseCommand(string[] args)
    {
        foreach (var raw in args)
        {
            var arg = raw.TrimStart('-').ToLowerInvariant();
            if (arg is "black" or "white" or "show" or "startup" or "show-light" or "show-dark" or
                "startup-on" or "startup-off" or "exit" || arg.StartsWith("rgb:"))
                return arg;
        }
        return null;
    }

    private static async Task<bool> SendToHostAsync(string command)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await pipe.ConnectAsync(timeout.Token);
            await using var writer = new StreamWriter(pipe) { AutoFlush = true };
            await writer.WriteLineAsync(command);
            AppPaths.Log($"Sent {command} to host");
            return true;
        }
        catch (Exception ex)
        {
            AppPaths.Log($"Pipe send failed: {ex.Message}");
            return false;
        }
    }

    private static async Task RunPipeServerAsync(TrayAppContext context, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.WaitForConnectionAsync(token);
            using var reader = new StreamReader(pipe);
            var command = await reader.ReadLineAsync(token);
            if (!string.IsNullOrWhiteSpace(command)) context.HandleExternalCommand(command);
        }
    }

    private static async Task IgnoreCancellation(Task task)
    {
        try { await task; } catch (OperationCanceledException) { }
    }
}
