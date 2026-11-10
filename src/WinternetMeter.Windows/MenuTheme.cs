using Microsoft.Win32;

namespace WinternetMeter.Windows;

/// <summary>
/// WinForms menus are always light. This follows Windows' "app mode" setting
/// (Settings → Personalization → Colors) and draws a dark menu when it's dark.
/// </summary>
internal static class MenuTheme
{
    public static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static ToolStripRenderer CreateRenderer() =>
        IsDarkMode() ? new DarkRenderer() : new LightRenderer();

    private sealed class LightRenderer : ToolStripProfessionalRenderer
    {
        public LightRenderer()
            : base(new LightColors())
        {
            RoundedEdges = false;
        }
    }

    private sealed class LightColors : ProfessionalColorTable
    {
        private static readonly Color Back = Color.FromArgb(249, 249, 249);
        private static readonly Color Hover = Color.FromArgb(232, 232, 232);
        private static readonly Color Border = Color.FromArgb(220, 220, 220);

        public override Color ToolStripDropDownBackground => Back;
        public override Color ImageMarginGradientBegin => Back;
        public override Color ImageMarginGradientMiddle => Back;
        public override Color ImageMarginGradientEnd => Back;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Hover;
        public override Color MenuItemPressedGradientEnd => Hover;
        public override Color CheckBackground => Hover;
        public override Color CheckSelectedBackground => Hover;
        public override Color CheckPressedBackground => Hover;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Back;
    }

    private sealed class DarkRenderer : ToolStripProfessionalRenderer
    {
        private static readonly Color Text = Color.FromArgb(240, 240, 240);
        private static readonly Color DisabledText = Color.FromArgb(140, 140, 140);

        public DarkRenderer()
            : base(new DarkColors())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Text : DisabledText;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Text;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // The default check glyph is black; draw a light one.
            var r = e.ImageRectangle;
            using var back = new SolidBrush(Color.FromArgb(70, 70, 70));
            e.Graphics.FillRectangle(back, r);
            TextRenderer.DrawText(e.Graphics, "✓", e.Item.Font, r, Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        private static readonly Color Back = Color.FromArgb(43, 43, 43);
        private static readonly Color Hover = Color.FromArgb(61, 61, 61);
        private static readonly Color Border = Color.FromArgb(70, 70, 70);

        public override Color ToolStripDropDownBackground => Back;
        public override Color ImageMarginGradientBegin => Back;
        public override Color ImageMarginGradientMiddle => Back;
        public override Color ImageMarginGradientEnd => Back;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Hover;
        public override Color MenuItemPressedGradientEnd => Hover;
        public override Color CheckBackground => Hover;
        public override Color CheckSelectedBackground => Hover;
        public override Color CheckPressedBackground => Hover;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Back;
    }
}
