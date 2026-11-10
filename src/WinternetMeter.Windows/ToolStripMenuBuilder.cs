using WinternetMeter.Core;

namespace WinternetMeter.Windows;

/// <summary>Converts the shared <see cref="MenuNode"/> tree into WinForms menu items.</summary>
internal static class ToolStripMenuBuilder
{
    public static ToolStripItem[] Build(IReadOnlyList<MenuNode> nodes) => nodes.Select(Convert).ToArray();

    private static ToolStripItem Convert(MenuNode node)
    {
        if (node.IsSeparator)
            return new ToolStripSeparator();

        var item = new ToolStripMenuItem(node.Text)
        {
            Enabled = node.IsEnabled,
            Checked = node.IsChecked == true,
            ToolTipText = node.ToolTip,
        };
        if (node.IsBold)
            item.Font = new Font(item.Font, FontStyle.Bold);
        if (node.OnClick is { } onClick)
            item.Click += (_, _) => onClick();
        if (node.Children is { Count: > 0 } children)
            item.DropDownItems.AddRange(Build(children));
        return item;
    }
}
