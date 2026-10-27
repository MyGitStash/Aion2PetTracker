# Reviewing Release 1

## What is supplied

- Complete corresponding source, tests, locked dependency graphs and notices.
- A Windows x64 self-contained EXE and SHA-256 manifests.
- A Git bundle with the initial release commit and tag (when packaging from Git).
- Build/package scripts and a fresh-extraction verification script.

The bundle can be inspected without a hosting account:

```powershell
git clone ./Aion2-Pet-Tracker.bundle Aion2-Pet-Tracker
cd Aion2-Pet-Tracker
git log --oneline --decorate
git show release-1
./scripts/Build.ps1
```

Start review at `src/Aion2PetTracker.App/Program.cs`, then
`Services/PetTrackerService.cs`. `NpcapSetup.cs` contains the only website
launch. The other shell launch opens the local data folder after a user click.
Capture uses SharpPcap/PacketDotNet, with local framing/LZ4 decoding.

## Check the download

```powershell
Get-FileHash ./Aion2PetTracker.exe -Algorithm SHA256
```

Compare the result with the EXE entry in the archive's SHA256SUMS.txt. A manifest
inside an archive detects accidental corruption; authenticity additionally
requires a trusted independently obtained manifest or release provenance.
An unsigned EXE is not a signed publisher assertion. Available source and passed
tests do not by themselves establish that a program is free of malware.

`scripts/Verify.ps1` expands the source archive into a new directory, verifies
its file hashes, restores locked packages and runs the tests and app build.
Release builds omit debug symbols so that debugger metadata does not depend on
the build checkout; Debug builds retain the SDK's normal symbols. Verification
also compares the rebuilt EXE with an existing original EXE when available.
Use the pinned SDK and locked dependencies for that comparison. This is a local
reproducibility check, not an independent malware certification.
Review the dependency and artwork licenses before redistributing modified builds.

## Personal data and publishing

Runtime settings, character totals, private packet captures, tokens and local
diagnostic logs are excluded from both Git and the release archives. Source
fixtures originate from the documented public upstreams or synthetic data; the
level-change regression body contains pet/stage fields, not account identity.

An ordinary GitHub repository and Releases download need an authenticated owner.
The local bundle is portable but does not create a public website. Once a target
account is available, `scripts/Publish-GitHub.ps1 -Repository owner/name` publishes
the prepared repository and Release 1 using that account's GitHub CLI login.
It never signs up for an account or embeds a token in source.
