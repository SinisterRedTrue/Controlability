# Controlability redesign plan

Concepts generated on 23 October 2026 after reviewing the supplied screenshots and the WPF page sources. These are design proposals; application code is unchanged.

## Visual direction

Use navy surfaces, high-contrast off-white text, subdued secondary text, cyan selection accents, consistent 8px spacing increments and approximately 10px panel corners. Use one logo asset based on 01-logo.png throughout. Keep player colours with explicit player labels; status always needs text as well as colour. Avoid large decorative branding consuming navigation space.

## Screen coverage

1. [Logo](01-logo.png): C/controller connection mark, wordmark and small-icon treatment.
2. [Dashboard](02-dashboard.png): modes, conditional keyboard input style, pause, Steam notice, driver readiness, four player slots, profile selection and explicit device assignment alongside drag/drop.
3. [Devices](03-devices.png): device selection, connection and assignment, capabilities, expandable technical identifiers, hiding, rumble, light colour and physical/virtual input testing.
4. [Buttons](04-profiles-buttons.png): all 15 button mappings, Bind/Clear, live output, profile management and persistent save controls.
5. [Sticks](05-profiles-sticks.png): left/right inner and outer thresholds, curves and live previews.
6. [Triggers](06-profiles-triggers.png): shared dead zone, digital mode, threshold and proposed live comparison.
7. [Keyboard and mouse](07-profiles-keyboard-mouse.png): bindings, mouse sensitivity and decay, focus restriction explanation.
8. [Extras](08-profiles-extras.png): gyro enable/sensitivity/inversion/smoothing/activation, touchpad enable/speed/acceleration, turbo/rate/toggle table. Show every supported button in a scrollable table; the illustration shows representative rows.
9. [Applies to](09-profiles-applies-to.png): controller applicability, linked game list, process entry, executable browsing and running-process selection.
10. [Settings](10-settings.png): section index; startup/tray, keyboard mode, slot policy, polling, Bluetooth grace, hiding/recovery, theme/language, target games, key mappings/reset, controller reference, logs/diagnostics, licences and uninstall. Extended-height overview; normal desktop view scrolls or jumps to the selected section.

## Implementation sequence

1. Fix global foreground/theme resource usage first: current screenshots show black headings and labels on dark surfaces. Define shared text, surface, border, control and focus styles.
2. Implement the app shell, compact logo/navigation and responsive content widths. Ensure keyboard focus, screen reader labels, scaling and light/system theme support.
3. Rework dashboard and device layouts while retaining existing commands, assignments and live data. Show capability-specific controls only when supported and explain unavailable controls.
4. Rework the profile library and persistent editor header, then each of the six tabs. Retain validation, cancel binding, timeout, shipped-profile protections and unsaved-change behaviour. Keep all existing mapping rows accessible through scrolling.
5. Organize settings by indexed sections. Keep recovery distinct and make technical explanations concise. Reuse existing settings storage and commands.
6. Validate existing functional tests, then visually check keyboard operation, empty/disconnected states, missing drivers, paused state, binding in progress, validation errors, long names, 125–200% scaling and smaller windows.

## Corrections before implementation

- Generated screenshots are layout references, not authoritative specifications. Their example values, device IDs, bindings, version strings and product compatibility claims are illustrative. Source definitions remain authoritative.
- Dashboard: Xbox/Normal keyboard input style applies only to Keyboard-as-controller (Mode B). Hide or disable it in Mode A. Mode B must not be described as limited to single-player. Mode A leaves keyboard/mouse untouched; Mode B uses the configured input style.
- Settings: Player slots is an assignment policy (Auto, Remember, Manual), not a count. Bluetooth grace retains the virtual player pad during a dropout, not a delay before hiding. Keyboard mode choices must use the actual mode names.
- Triggers: the existing dead-zone maximum is 0.50, despite the generated scale showing 1.00. Preserve source ranges, units and defaults for all controls.
- Keep the actual controller applicability choices from the source. Do not adopt generated taxonomy labels such as Generic (XInput) without verifying them.
- Some generated screens omit library entries or change the logo and spacing. Standardize on the complete four-profile library, shared editor geometry and selected logo; do not reproduce those inconsistencies.
- Disable Delete for protected shipped profiles. Reflect saved/dirty states correctly; do not show Save active and Profile saved inconsistently.
- New live trigger comparisons, section navigation and summaries are proposed UI improvements and need implementation; the images do not establish that they already exist.
- Preserve every existing setting and mapping. Summary previews in the settings overview must open or expand the complete rows, not replace them.

## Files

All ten PNGs are saved beside this plan. generation-prompts.md records the mockup prompts used with the built-in image generation tool. The logo prompt is summarized there.
