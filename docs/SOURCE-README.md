# Aion2 Pet Tracker

For the shop compatibility update and fallback option, see
[Release 1.0.1](RELEASE-1.0.1.md). The original Release 1 notes below describe the
baseline; current builds include the newer header search and tab compatibility.

A compact Windows overlay for pet souls, genus crystals and pet progress at
the Cloud Merchant in AION 2. **Release 1** is the first named public release.
The app, executable and portable folder have no version suffix.

## Start playing

1. Extract **Aion2-Pet-Tracker.zip** to a writable folder.
2. Run **Aion2PetTracker.exe**.
3. If Npcap is missing, follow the tracker's one-time setup screen. It opens
   the [official Npcap download](https://npcap.com/#download); complete the
   vendor's installer, then click **Check again**.
4. Keep the tracker running and start AION 2. Log into your character normally.
5. Character and server are detected automatically. Position the overlays,
   then enable **Lock overlays · Click through to game**.

After the first setup, the usual routine is simply **tracker first, game second**.
The .NET runtime and the pet catalogue are inside the EXE. You do not need
Python, Wireshark, another tracker, an SDK or a separate capture application.
Npcap is a Windows capture driver and is installed once, not opened for each use.

[Deutsche Startanleitung](docs/START-DE.md) · [How it works](docs/ARCHITECTURE.md) ·
[Review the source](docs/REVIEW.md) · [Release notes](docs/RELEASE-1.md)

## Features

- Permanent six-crystal header, including a wide Rainbow balance field.
- Separate farm cards for each pet, with portrait, genus, level and progress.
  Each card stays for 60 seconds after the last soul gained for that pet.
- An independent pet-details popup for Cloud Merchant Pets and Special offers.
- Per-character/server autosave, atomic file replacement and a recovery backup.
- Transparent overlays, independent positions, opacity and click-through locking.
- English interface and consistent number formatting.

Closing settings keeps the tracker in the system tray. Use **Exit** in the tray
menu to stop it. Alt-Tab and idle time do not erase saved balances or add an
approximation symbol. A real capture gap or unsupported update may mark a value
with `≈` until a new complete snapshot arrives.

## First synchronization and known limits

Start the tracker **before the game** for the initial complete collection.
Starting midway through a session can display saved balances and new observed
gains, but it cannot request messages already missed. Opening a menu or changing
areas does not reliably resend the collection. The next normal full game login
can provide it. Unknown and known zero are different states.

The tracker counts pet soul points (5 + 25 + 75 to MAX), not unused pet items
in your inventory. It does not derive exact counts from a shop's Owned label.
Registration and level messages update the shared pet state. The level format
has been observed live; registration handling is covered by tests and a pinned
upstream reference, with its purchase path still awaiting live confirmation.

The shop popup checks a small visible header region at most once per second
while relevant offers exist and the game is in the foreground. Unusual UI
scaling, covered headers or exclusive/protected display modes may prevent
recognition. Screenshots are neither recorded nor uploaded by normal use.

## Privacy and review

Normal operation receives server-to-client traffic locally through Npcap. There
is no game-memory access, injection, key extraction, packet sending, automatic
purchasing, input automation, analytics, upload or application update service.
The setup website opens only after a button click. Npcap is separately licensed
and is **not bundled or silently installed**; see its
[redistribution terms](https://npcap.com/oem/redist).

`Daten/` beside the EXE contains your settings and saved character data. Keep this
folder for upgrades. It is excluded from Git and every release archive.
Source availability and file hashes help inspection; neither is a guarantee
against bugs or an independent malware verdict. The EXE is not code-signed.
This fan application has no claimed official approval or ban immunity.

## Build from source

On Windows, install the exact .NET SDK specified in `global.json` (10.0.402) and run:

```powershell
./scripts/Build.ps1
```

The script restores locked dependencies, runs the tests and publishes a
self-contained Windows x64 EXE to `artifacts/publish/`. The SDK is needed only
for building, not for using the portable release. `scripts/Package.ps1` creates
the portable, source and combined review archives plus SHA-256 manifests.
`scripts/Verify.ps1` builds and tests a freshly extracted source archive.

## Repository layout

```text
src/Aion2PetTracker.App/    Windows UI, startup, overlays and rendering
src/Aion2PetTracker.Core/   Capture, protocol, pet state and persistence
tests/Aion2PetTracker.Tests/  Protocol and state regression tests
config/                   Embedded pet catalogue and shop/header mappings
fixtures/                 Public and synthetic protocol test fixtures
docs/                     Startup, architecture, review and release notes
scripts/                  Build, package, verification and GitHub publishing
licenses/                 Original dependency, asset and source notices
```

The code retains its GPL-3.0 provenance. Dependency and game-artwork rights are
described separately in `licenses/`; do not treat the game artwork as newly
GPL-licensed. Original upstream notices remain included.
