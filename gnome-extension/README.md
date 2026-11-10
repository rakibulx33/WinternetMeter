# Winternet Meter — GNOME top-bar extension

Shows `↓ download  ↑ upload` in the GNOME top bar. Click it to switch between MB/s and Mbps.

This is the most native option on Ubuntu (GNOME 45+, i.e. Ubuntu 24.04 and later): it lives in the panel, so Wayland's rules about window placement and always-on-top don't apply. Use it instead of, or alongside, the floating widget.

## Install

From a release zip:

```bash
gnome-extensions install --force winternetmeter@rakibulx33.shell-extension.zip
```

From this repo:

```bash
cp -r gnome-extension/winternetmeter@rakibulx33 ~/.local/share/gnome-shell/extensions/
```

Then log out and back in (Wayland can't restart the shell in place), and enable it:

```bash
gnome-extensions enable winternetmeter@rakibulx33
```

## Which interfaces it measures

Same rule as the desktop app: interfaces with a default route, ignoring virtual ones (`docker*`, `veth*`, `br-*`, `virbr*`, `tun*`, `wg*`, `tailscale*`, …). If nothing has a default route, it sums all real interfaces.
