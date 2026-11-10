using WinternetMeter.Core;

namespace WinternetMeter.Windows;

internal static class Program
{
    internal const string MutexName = "WinternetMeter.SingleInstance";

    [STAThread]
    private static void Main()
    {
        // One meter only: a startup entry plus a manual launch must not give two widgets.
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
            return;

        ApplicationConfiguration.Initialize();

        var store = new JsonSettingsStore(JsonSettingsStore.DefaultPath());
        AppSettings settings;
        if (!store.Exists && LegacySettingsMigrator.TryMigrate(out var migrated))
        {
            settings = migrated.Normalize();
            store.Save(settings);
        }
        else
        {
            settings = store.Load();
        }

        Application.Run(new TrayAppContext(settings, store));
    }
}
