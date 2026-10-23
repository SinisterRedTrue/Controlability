# Changelog

All notable changes to Controlability. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and versions follow [Semantic Versioning](https://semver.org/).

## [1.1.0] - 2026-10-23

### Changed
- Redesigned the dashboard, devices page, settings, navigation, app branding and tray icons.
- Rebuilt all six profile editor tabs with a compact profile library, consistent save controls, controller symbols and mapping tables.
- Added shaded live Xbox output, stick coordinates and response graphs, and physical/output trigger comparisons.
- Updated keyboard/mouse mappings, gyro and touchpad controls, and turbo/toggle tables, with additional mappings available in expandable sections.
- Grouped controller applicability by family while preserving individual controller selections and game associations.
- Improved theme colours, layout at different window sizes, and keyboard binding cancellation.
- Added visual rendering checks and profile editor regression coverage.
- Release packaging supports a dedicated output directory, preserving other local build and review artifacts.

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
