# Release 1.0.1 — Shop compatibility

The Cloud Merchant panel could stay hidden because the old detector only checked
a few exact UI scales at the top-left origin. Non-default game UI scaling and
small header offsets could fail that test even when shop offers had arrived.

- Search a bounded range of UI scales and small offsets while requiring both
  existing header shapes. Centred letterbox viewport anchors are also checked.
- Read a small luminance buffer once, with at most one scan per second. Normal
  matching scales have a fast path. Screenshots are not saved or uploaded.
- Accept both the observed Global shop tab IDs and the IDs documented in the
  pinned MIT protocol reference. Offer validation remains strict.
- Add **Show merchant panel while game is focused** as an optional fallback.
  It uses the last valid received offers and does not capture header pixels.
  It still requires game focus, valid shop data and unexpired offers.
- Add a readable shop status to settings and accepted/rejected-message metadata
  to optional local diagnostics. No raw packets or personal data are uploaded.
- Add **Copy shop diagnostics** to the tray menu. It copies only shop message
  counters, detection/display state and resolution/DPI; it excludes character,
  server, account details, balances, network addresses and raw game traffic.
- Hide immediately when another application becomes foreground.

The complete source, tests and licenses can now be browsed directly in GitHub.
Settings and saved character balances remain compatible. Keep `Daten` when
replacing the EXE. The executable and ZIP filenames remain unchanged.

If the panel is missing: update, open Pets or Special once, and check the shop
status. Try the manual display checkbox if automatic detection fails. If no shop
data arrived, switching display mode cannot invent the missing offers.
If the problem persists, use **Copy shop diagnostics** from the tray menu and
include the copied information in a bug report.

The compatibility cases are covered by deterministic tests. Other users'
affected PCs have not been tested directly, so their live result remains to be
confirmed. No game restart or additional purchase is needed for this check.

Validation: 208 protocol/state/detection tests, overlay preview checks, ZIP CRC
and SHA-256 manifests, followed by an extracted-source build and EXE comparison.
