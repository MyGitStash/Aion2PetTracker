# Overlay visibility and multi-monitor recovery

Overlays could remain behind other windows after Alt-Tab, minimizing or a
display change. The merchant panel deliberately hid outside game focus and its
saved position was clamped to the game monitor on every refresh.

- **Always on top** is enabled by default, including for existing settings.
  Native topmost state is restored without activating the overlays. The option
  can be disabled and is saved with the other preferences.
- Foreground, display, DPI and handle changes request a cached-surface recovery.
  A lightweight native Z-order check runs at most once a second while visible;
  the bitmap is not redrawn on every timer tick.
- The merchant panel retains the last observed shop scene when switching apps.
  It still requires valid unexpired offers and a live game window. Automatic
  detection resumes when the game regains focus. Other apps are not sampled.
- Saved merchant placement can remain on another monitor. Disconnected or
  rearranged display coordinates are clamped into a connected working area.
- Closing settings minimizes the app to the taskbar instead of hiding it.
  The taskbar icon reopens settings; **Restore overlays** is available in both
  settings and the tray menu, without toggling the overlay off and on.
- Preview checks report failures to their own output folder without a modal
  startup-error popup. They do not start capture or save user settings.

Verification includes the existing 208 protocol/state tests plus native Windows
checks for topmost loss, minimize recovery, display messages, focus preservation,
click-through after handle recreation, preference migration and placement on all
connected monitors. Those checks use transparent demo windows and no game input.
They do not prove behavior on every other user's PC or exclusive-fullscreen setup.

The public release page continues to offer one program ZIP with an English
quick-start guide. Keep `Daten` when updating. Npcap remains a separate prerequisite.
