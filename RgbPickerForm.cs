using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace RazerLightingSwitch;

internal sealed class RgbPickerForm : Form
{
    private readonly ColorWheel _wheel;
    private readonly TrackBar _brightness;
    private readonly Label _value;
    private readonly Label _brightnessLabel;
    private readonly Panel _preview;
    private readonly System.Windows.Forms.Timer _commitTimer;
    private Color _selectedColor;
    private bool _keepOpen;

    internal event EventHandler<ColorCommittedEventArgs>? ColorCommitted;

    internal RgbPickerForm(AppSettings settings)
    {
        Text = "Razer Lighting";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(258, 318);
        Padding = new Padding(14);
        Font = new Font("Segoe UI", 9f);

        _selectedColor = settings.BaseColor;
        _wheel = new ColorWheel { Size = new Size(214, 214), Location = new Point(22, 14), TabStop = true };
        _wheel.SelectedColorChanged += (_, color) => { _selectedColor = color; UpdatePreview(); QueueCommit(); };

        _brightnessLabel = new Label { Text = "Brightness", AutoSize = true, Location = new Point(14, 240) };
        _brightness = new TrackBar
        {
            Minimum = 1,
            Maximum = 100,
            TickFrequency = 10,
            Value = Math.Clamp(settings.Brightness, 1, 100),
            Location = new Point(84, 232),
            Size = new Size(130, 35),
            AccessibleName = "Brightness"
        };
        _brightness.ValueChanged += (_, _) => { UpdatePreview(); QueueCommit(); };
        _value = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Location = new Point(214, 240), Size = new Size(32, 20) };
        _preview = new Panel { Location = new Point(14, 280), Size = new Size(230, 22), AccessibleName = "Selected keyboard color" };

        Controls.AddRange([_wheel, _brightnessLabel, _brightness, _value, _preview]);
        _commitTimer = new System.Windows.Forms.Timer { Interval = 65 };
        _commitTimer.Tick += (_, _) =>
        {
            _commitTimer.Stop();
            ColorCommitted?.Invoke(this, new ColorCommittedEventArgs(_selectedColor, _brightness.Value));
        };
        Deactivate += (_, _) => { if (!_keepOpen) Hide(); };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Hide(); };
        ApplyTheme(SystemThemeIsLight());
        SetColor(settings.BaseColor, settings.Brightness);
    }

    internal void SetColor(Color color, int brightness)
    {
        _selectedColor = color;
        _brightness.Value = Math.Clamp(brightness, 1, 100);
        _wheel.SetColor(color);
        UpdatePreview();
    }

    internal void ShowNearTray(bool keepOpenForTest = false)
    {
        _keepOpen = keepOpenForTest;
        var work = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(work.Right - Width - 10, work.Bottom - Height - 10);
        Show();
        Activate();
        _wheel.Focus();
    }

    internal void ApplyTheme(bool light)
    {
        var background = light ? Color.FromArgb(248, 249, 251) : Color.FromArgb(20, 24, 31);
        var foreground = light ? Color.FromArgb(22, 32, 49) : Color.FromArgb(239, 242, 246);
        BackColor = background;
        ForeColor = foreground;
        _brightnessLabel.ForeColor = foreground;
        _value.ForeColor = foreground;
        _wheel.BackColor = background;
        var dark = light ? 0 : 1;
        if (IsHandleCreated) DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        Invalidate(true);
    }

    private void QueueCommit()
    {
        _commitTimer.Stop();
        _commitTimer.Start();
    }

    private void UpdatePreview()
    {
        var factor = _brightness.Value / 100d;
        _preview.BackColor = Color.FromArgb(
            (int)Math.Round(_selectedColor.R * factor),
            (int)Math.Round(_selectedColor.G * factor),
            (int)Math.Round(_selectedColor.B * factor));
        _value.Text = $"{_brightness.Value}%";
    }

    private static bool SystemThemeIsLight()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return (key?.GetValue("AppsUseLightTheme") as int? ?? 1) != 0;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

internal sealed record ColorCommittedEventArgs(Color Color, int Brightness);

internal sealed class ColorWheel : Control
{
    private Bitmap? _bitmap;
    private double _hue;
    private double _saturation;

    internal event EventHandler<Color>? SelectedColorChanged;

    internal ColorWheel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        AccessibleName = "RGB color wheel";
    }

    internal void SetColor(Color color)
    {
        var hsv = ToHsv(color);
        _hue = hsv.H;
        _saturation = hsv.S;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        BuildBitmap();
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        BuildBitmap();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (_bitmap is not null) e.Graphics.DrawImageUnscaled(_bitmap, 0, 0);
        var radius = Math.Min(Width, Height) / 2d - 5;
        var angle = _hue * Math.PI / 180d;
        var x = Width / 2d + Math.Cos(angle) * radius * _saturation;
        var y = Height / 2d + Math.Sin(angle) * radius * _saturation;
        using var outer = new Pen(Color.Black, 4);
        using var inner = new Pen(Color.White, 2);
        e.Graphics.DrawEllipse(outer, (float)x - 6, (float)y - 6, 12, 12);
        e.Graphics.DrawEllipse(inner, (float)x - 6, (float)y - 6, 12, 12);
    }

    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Capture = true; SelectFromPoint(e.Location); }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Capture) SelectFromPoint(e.Location); }
    protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }

    private void SelectFromPoint(Point point)
    {
        var dx = point.X - Width / 2d;
        var dy = point.Y - Height / 2d;
        var radius = Math.Min(Width, Height) / 2d - 2;
        _saturation = Math.Clamp(Math.Sqrt(dx * dx + dy * dy) / radius, 0, 1);
        _hue = (Math.Atan2(dy, dx) * 180d / Math.PI + 360d) % 360d;
        Invalidate();
        SelectedColorChanged?.Invoke(this, FromHsv(_hue, _saturation, 1));
    }

    private void BuildBitmap()
    {
        _bitmap?.Dispose();
        if (Width < 2 || Height < 2) return;
        _bitmap = new Bitmap(Width, Height);
        var cx = Width / 2d;
        var cy = Height / 2d;
        var radius = Math.Min(Width, Height) / 2d - 2;
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var dx = x - cx;
            var dy = y - cy;
            var saturation = Math.Sqrt(dx * dx + dy * dy) / radius;
            if (saturation > 1) { _bitmap.SetPixel(x, y, BackColor); continue; }
            var hue = (Math.Atan2(dy, dx) * 180d / Math.PI + 360d) % 360d;
            _bitmap.SetPixel(x, y, FromHsv(hue, saturation, 1));
        }
    }

    private static Color FromHsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60d) % 2 - 1));
        var m = v - c;
        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x)
        };
        return Color.FromArgb((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }

    private static (double H, double S) ToHsv(Color color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var h = delta == 0 ? 0 : max == r ? 60 * (((g - b) / delta) % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        if (h < 0) h += 360;
        return (h, max == 0 ? 0 : delta / max);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _bitmap?.Dispose();
        base.Dispose(disposing);
    }
}
