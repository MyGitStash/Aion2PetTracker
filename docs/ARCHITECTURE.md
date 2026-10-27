# Architecture

The Windows host owns one passive capture service. The pipeline is:

```text
Npcap -> adapter/TCP selection -> reassembly -> framing/LZ4
      -> character identity -> SoulTrackingSession -> persistent character state
      -> shop offers -> MerchantTracker
      -> immutable snapshots -> Windows overlays
```

## Responsibilities

- **App/Startup** checks the separate Npcap prerequisite. It opens the vendor
  page only when requested and never downloads or launches an installer.
- **Core/Capture** identifies processes and TCP endpoints through Windows
  metadata, selects an adapter and receives packets. It does not read process
  memory or send packets. CaptureFileSource and PcapNgWriter support optional
  developer diagnostics; the production service supplies no recording path.
- **Core/Protocol** validates framing, bundles, varints and message fields.
  Shared upstream readers are retained with their original notices and tests.
- **Core/Pets** owns the pet catalogue, total-to-level conversion, protocol
  readers, currency balances, shop mapping and character/session state.
- **Core/Storage** provides atomic JSON replacement. SoulStateStore queues
  writes in the background and preserves a validated previous file.
- **App/Overlays** draws the permanent wallet, recent per-pet cards and merchant
  information. Rendering uses only the application's own windows.
- **App/Services/MerchantSceneProbe** checks a bounded visible shop header,
  at most once a second while relevant. Pixel data is not persisted or sent.
  The search handles UI scale and translation; a second bounded anchor covers
  centred letterboxing. Manual display skips pixel capture and retains the
  foreground-game and valid-offer requirements. The cached foreground-window
  identity is checked again on every UI update, including after Alt-Tab.

## Data and uncertainty

Profiles use the normalized character name and server, not a transient actor ID.
Early messages are buffered until identity is established. A saved network flow
can resume its own profile; unrelated unidentified flows are not guessed.
Restored balances retain a Saved source indicator without automatically becoming
approximate. Observed gaps and unsupported relevant updates are separate flags.
Authoritative complete collections replace the baseline. Shop offers never
change owned counts. Registration and level metadata are absolute stage
information and do not manufacture extra farm drops.

The service and state mutation run under the capture gate. Background storage
and render invalidation avoid repeated work when data is unchanged. The settings
window may be hidden without stopping capture or autosave.

## Development entry points

`--render-preview <folder>` renders sample data and the setup screen without
starting capture. `--status-file <path>` writes local diagnostic snapshots.
Neither switch uploads data. These files must not be added to a public release.
The old development-only merchant capture replay switch was removed.
