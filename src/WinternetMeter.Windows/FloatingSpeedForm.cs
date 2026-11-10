using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using WinternetMeter.Core;
using static WinternetMeter.Windows.NativeMethods;

namespace WinternetMeter.Windows;

/// <summary>
/// The floating speed widget.
///
/// It is a "layered" window drawn with UpdateLayeredWindow: we paint a 32-bit image
/// with real per-pixel transparency and hand it to Windows. Compared with the old
/// TransparencyKey approach this gives smooth text edges (no black halo), allows any
/// text color including black, and allows a semi-transparent rounded background.
///
/// Size is computed once per appearance change from the widest possible text, so
/// the widget never resizes or jitters while numbers change.
/// </summary>
internal sealed class FloatingSpeedForm : Form
{
    private const string UpArrow = "↑";
    private const string DownArrow = "↓";

    private readonly AppSettings _settings;
    private Font? _font;
    private string _up = SpeedFormatter.Format(0, SpeedUnit.Bytes);
    private string _down = SpeedFormatter.Format(0, SpeedUnit.Bytes);

    // Layout in pixels (computed by RebuildLayout)
    private float _padX, _padY, _gap, _arrowWidth, _valueWidth, _lineHeight, _radius;

    public FloatingSpeedForm(AppSettings settings)
    {
        _settings = settings;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Text = "Winternet Meter";
        AutoScaleMode = AutoScaleMode.None; // we scale ourselves from DeviceDpi
    }

