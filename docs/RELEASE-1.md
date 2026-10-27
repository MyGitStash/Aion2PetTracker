# Release 1

- Product name: **Aion2 Pet Tracker**.
- Stable executable and archive names without a version suffix.
- Clean App/Core projects and a solution, named namespaces, readable formatting,
  split pet domain files, locked dependencies and documented build scripts.
- One-time Npcap prerequisite screen integrated into the tracker. The separate
  driver is obtained by the user from its vendor, not redistributed.
- Embedded catalogue loading avoids any loose pets.json startup dependency.
- Existing per-character persistence, crystal header, multifarm cards and
  Cloud Merchant information retained.
- Ordinary restored balances stay plain; real uncertainty remains explicit.
- Pet registration and level-message support retained from the previous build.

## Validation scope

The release includes protocol/state regression tests and rendered checks for
transparency, layout, scaling and native overlay uploads. Packaging verifies
ZIP CRC, payload hashes and a fresh extracted-source build/test run.

Previous live evidence covered character identification, ordinary gains, saved
balances, merchant offers and the 1490 level-change layout. A new purchase
through the 0990 registration path has **not** been independently confirmed live.
Already missed historical purchases still require a fresh complete collection.
This source/layout cleanup does not imply that the missing data was recovered.
