namespace WinternetMeter.Core;

/// <summary>"Run on startup". Windows: HKCU Run key. Linux: ~/.config/autostart/*.desktop.</summary>
public interface IAutostart
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);

    /// <summary>If enabled but pointing at an old location (exe moved/updated), point it here.</summary>
    void Repair();
}