    /// <summary>Raised when the user finishes dragging the widget.</summary>
    public event EventHandler? DragFinished;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
            return cp;
        }
    }

    public void SetSpeeds(string upload, string download)
    {
        if (upload == _up && download == _down) return;
        _up = upload;
        _down = download;
        Render();
    }

    /// <summary>Font, size, colors or units changed: re-measure and redraw.</summary>
    public void ApplyAppearance()
    {
        RebuildLayout();
        Render();
    }

    /// <summary>Re-pins the widget above everything (the taskbar likes to cover it after it's clicked).</summary>
    public void ReassertTopmost()
    {
        if (!IsHandleCreated || !Visible) return;
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RebuildLayout();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) Render();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyAppearance();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && !_settings.FixPosition)
        {
            // Hand the drag to Windows: smooth, and snaps across monitors correctly.
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WM_EXITSIZEMOVE)
            DragFinished?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Layered windows ignore WM_PAINT; drawing happens in Render().
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _font?.Dispose();
        base.Dispose(disposing);
    }

    private float Scale => DeviceDpi / 96f;

    private void RebuildLayout()
    {
        float scale = Scale;
        _font?.Dispose();
        _font = CreateWidgetFont(_settings, scale);

        using var bmp = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bmp);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

        var arrow = g.MeasureString(DownArrow, _font, PointF.Empty, format);
        var value = g.MeasureString(SpeedFormatter.WidestSample(_settings.Unit), _font, PointF.Empty, format);

        _padX = 8 * scale;
        _padY = 3 * scale;
        _gap = 5 * scale;
        _radius = 6 * scale;
        _arrowWidth = arrow.Width;
        _valueWidth = value.Width;
        _lineHeight = Math.Max(arrow.Height, value.Height);

        var size = new Size(
            (int)Math.Ceiling(_padX * 2 + _arrowWidth + _gap + _valueWidth),
            (int)Math.Ceiling(_padY * 2 + _lineHeight * 2));
        ApplySize(size);
    }

    /// <summary>
    /// Resizes while keeping the edge nearest the screen edge fixed: a widget parked
    /// next to the clock (bottom-right) grows up/left instead of off-screen.
    /// </summary>
    private void ApplySize(Size newSize)
    {
        var old = Bounds;
        if (old.Size == newSize) return;
        if (!Visible)
        {
            Size = newSize;
            return;
        }

        var screen = Screen.FromRectangle(old).Bounds;
        int x = old.X, y = old.Y;
        if (old.X + old.Width / 2 > screen.X + screen.Width / 2) x = old.Right - newSize.Width;
        if (old.Y + old.Height / 2 > screen.Y + screen.Height / 2) y = old.Bottom - newSize.Height;
        SetBounds(x, y, newSize.Width, newSize.Height);
    }

    internal static Font CreateWidgetFont(AppSettings settings, float scale)
    {
        float pixels = settings.FontSize * 96f / 72f * scale;
        foreach (var family in FontCandidates(settings.FontFamily))
        {
            try
            {
                var font = new Font(family, pixels, FontStyle.Bold, GraphicsUnit.Pixel);
                // GDI+ silently substitutes unknown families; keep looking if that happened.
                if (string.Equals(font.Name, family, StringComparison.OrdinalIgnoreCase))
                    return font;
                font.Dispose();
            }
            catch (ArgumentException)
            {
            }
        }
        return new Font(FontFamily.GenericMonospace, pixels, FontStyle.Bold, GraphicsUnit.Pixel);
    }

    private static IEnumerable<string> FontCandidates(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred)) yield return preferred;
        yield return "Cascadia Mono";
        yield return "Consolas";
        yield return "Segoe UI";
    }

    private void Render()
    {
        if (!IsHandleCreated || _font is null || Width <= 0 || Height <= 0) return;

        using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; // grayscale AA works on transparent pixels; ClearType doesn't

            bool pill = _settings.Background == BackgroundStyle.Pill;
            if (pill)
            {
                var bg = ToColor(_settings.BackgroundColor, (int)Math.Round(_settings.BackgroundOpacity * 255));
                using var brush = new SolidBrush(bg);
                using var path = RoundedRectangle(new RectangleF(0, 0, Width - 1, Height - 1), _radius);
                g.FillPath(brush, path);
            }
            else
            {
                // Alpha 1 is invisible but still catches the mouse; fully transparent
                // pixels of a layered window are click-through, which would make dragging fiddly.
                using var hit = new SolidBrush(Color.FromArgb(1, 0, 0, 0));
                g.FillRectangle(hit, 0, 0, Width, Height);
            }

            var textColor = ToColor(_settings.TextColor, 255);
            DrawLine(g, UpArrow, _up, 0, textColor, shadow: !pill);
            DrawLine(g, DownArrow, _down, 1, textColor, shadow: !pill);
        }

        PushToScreen(bmp);
    }

    private void DrawLine(Graphics g, string arrow, string value, int row, Color color, bool shadow)
    {
        float top = _padY + row * _lineHeight;
        var arrowRect = new RectangleF(_padX, top, _arrowWidth + 1, _lineHeight);
        var valueRect = new RectangleF(_padX + _arrowWidth + _gap, top, _valueWidth + 1, _lineHeight);

        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
        using var valueFormat = (StringFormat)format.Clone();
        valueFormat.Alignment = StringAlignment.Far; // right-align: the unit stays put, digits grow left

        string text = value.Trim();
        if (shadow)
        {
            // A soft dark outline keeps transparent text readable on light wallpapers/taskbars.
            using var shadowBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
            float o = Math.Max(1f, Scale);
            g.DrawString(arrow, _font!, shadowBrush, Offset(arrowRect, o), format);
            g.DrawString(text, _font!, shadowBrush, Offset(valueRect, o), valueFormat);
        }

        using var brush = new SolidBrush(color);
        g.DrawString(arrow, _font!, brush, arrowRect, format);
        g.DrawString(text, _font!, brush, valueRect, valueFormat);
    }

    private static RectangleF Offset(RectangleF r, float by) => new(r.X + by, r.Y + by, r.Width, r.Height);

    private void PushToScreen(Bitmap bmp)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;
        try
        {
            hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
            oldBitmap = SelectObject(memDc, hBitmap);

            var size = new SIZE(bmp.Width, bmp.Height);
            var source = new POINT(0, 0);
            var destination = new POINT(Left, Top);
            var blend = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AC_SRC_ALPHA,
            };
            UpdateLayeredWindow(Handle, screenDc, ref destination, ref size, memDc, ref source, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDc);
            if (hBitmap != IntPtr.Zero)
            {
                SelectObject(memDc, oldBitmap);
                DeleteObject(hBitmap);
            }
            DeleteDC(memDc);
        }
    }

    private static GraphicsPath RoundedRectangle(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static Color ToColor(string hex, int alpha) =>
        ColorHex.TryParse(hex, out var r, out var g, out var b)
            ? Color.FromArgb(Math.Clamp(alpha, 0, 255), r, g, b)
            : Color.FromArgb(Math.Clamp(alpha, 0, 255), Color.White);
}
