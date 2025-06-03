using System;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WinternetMeter;
using WinternetMeter.Properties;

namespace WinternetMeter
{
    public class TrayAppContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private System.Windows.Forms.Timer updateTimer;
        private NetworkMonitor? networkMonitor;
        private string selectedAdapter;
        private int refreshInterval = 800; // 0.8 seconds
        // TODO: Add fields for settings, etc.
        private FloatingSpeedForm? floatingForm;
        private Point floatingLocation = Point.Empty;
        private bool fixPositionChecked = false;
        private string fontFamily = Properties.Settings.Default.FontStyle ?? "Segoe UI";
        private const string StartupKey = @"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private string AppName => Application.ProductName;
        private string AppPath => Application.ExecutablePath;
        private bool autoSelectAdapter = true;
        private const string AppLink = "https://github.com/rakibulx33/WinternetMeter";

        public TrayAppContext()
        {
            // Load last settings
            selectedAdapter = Properties.Settings.Default.SelectedAdapter;
            floatingLocation = Properties.Settings.Default.FloatingLocation;
            fixPositionChecked = Properties.Settings.Default.FixPositionChecked;
            autoSelectAdapter = Properties.Settings.Default.AutoSelectAdapter;
            // If no adapter was selected before, pick the first one available
            if (string.IsNullOrEmpty(selectedAdapter))
                selectedAdapter = NetworkMonitor.GetAdapterNames().FirstOrDefault() ?? string.Empty;
            networkMonitor = new NetworkMonitor(selectedAdapter);
            try
            {
                // Use app.ico for both app and tray icon
                trayIcon = new NotifyIcon()
                {
                    Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico")),
                    Visible = true,
                    Text = "Initializing..."
                };
            }
            catch (Exception)
            {
                trayIcon = new NotifyIcon()
                {
                    Icon = SystemIcons.Application,
                    Visible = true,
                    Text = "Initializing..."
                };
            }
            trayIcon.ContextMenuStrip = BuildContextMenu();
            // No click, left click, or double click actions

            updateTimer = new System.Windows.Forms.Timer { Interval = refreshInterval };
            updateTimer.Tick += (s, e) => UpdateSpeed();
            updateTimer.Start();

            // Show floating speed form near the taskbar clock
            floatingForm = new FloatingSpeedForm();
            floatingForm.SetFontFamily(fontFamily);
            if (floatingLocation == Point.Empty)
                PositionFloatingForm();
            else
                floatingForm.Location = floatingLocation;
            floatingForm.AllowDrag = !fixPositionChecked; // Ensure drag state is set on startup
            floatingForm.Show();
        }

