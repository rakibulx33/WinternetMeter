// Winternet Meter — GNOME Shell top-bar extension (GNOME 45+).
//
// The most native option on Ubuntu: the speed lives in the top bar, so it works
// on Wayland without any window placement or always-on-top tricks.
//
// How it works (same rules as the desktop app's Core library):
//   1. Read byte counters per interface from /proc/net/dev.
//   2. Measure interfaces that carry the default route (/proc/net/route, /proc/net/ipv6_route),
//      skipping virtual ones (docker, veth, VPN tunnels...). VPN traffic is still counted —
//      encrypted — on the physical interface, so skipping the tunnel avoids double counting.
//   3. speed = (bytes now − bytes before) / seconds between readings.

import GLib from 'gi://GLib';
import St from 'gi://St';
import Clutter from 'gi://Clutter';

import * as Main from 'resource:///org/gnome/shell/ui/main.js';
import * as PanelMenu from 'resource:///org/gnome/shell/ui/panelMenu.js';
import {Extension} from 'resource:///org/gnome/shell/extensions/extension.js';

const VIRTUAL = /^(lo$|docker\d*|br-|veth|virbr|vboxnet|vmnet|tun\d|tap\d|wg\d|zt|tailscale|cni|flannel|cali|lxcbr|lxdbr|podman|vnet|ifb|dummy|sit\d)/;
const BYTE_UNITS = ['KB/s', 'MB/s', 'GB/s', 'TB/s'];
const BIT_UNITS = ['Kbps', 'Mbps', 'Gbps', 'Tbps'];
const MAX_GAP_US = 30 * 1000 * 1000;

const decoder = new TextDecoder();

function readText(path) {
    try {
        const [ok, bytes] = GLib.file_get_contents(path);
        return ok ? decoder.decode(bytes) : '';
    } catch (_e) {
        return '';
    }
}

/** Map of interface name → {rx, tx} total bytes. */
function readCounters() {
    const counters = new Map();
    const lines = readText('/proc/net/dev').split('\n').slice(2);
    for (const line of lines) {
        const colon = line.indexOf(':');
        if (colon < 0)
            continue;
        const name = line.slice(0, colon).trim();
        const fields = line.slice(colon + 1).trim().split(/\s+/);
        if (fields.length < 9)
            continue;
        counters.set(name, {rx: Number(fields[0]), tx: Number(fields[8])});
    }
    return counters;
}

/** Interfaces that have a default route (IPv4 or IPv6). */
function defaultRouteInterfaces() {
    const result = new Set();
    for (const line of readText('/proc/net/route').split('\n').slice(1)) {
        const cols = line.trim().split(/\s+/);
        if (cols.length > 2 && cols[1] === '00000000')
            result.add(cols[0]);
    }
    for (const line of readText('/proc/net/ipv6_route').split('\n')) {
        const cols = line.trim().split(/\s+/);
        if (cols.length >= 10 && /^0{32}$/.test(cols[0]) && cols[1] === '00' && cols[9] !== 'lo')
            result.add(cols[9]);
    }
    return result;
}

function pickInterfaces(counters) {
    const real = [...counters.keys()].filter(name => !VIRTUAL.test(name));
    const routed = defaultRouteInterfaces();
    const withRoute = real.filter(name => routed.has(name));
    return withRoute.length > 0 ? withRoute : real;
}

function format(bytesPerSecond, bits) {
    let value = bits ? bytesPerSecond * 8 / 1000 : bytesPerSecond / 1024;
    const step = bits ? 1000 : 1024;
    const units = bits ? BIT_UNITS : BYTE_UNITS;
    let i = 0;
    while (value >= 999.95 && i < units.length - 1) {
        value /= step;
        i++;
    }
    return `${value.toFixed(1).padStart(5, ' ')} ${units[i]}`; // figure space keeps width steady
}

export default class WinternetMeterExtension extends Extension {
    enable() {
        this._bits = false;
        this._last = null;

        this._button = new PanelMenu.Button(0.0, this.metadata.name, true);
        this._label = new St.Label({
            text: '↓ —  ↑ —',
            y_align: Clutter.ActorAlign.CENTER,
            style_class: 'winternetmeter-label',
        });
        this._button.add_child(this._label);
        this._button.connect('button-press-event', () => {
            this._bits = !this._bits;
            this._update();
            return Clutter.EVENT_STOP;
        });
        Main.panel.addToStatusArea(this.uuid, this._button, 0, 'right');

        this._timer = GLib.timeout_add_seconds(GLib.PRIORITY_DEFAULT, 1, () => {
            this._update();
            return GLib.SOURCE_CONTINUE;
        });
        this._update();
    }

    disable() {
        if (this._timer) {
            GLib.source_remove(this._timer);
            this._timer = null;
        }
        this._button?.destroy();
        this._button = null;
        this._label = null;
        this._last = null;
    }

    _update() {
        if (!this._label)
            return;

        const counters = readCounters();
        const names = pickInterfaces(counters).sort();
        let rx = 0, tx = 0;
        for (const name of names) {
            rx += counters.get(name).rx;
            tx += counters.get(name).tx;
        }

        const now = GLib.get_monotonic_time();
        const key = names.join(',');
        const last = this._last;
        this._last = {rx, tx, now, key};

        if (!last || last.key !== key || rx < last.rx || tx < last.tx || now <= last.now || now - last.now > MAX_GAP_US) {
            if (names.length === 0)
                this._label.text = '↓ —  ↑ —';
            return;
        }

        const seconds = (now - last.now) / 1e6;
        this._label.text = `↓ ${format((rx - last.rx) / seconds, this._bits)}  ↑ ${format((tx - last.tx) / seconds, this._bits)}`;
    }
}
