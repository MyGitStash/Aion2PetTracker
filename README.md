# Aion2 Pet Tracker — Release 1

Track your **pet souls, crystal balances and pet progress** with compact overlays for AION 2.

<img width="840" height="248" alt="Pet soul tracker showing a pet portrait, level and soul progress" src="https://github.com/user-attachments/assets/4881e23b-175f-467c-9a69-5793da5b9654" />

## 🚀 Quick Start

1. **Download and extract** the ZIP.
2. Run **Aion2PetTracker.exe**.
3. If prompted, install [Npcap](https://npcap.com/#download) once, then click **Check again**.
4. **Start AION 2 and log in.** Your character is detected automatically.
5. Position the overlays, then enable **Lock overlays**.

> **For future sessions: start the tracker first, then the game.**

## ✨ Features

- **Pet soul tracking** — See each pet’s portrait, group, level and soul progress.
- **Multi-pet farming** — Track several pets in separate cards.
- **Crystal balances** — Keep your group crystals and Rainbow souls in view.
- **Cloud Merchant information** — Check your saved pet progress while browsing pet offers.

## 📸 Screenshots

*All screenshots show demo data without personal information.*

<details>
<summary><strong>Crystal balances</strong></summary>

The permanent header keeps all six crystal balances together, including Rainbow souls.

<img width="840" height="84" alt="Crystal balance header with six crystal types" src="https://github.com/user-attachments/assets/0a5e0748-2aff-43fa-aa9b-2de1c27affc3" />

</details>

<details>
<summary><strong>Multi-pet farming</strong></summary>

Each pet gets its own card with independently updated gains and progress.

<img width="840" height="584" alt="Multiple pet cards showing separate soul gains and level progress" src="https://github.com/user-attachments/assets/401f4714-e139-4959-8a7b-2959167958e2" />

</details>

<details>
<summary><strong>New pet notification</strong></summary>

A compact notification highlights a newly registered pet.

<img width="840" height="248" alt="New pet notification for Potcrab" src="https://github.com/user-attachments/assets/8db204c6-8762-4c96-9a07-e11a6dc02e12" />

</details>

<details>
<summary><strong>Cloud Merchant pet information</strong></summary>

A separate panel shows pet groups, levels and soul totals for the available pet offers.

<img width="435" height="915" alt="Cloud Merchant panel showing pet levels and soul totals" src="https://github.com/user-attachments/assets/d31787eb-a1d8-4671-92d2-865ee37d3780" />

</details>

## 💾 Saving & Updates

- Your progress **saves automatically**.
- Keep the **Daten** folder when updating to preserve your settings and saved progress.
- Closing the settings window keeps the tracker running in the system tray.
- To close it completely, right-click the tray icon and select **Exit**.


## Source code

**Shop compatibility update:** if the merchant panel does not appear, use the
[latest release](https://github.com/MyGitStash/Aion2PetTracker/releases/latest),
open **Pets** or **Special**, and check the shop status in settings. If needed,
enable **Show merchant panel while game is focused**. This fallback uses the
last received valid offers. See [update details](docs/RELEASE-1.0.1.md).

The complete source is available in [`src/`](src/), with regression tests in
[`tests/`](tests/) and the original notices in [`licenses/`](licenses/).
See the [build and review guide](docs/SOURCE-README.md#build-from-source) to build
the Windows application yourself. The public repository excludes personal saved
character data and private captures.