        private ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip();
            // Add app name at the top
            var appNameItem = new ToolStripMenuItem("⬆️ Winternet Meter ⬇️") { Font = new Font("Segoe UI", 12, FontStyle.Bold) };
            appNameItem.Click += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = AppLink, UseShellExecute = true });
            menu.Items.Add(appNameItem);
            menu.Items.Add(new ToolStripSeparator());
            // Adapter selection option (as dropdown with checkmarks, like Font Style)
            var adapterItem = new ToolStripMenuItem("Adapter");
            // Add 'Auto-Select' as the first option
            var autoSelectAdapterItem = new ToolStripMenuItem("Auto-Select") { CheckOnClick = true };
            autoSelectAdapterItem.Checked = autoSelectAdapter;
            autoSelectAdapterItem.Click += (s, e) => {
                if (!autoSelectAdapter) {
                    autoSelectAdapter = true;
                    Properties.Settings.Default.AutoSelectAdapter = true;
                    Properties.Settings.Default.Save();
                    selectedAdapter = GetBestAdapterName() ?? selectedAdapter;
                    networkMonitor = new NetworkMonitor(selectedAdapter);
                    // Uncheck all other adapters
                    foreach (ToolStripMenuItem mi in adapterItem.DropDownItems)
                        if (mi != autoSelectAdapterItem) mi.Checked = false;
                }
            };
            adapterItem.DropDownItems.Add(autoSelectAdapterItem);
            // List all adapters
            var adapters = NetworkMonitor.GetAdapterNames();
            foreach (var adapter in adapters)
            {
                var aItem = new ToolStripMenuItem(adapter) { CheckOnClick = true };
                aItem.Checked = (!autoSelectAdapter && adapter == selectedAdapter);
                aItem.Click += (s, e) =>
                {
                    if (autoSelectAdapter) {
                        autoSelectAdapter = false;
                        Properties.Settings.Default.AutoSelectAdapter = false;
                        Properties.Settings.Default.Save();
                        autoSelectAdapterItem.Checked = false;
                    }
                    selectedAdapter = adapter;
                    networkMonitor = new NetworkMonitor(selectedAdapter);
                    // Update checkmarks
                    foreach (ToolStripMenuItem mi in adapterItem.DropDownItems)
                        mi.Checked = (mi == aItem);
                };
                adapterItem.DropDownItems.Add(aItem);
            }
            menu.Items.Add(adapterItem);
            // Fix position option
            var fixItem = new ToolStripMenuItem("Fix Position") { CheckOnClick = true };
            fixItem.Checked = fixPositionChecked;
            if (floatingForm != null)
                floatingForm.AllowDrag = !fixPositionChecked;
            fixItem.CheckedChanged += (s, e) =>
            {
                fixPositionChecked = fixItem.Checked;
                if (floatingForm != null)
                    floatingForm.AllowDrag = !fixItem.Checked;
                Properties.Settings.Default.FixPositionChecked = fixPositionChecked;
                Properties.Settings.Default.Save();
            };
            menu.Items.Add(fixItem);
            // Run on Startup option
            var startupItem = new ToolStripMenuItem("Run on Startup") { CheckOnClick = true };
            startupItem.Checked = IsStartupEnabled();
            startupItem.CheckedChanged += (s, e) => SetStartup(startupItem.Checked);
            menu.Items.Add(startupItem);
            // Font style option
            var fontItem = new ToolStripMenuItem("Font Style");
            var fonts = new[] { "Segoe UI", "Arial", "Consolas", "Tahoma", "Calibri", "Times New Roman" };
            foreach (var font in fonts)
            {
                var fItem = new ToolStripMenuItem(font) { Checked = (font == fontFamily) };
                fItem.Click += (s, e) => SetFontFamily(font);
                fontItem.DropDownItems.Add(fItem);
            }
            // Add 'More Fonts...' option to show all installed fonts
            var moreFontsItem = new ToolStripMenuItem("More Fonts...");
            moreFontsItem.Click += (s, e) =>
            {
                var installedFonts = System.Drawing.FontFamily.Families.Select(f => f.Name).OrderBy(n => n).ToArray();
                var fontListForm = new Form { Text = "Installed Fonts", Width = 320, Height = 400, StartPosition = FormStartPosition.CenterScreen };
                var listBox = new ListBox { Dock = DockStyle.Fill, DataSource = installedFonts };
                listBox.DoubleClick += (ls, le) =>
                {
                    if (listBox.SelectedItem is string selectedFont)
                    {
                        SetFontFamily(selectedFont);
                        fontListForm.Close();
                    }
                };
                fontListForm.Controls.Add(listBox);
                fontListForm.ShowDialog();
            };
            fontItem.DropDownItems.Add(new ToolStripSeparator());
            fontItem.DropDownItems.Add(moreFontsItem);
            menu.Items.Add(fontItem);
            // Font size option
            var fontSizeItem = new ToolStripMenuItem("Font Size");
            var fontSizeBox = new ToolStripTextBox { Text = Properties.Settings.Default.TextSize.ToString(), Width = 40 };
            fontSizeBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (int.TryParse(fontSizeBox.Text, out int newSize) && newSize > 6 && newSize < 100)
                    {
                        Properties.Settings.Default.TextSize = newSize;
                        Properties.Settings.Default.Save();
                        floatingForm?.SetFontSize(newSize);
                    }
                }
            };
            fontSizeItem.DropDownItems.Add(new ToolStripLabel("Enter size (7-99):"));
            fontSizeItem.DropDownItems.Add(fontSizeBox);
            menu.Items.Add(fontSizeItem);
            // Text color option
            var textColorItem = new ToolStripMenuItem("Text Color");
            var presetColors = new (string Name, string Hex)[]
            {
                ("Lime", "#00FF00"),
                ("White", "#FFFFFF"),
                ("Red", "#FF0000"),
                ("Yellow", "#FFFF00"),
                ("Cyan", "#00FFFF"),
                ("Magenta", "#FF00FF"),
                ("Orange", "#FFA500"),
                ("Blue", "#0078D7"),
                ("Custom...", "custom")
            };
            Color currentColor = Properties.Settings.Default.TextColor;
            foreach (var (name, hex) in presetColors)
            {
                Color color = hex == "custom" ? Color.Empty : ColorTranslator.FromHtml(hex);
                var item = new ToolStripMenuItem(name)
                {
                    Checked = (hex != "custom" && currentColor.ToArgb() == color.ToArgb())
                };
                item.Paint += (s, e) =>
                {
                    // Always update check state to match current color
                    item.Checked = (hex != "custom" && (floatingForm?.ForeColor.ToArgb() ?? currentColor.ToArgb()) == color.ToArgb());
                };
                item.Click += (s, e) =>
                {
                    Color selectedColor;
                    if (hex == "custom")
                    {
                        string input = Microsoft.VisualBasic.Interaction.InputBox("Enter hex color code (e.g. #00FF00):", "Custom Text Color", "#00FF00");
                        if (!string.IsNullOrWhiteSpace(input))
                        {
                            try { selectedColor = ColorTranslator.FromHtml(input); }
                            catch { MessageBox.Show("Invalid hex code."); return; }
                        }
                        else return;
                    }
                    else
                    {
                        selectedColor = color;
                    }
                    if (floatingForm != null)
                    {
                        floatingForm.ForeColor = selectedColor;
                        foreach (Control c in floatingForm.Controls)
                            c.ForeColor = selectedColor;
                    }
                    Properties.Settings.Default.TextColor = selectedColor;
                    Properties.Settings.Default.Save();
                    // Update checkmarks
                    foreach (ToolStripMenuItem mi in textColorItem.DropDownItems)
                        mi.Checked = (mi == item);
                };
                textColorItem.DropDownItems.Add(item);
            }
            menu.Items.Add(textColorItem);
            menu.Items.Add("Exit", null, (s, e) => ExitThread());
            return menu;
        }

        private void ChangeAdapter(string adapterName)
        {
            selectedAdapter = adapterName;
            networkMonitor = new NetworkMonitor(selectedAdapter);
            // Save selected adapter
            Properties.Settings.Default.SelectedAdapter = selectedAdapter;
            Properties.Settings.Default.Save();
        }

        private void FixWindowPosition()
        {
            if (floatingForm == null) return;
            floatingForm.AllowDrag = false;
            MessageBox.Show("The floating window is now fixed in its current position.", "Fixed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void PositionFloatingForm()
        {
            var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0,0,800,600);
            if (floatingForm == null) return;
            floatingForm.Left = screen.Right - floatingForm.Width - 10;
            floatingForm.Top = screen.Bottom - floatingForm.Height - 10;
        }

        private void UpdateSpeed()
        {
            if (networkMonitor == null) return;
            var (dl, ul) = networkMonitor.GetSpeed();
            string dlStr, ulStr;
            // Download
            if (dl >= 1024 * 1024)
                dlStr = $"{dl / 1024 / 1024:F2} MB/s";
            else
                dlStr = $"{dl / 1024:F2} KB/s";
            // Upload
            if (ul >= 1024 * 1024)
                ulStr = $"{ul / 1024 / 1024:F2} MB/s";
            else
                ulStr = $"{ul / 1024:F2} KB/s";
            trayIcon.Text = $"D: {dlStr} | U: {ulStr}";
            floatingForm?.SetSpeed(dlStr, ulStr);
        }

        private void SetFontFamily(string font)
        {
            fontFamily = font;
            Properties.Settings.Default.FontStyle = font;
            Properties.Settings.Default.Save();
            floatingForm?.SetFontFamily(fontFamily);

            // Update checkmarks in the font menu
            if (trayIcon.ContextMenuStrip != null)
            {
                foreach (ToolStripItem item in trayIcon.ContextMenuStrip.Items)
                {
                    if (item is ToolStripMenuItem fontItem && fontItem.Text == "Font Style")
                    {
                        foreach (ToolStripItem fItem in fontItem.DropDownItems)
                        {
                            if (fItem is ToolStripMenuItem fontMenuItem)
                                fontMenuItem.Checked = (fontMenuItem.Text == font);
                        }
                    }
                }
            }
        }

        private bool IsStartupEnabled()
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupKey, false))
            {
                return key?.GetValue(AppName) as string == AppPath;
            }
        }

        private void SetStartup(bool enable)
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupKey, true))
            {
                if (enable)
                    key?.SetValue(AppName, AppPath);
                else
                    key?.DeleteValue(AppName, false);
            }
        }

        private string? GetBestAdapterName()
        {
            var best = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                    && nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback
                    && nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel)
                .OrderByDescending(nic => nic.Speed)
                .FirstOrDefault();
            return best?.Name;
        }

        protected override void ExitThreadCore()
        {
            // Save floating window position and fix state
            if (floatingForm != null)
                Properties.Settings.Default.FloatingLocation = floatingForm.Location;
            Properties.Settings.Default.FixPositionChecked = fixPositionChecked;
            Properties.Settings.Default.Save();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            if (floatingForm != null)
            {
                floatingForm.Close();
                floatingForm.Dispose();
            }
            base.ExitThreadCore();
        }
    }
}
