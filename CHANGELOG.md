# Changelog

All notable changes to Controlability. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and versions follow [Semantic Versioning](https://semver.org/).

## [1.0.0-rc1]

First release candidate.

### Added
- Any game controller (through SDL3) becomes a virtual Xbox 360 pad (ViGEmBus), with per-slot assignment and a grace period for reconnects.
- Keyboard + controller local co-op: Mode A (keyboard stays a keyboard) and Mode B (keyboard and mouse become a pad).
- Profiles with a mapping editor, response curves, deadzones, import/export, and automatic profile per game.
- Extras: gyro to right stick, touchpad as mouse, turbo, toggle buttons, battery alerts.
- HidHide integration: hides real controllers from games, unhides on exit, on a crash, and from the tray ("Unhide all").
- WPF app with dashboard, devices page, tray icon, first-run wizard, settings, diagnostics and logging.
- Windows installer (Inno Setup): shortcuts, optional start with Windows, optional ViGEmBus and HidHide install, clean uninstall.
- `Controlability.exe --cleanup`: unhides all controllers and removes the autostart entry (used by the uninstaller).
