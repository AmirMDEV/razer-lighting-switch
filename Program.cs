using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Http.Json;
using System.Text.Json;

namespace RazerLightingSwitch;

internal static class Program
{
    private const string MutexName = "Local\\Amir.RazerLightingSwitch.Host";
    private const string PipeName = "Amir.RazerLightingSwitch.Commands";
    private static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Amir", "RazerLightingSwitch");
    private static readonly string LogPath = Path.Combine(DataDirectory, "controller.log");

    [STAThread]
    private static async Task Main(string[] args)
    {
        Directory.CreateDirectory(DataDirectory);
        var command = ParseCommand(args);
        if (command is null)
        {
            Log("Ignored launch without black or white command");
            return;
        }

        using var mutex = new Mutex(true, MutexName, out var ownsMutex);
        if (!ownsMutex)
        {
            if (!await SendToHostAsync(command))
            {
                Log($"Host pipe unavailable for command {command}");
            }
            return;
        }

        using var lifetime = new CancellationTokenSource();
        await using var chroma = new ChromaClient(Log);
        if (!await chroma.ConnectAsync(lifetime.Token))
        {
            return;
        }

        if (!await chroma.ApplyAsync(command, lifetime.Token))
        {
            return;
        }

        Log($"Host started with {command}");
        var heartbeat = chroma.RunHeartbeatAsync(lifetime.Token);
        var pipe = RunPipeServerAsync(chroma, lifetime.Token);
        await Task.WhenAny(heartbeat, pipe);
        lifetime.Cancel();
        await Task.WhenAll(IgnoreCancellation(heartbeat), IgnoreCancellation(pipe));
    }

    private static string? ParseCommand(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.Equals("black", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--black", StringComparison.OrdinalIgnoreCase)) return "black";
            if (arg.Equals("white", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--white", StringComparison.OrdinalIgnoreCase)) return "white";
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
            Log($"Sent {command} to host");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Pipe send failed: {ex.Message}");
            return false;
        }
    }

    private static async Task RunPipeServerAsync(ChromaClient chroma, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.WaitForConnectionAsync(token);
            using var reader = new StreamReader(pipe);
            var command = await reader.ReadLineAsync(token);
            if (command is "black" or "white")
            {
                await chroma.ApplyAsync(command, token);
            }
        }
    }

    private static async Task IgnoreCancellation(Task task)
    {
        try { await task; } catch (OperationCanceledException) { }
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch { }
    }
}

internal sealed class ChromaClient : IAsyncDisposable
{
    private static readonly Uri RegistrationUri = new("http://localhost:54235/razer/chromasdk");
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly Action<string> _log;
    private Uri? _sessionUri;

    public ChromaClient(Action<string> log) => _log = log;

    public async Task<bool> ConnectAsync(CancellationToken token)
    {
        try
        {
            var app = new
            {
                title = "Amir Razer Lighting Switch",
                description = "Instant black and white keyboard lighting switch",
                author = new { name = "Amir Mansaray", contact = "followamir.com" },
                device_supported = new[] { "keyboard" },
                category = "application"
            };
            using var response = await _http.PostAsJsonAsync(RegistrationUri, app, token);
            response.EnsureSuccessStatusCode();
            var registration = await response.Content.ReadFromJsonAsync<Registration>(cancellationToken: token);
            if (registration?.Uri is null)
            {
                _log("Chroma registration returned no session URI");
                return false;
            }
            _sessionUri = new Uri(registration.Uri.TrimEnd('/'));
            _log($"Connected to Chroma session {registration.SessionId}");
            return true;
        }
        catch (Exception ex)
        {
            _log($"Chroma registration failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ApplyAsync(string command, CancellationToken token)
    {
        if (_sessionUri is null) return false;
        object effect = command == "black"
            ? new { effect = "CHROMA_NONE" }
            : new { effect = "CHROMA_STATIC", param = new { color = 0xFFFFFF } };
        try
        {
            using var response = await _http.PutAsJsonAsync(new Uri(_sessionUri + "/keyboard"), effect, token);
            var body = await response.Content.ReadAsStringAsync(token);
            response.EnsureSuccessStatusCode();
            var result = JsonSerializer.Deserialize<EffectResult>(body, JsonOptions);
            if (result?.Result != 0)
            {
                _log($"Apply {command} failed with Chroma result {result?.Result}: {body}");
                return false;
            }
            _log($"Applied {command}");
            return true;
        }
        catch (Exception ex)
        {
            _log($"Apply {command} failed: {ex.Message}");
            return false;
        }
    }

    public async Task RunHeartbeatAsync(CancellationToken token)
    {
        if (_sessionUri is null) return;
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            using var response = await _http.PutAsJsonAsync(new Uri(_sessionUri + "/heartbeat"), new { }, token);
            if (!response.IsSuccessStatusCode)
            {
                _log($"Heartbeat failed: {(int)response.StatusCode}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_sessionUri is not null)
        {
            try { await _http.DeleteAsync(_sessionUri); } catch { }
        }
        _http.Dispose();
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private sealed record Registration(int SessionId, string Uri);
    private sealed record EffectResult(int Result);
}
