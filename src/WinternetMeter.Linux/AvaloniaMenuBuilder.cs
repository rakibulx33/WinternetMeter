using Avalonia.Controls;
using Avalonia.Media;
using WinternetMeter.Core;

namespace WinternetMeter.Linux;

/// <summary>
/// Converts the shared <see cref="MenuNode"/> tree into the two Avalonia menu kinds:
/// a NativeMenu for the tray icon (exported over D-Bus) and a ContextMenu for right-clicking the widget.
/// </summary>
internal static class AvaloniaMenuBuilder
{
    public static NativeMenu ToNativeMenu(IReadOnlyList<MenuNode> nodes, Action afterClick)
    {
        var menu = new NativeMenu();
        foreach (var node in nodes)
        {
            if (node.IsSeparator)
            {
                menu.Items.Add(new NativeMenuItemSeparator());
                continue;
            }

            var item = new NativeMenuItem(node.Text)
            {
                IsEnabled = node.IsEnabled,
                ToggleType = node.IsChecked is null ? NativeMenuItemToggleType.None : NativeMenuItemToggleType.CheckBox,
                IsChecked = node.IsChecked == true,
            };
            if (node.Children is { Count: > 0 } children)
                item.Menu = ToNativeMenu(children, afterClick);
            if (node.OnClick is { } onClick)
                item.Click += (_, _) =>
                {
                    onClick();
                    afterClick();
                };
            menu.Items.Add(item);
        }
        return menu;
    }

    public static ContextMenu ToContextMenu(IReadOnlyList<MenuNode> nodes, Action afterClick)
    {
        var menu = new ContextMenu();
        foreach (var control in ToControls(nodes, afterClick))
            menu.Items.Add(control);
        return menu;
    }

    private static IEnumerable<Control> ToControls(IReadOnlyList<MenuNode> nodes, Action afterClick)
    {
        foreach (var node in nodes)
        {
            if (node.IsSeparator)
            {
                yield return new Separator();
                continue;
            }

            var item = new MenuItem
            {
                Header = node.Text,
                IsEnabled = node.IsEnabled,
                ToggleType = node.IsChecked is null ? MenuItemToggleType.None : MenuItemToggleType.CheckBox,
                IsChecked = node.IsChecked == true,
                FontWeight = node.IsBold ? FontWeight.Bold : FontWeight.Normal,
            };
            if (node.ToolTip is { } tip)
                ToolTip.SetTip(item, tip);
            if (node.Children is { Count: > 0 } children)
            {
                foreach (var child in ToControls(children, afterClick))
                    item.Items.Add(child);
            }
            if (node.OnClick is { } onClick)
                item.Click += (_, e) =>
                {
                    e.Handled = true;
                    onClick();
                    afterClick();
                };
            yield return item;
        }
    }
}
