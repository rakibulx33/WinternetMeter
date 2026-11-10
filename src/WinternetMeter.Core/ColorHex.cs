using System.Globalization;

namespace WinternetMeter.Core;

/// <summary>Colors are stored as "#RRGGBB" strings so settings don't depend on a UI toolkit.</summary>
public static class ColorHex
{
    public static bool TryParse(string? text, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim().TrimStart('#');

        if (s.Length == 3) // #RGB → #RRGGBB
            s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        else if (s.Length == 8) // #AARRGGBB → ignore alpha
            s = s[2..];

        if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            return false;

        r = (byte)(rgb >> 16);
        g = (byte)(rgb >> 8);
        b = (byte)rgb;
        return true;
    }

    public static string ToHex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";

    /// <summary>Returns the normalized "#RRGGBB" form, or <paramref name="fallback"/> if invalid.</summary>
    public static string Normalize(string? text, string fallback) =>
        TryParse(text, out var r, out var g, out var b) ? ToHex(r, g, b) : fallback;
}
