using System.Net.Http.Json;
using System.Text.Json;

namespace RazerLightingSwitch;

internal sealed class ChromaClient : IDisposable
{
    private static readonly Uri RegistrationUri = new("http://localhost:54235/razer/chromasdk");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly Action<string> _log;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private Uri? _sessionUri;

    internal ChromaClient(Action<string> log) => _log = log;

    internal async Task<bool> ConnectAsync(CancellationToken token)
    {
        try
        {
            var app = new
            {
                title = "Amir Razer Lighting Switch",
                description = "Instant keyboard lighting tray utility",
                author = new { name = "Amir Mansaray", contact = "followamir.com" },
                device_supported = new[] { "keyboard" },
                category = "application"
            };
            using var response = await _http.PostAsJsonAsync(RegistrationUri, app, token);
            response.EnsureSuccessStatusCode();
            var registration = await response.Content.ReadFromJsonAsync<Registration>(cancellationToken: token);
            if (registration?.Uri is null) return false;
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

    internal Task<bool> ApplyCommandAsync(string command, AppSettings settings, CancellationToken token) =>
        command == "black"
            ? ApplyEffectAsync("black", new { effect = "CHROMA_NONE" }, token)
            : command == "white"
                ? ApplyColorAsync(Color.White, 100, "white", token)
                : ApplyColorAsync(settings.BaseColor, settings.Brightness, "rgb", token);

    internal Task<bool> ApplyColorAsync(Color color, int brightness, string label, CancellationToken token)
    {
        var factor = Math.Clamp(brightness, 1, 100) / 100d;
        var r = (int)Math.Round(color.R * factor);
        var g = (int)Math.Round(color.G * factor);
        var b = (int)Math.Round(color.B * factor);
        var bgr = r | (g << 8) | (b << 16);
        return ApplyEffectAsync(label, new { effect = "CHROMA_STATIC", param = new { color = bgr } }, token);
    }

    private async Task<bool> ApplyEffectAsync(string label, object effect, CancellationToken token)
    {
        if (_sessionUri is null) return false;
        await _writeLock.WaitAsync(token);
        try
        {
            using var response = await _http.PutAsJsonAsync(new Uri(_sessionUri + "/keyboard"), effect, token);
            var body = await response.Content.ReadAsStringAsync(token);
            response.EnsureSuccessStatusCode();
            var result = JsonSerializer.Deserialize<EffectResult>(body, JsonOptions);
            if (result?.Result != 0)
            {
                _log($"Apply {label} failed with Chroma result {result?.Result}: {body}");
                return false;
            }
            _log($"Applied {label}");
            return true;
        }
        catch (Exception ex)
        {
            _log($"Apply {label} failed: {ex.Message}");
            return false;
        }
        finally { _writeLock.Release(); }
    }

    internal async Task RunHeartbeatAsync(CancellationToken token)
    {
        if (_sessionUri is null) return;
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            using var response = await _http.PutAsJsonAsync(new Uri(_sessionUri + "/heartbeat"), new { }, token);
            if (!response.IsSuccessStatusCode) _log($"Heartbeat failed: {(int)response.StatusCode}");
        }
    }

    public void Dispose()
    {
        if (_sessionUri is not null)
        {
            try { _http.DeleteAsync(_sessionUri).GetAwaiter().GetResult(); } catch { }
        }
        _writeLock.Dispose();
        _http.Dispose();
    }

    private sealed record Registration(int SessionId, string Uri);
    private sealed record EffectResult(int Result);
}
