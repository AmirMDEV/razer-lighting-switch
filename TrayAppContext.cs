using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace RazerLightingSwitch;

internal sealed class TrayAppContext : ApplicationContext
{
    private const string StartupKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "AmirRazerLightingSwitch";
    private readonly ChromaClient _chroma;
    private readonly AppSettings _settings;
    private readonly CancellationTokenSource _lifetime;
    private readonly NotifyIcon _tray;
    private readonly RgbPickerForm _picker;
    private readonly HotkeyWindow _hotkeys;
    private readonly ToolStripMenuItem _startupItem;

    internal TrayAppContext(ChromaClient chroma, AppSettings settings, CancellationTokenSource lifetime)
    {
        _chroma = chroma;
        _settings = settings;
        _lifetime = lifetime;
        _picker = new RgbPickerForm(settings);
        _ = _picker.Handle;
        _picker.ColorCommitted += (_, e) => ApplyRgb(e.Color, e.Brightness);

        var menu = new ContextMenuStrip { ShowImageMargin = false };
        menu.Items.Add("Black    Ctrl+Alt+B", null, (_, _) => ApplyPreset("black"));
        menu.Items.Add("White    Ctrl+Alt+W", null, (_, _) => ApplyPreset("white"));
        menu.Items.Add("RGB wheel    Ctrl+Alt+L", null, (_, _) => ShowPicker());
        menu.Items.Add(new ToolStripSeparator());
        _startupItem = new ToolStripMenuItem("Start with Windows") { Checked = IsStartupEnabled(), CheckOnClick = true };
        _startupItem.CheckedChanged += (_, _) => SetStartup(_startupItem.Checked);
        menu.Items.Add(_startupItem);
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _tray = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Razer Lighting",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowPicker(); };

        _hotkeys = new HotkeyWindow(command =>
        {
            if (command == "show") ShowPicker(); else ApplyPreset(command);
        });
        _hotkeys.Register();
        AppPaths.Log($"Picker hwnd={_picker.Handle.ToInt64()}");
        AppPaths.Log("Tray ready with hotkeys Ctrl+Alt+B Ctrl+Alt+W Ctrl+Alt+L");
    }

    internal void ShowPickerSoon()
    {
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            Application.Idle -= handler;
            ShowPicker();
        };
        Application.Idle += handler;
    }

    internal void HandleExternalCommand(string command)
    {
        if (_picker.IsDisposed) return;
        _picker.BeginInvoke(new Action(() =>
        {
            switch (command)
            {
                case "black": ApplyPreset("black"); break;
                case "white": ApplyPreset("white"); break;
                case "show-light": ShowPicker(true, true); break;
                case "show-dark": ShowPicker(false, true); break;
                case "startup-on": _startupItem.Checked = true; break;
                case "startup-off": _startupItem.Checked = false; break;
                case "exit": ExitThread(); break;
                case var rgb when rgb.StartsWith("rgb:", StringComparison.OrdinalIgnoreCase): ApplyRgbCommand(rgb); break;
                default: ShowPicker(); break;
            }
        }));
    }

    private void ApplyRgbCommand(string command)
    {
        var parts = command.Split(':');
        if (parts.Length != 3 || parts[1].Length != 6 ||
            !int.TryParse(parts[2], out var brightness)) return;
        try
        {
            var color = ColorTranslator.FromHtml("#" + parts[1]);
            _picker.SetColor(color, Math.Clamp(brightness, 1, 100));
            ApplyRgb(color, Math.Clamp(brightness, 1, 100));
        }
        catch { }
    }

    private async void ApplyPreset(string command)
    {
        if (await _chroma.ApplyCommandAsync(command, _settings, _lifetime.Token))
        {
            _settings.LastMode = command;
            if (command == "white")
            {
                _settings.ColorArgb = Color.White.ToArgb();
                _settings.Brightness = 100;
                _picker.SetColor(Color.White, 100);
            }
            _settings.Save();
            _tray.Text = command == "black" ? "Razer Lighting - Black" : "Razer Lighting - White";
        }
    }

    private async void ApplyRgb(Color color, int brightness)
    {
        if (await _chroma.ApplyColorAsync(color, brightness, "rgb", _lifetime.Token))
        {
            _settings.LastMode = "rgb";
            _settings.ColorArgb = color.ToArgb();
            _settings.Brightness = brightness;
            _settings.Save();
            _tray.Text = $"Razer Lighting - #{color.R:X2}{color.G:X2}{color.B:X2} {brightness}%";
        }
    }

    private void ShowPicker(bool? lightOverride = null, bool keepOpenForTest = false)
    {
        _picker.ApplyTheme(lightOverride ?? SystemThemeIsLight());
        _picker.ShowNearTray(keepOpenForTest);
        AppPaths.Log("Picker shown");
    }

    private void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(StartupKeyPath);
        if (enabled)
            key.SetValue(StartupValueName, $"\"{Environment.ProcessPath}\" startup");
        else
            key.DeleteValue(StartupValueName, false);
        _settings.StartWithWindows = enabled;
        _settings.Save();
        AppPaths.Log($"Startup {(enabled ? "enabled" : "disabled")}");
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath);
        return key?.GetValue(StartupValueName) is string;
    }

    private static bool SystemThemeIsLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return (key?.GetValue("AppsUseLightTheme") as int? ?? 1) != 0;
    }

    private static Icon LoadTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "assets", "keyboard-white.ico");
        return File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
    }

    protected override void ExitThreadCore()
    {
        _hotkeys.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _picker.Dispose();
        _lifetime.Cancel();
        base.ExitThreadCore();
    }

    private sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int WmHotkey = 0x0312;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModNoRepeat = 0x4000;
        private readonly Action<string> _callback;

        internal HotkeyWindow(Action<string> callback)
        {
            _callback = callback;
            CreateHandle(new CreateParams { Caption = "RazerLightingHotkeys", Parent = new IntPtr(-3) });
        }

        internal void Register()
        {
            var black = RegisterHotKey(Handle, 1, ModControl | ModAlt | ModNoRepeat, (uint)Keys.B);
            var white = RegisterHotKey(Handle, 2, ModControl | ModAlt | ModNoRepeat, (uint)Keys.W);
            var rgb = RegisterHotKey(Handle, 3, ModControl | ModAlt | ModNoRepeat, (uint)Keys.L);
            AppPaths.Log($"Hotkey registration black={black} white={white} rgb={rgb} hwnd={Handle.ToInt64()}");
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey)
            {
                _callback(m.WParam.ToInt32() switch { 1 => "black", 2 => "white", _ => "show" });
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            UnregisterHotKey(Handle, 1);
            UnregisterHotKey(Handle, 2);
            UnregisterHotKey(Handle, 3);
            DestroyHandle();
        }

        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
