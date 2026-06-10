# BNL Community Launcher

A community-driven launcher and patching tool for **Block N Load** — detects your game install, applies client-side fixes and enhancements, and keeps itself up to date automatically.

Built around a compiled .NET launcher/updater pair with a GitHub-hosted manifest update flow.

**Repository:** `devprbtt/blocknload-community-launcher`

---

## Quick Start

1. Download the [latest `BnlCommunityFixes.exe`](https://github.com/devprbtt/blocknload-community-launcher/releases/latest/download/BnlCommunityFixes.exe).
2. Run it — it auto-detects your Block N Load installation.
3. Pick a server and launch. The launcher patches the game and applies all enabled features on startup.
4. Configure features at any time via the **Feature Settings** button in the launcher window.

The launcher auto-updates on every launch — no manual downloads needed after the first one.

---

## Features

All features are optional and configurable through the **Feature Settings** UI in the launcher.

### Crosshair
Custom reticle colors that change based on damage state (idle / at max range / beyond max range). Control size, spread, brightness, opacity, and shape. Force it to show in ADS or hide it entirely.

### FOV
Override the camera field of view, ADS sensitivity multiplier, and weapon model FOV independently.

### Team Colors
Change the color of friendly and enemy indicators (nameplates, health bars, hit effects). Includes one-click presets: **Beta**, **Classic**, and **Default** season colors.

### Damage & Healing Numbers
Custom colors and sizes for floating combat numbers. Combine rapid hits into a single number, filter out small heals, and toggle friendly / self-healing visibility.

### Heal Alerts
A separate directional indicator that appears when a heal lands, independent from the floating numbers.

### Font
Replace the in-game UI font with any font installed on your system. Adjust size and line spacing.

### Objective Beam
A tall vertical beam above the capture objective, visible from anywhere on the map.

### Shield Timer
Countdown display on the enemy shield buff bar. Supports **circle (clock-style)** and **numeric** modes.

### Build Preview (Local Build Preview)
Blocks and devices appear on your screen immediately when placed, without waiting for server confirmation. Also reduces the delay felt when switching weapons. Recommended for high-ping players.

### Aim Healthbar
Enemy healthbars appear when you aim at them — shows the enemy name and health bar while your crosshair is over them.

### Death Cam HP
When spectating a teammate during the death cam, their health bar and current HP are displayed alongside their name.

### Predicted Saucer Projectile Drop
Astro saucers detonate locally on timeout or right-click, with server reconciliation.

### Runtime Menu (F8)
Toggle an in-game settings menu with **F8** to adjust FOV and other settings at runtime without relaunching.

---

## Requirements

- **Block N Load** installed via Steam
- **.NET Framework 3.5** — required for the experimental patching features. Most machines already have it. If the launcher shows an error after enabling a feature, install it from:  
  *Windows Settings → Apps → Optional features → More Windows features → .NET Framework 3.5*

The base launcher (server selection and launch) works without .NET 3.5.

---

## Project Structure

### Source Projects (`src/`)

| Project | Description |
|---|---|
| `BnlCommunityFixes.Core` | Shared models and services for manifests, hashing, downloads, paths, logging, feature settings, and presets. |
| `BnlCommunityFixes.Avalonia` | The main launcher binary. Handles bootstrap, Steam detection, update checks, downloads, SHA256 verification, updater handoff, and the Feature Settings / Custom Server configuration UI. |
| `BnlCommunityFixes.Updater` | The file-swap helper that replaces the launcher EXE, restores the previous version on failure, and restarts the app. |

### Repository Layout

```
src/              Source code for all projects
tools/            PowerShell scripts for smoke testing, building releases, manifest generation
updates/          Published stable and beta manifest files polled by the launcher
docs/             Release workflow and first-publish instructions
release-notes/    Versioned markdown release notes used when generating manifests
release/          Built release artifacts organized by version
assets/patching/  PowerShell patching scripts, inline C# helpers, and feature config templates
testdata/         Sample configuration files for testing
test-output/      Test harness outputs and smoke test results
```

---

## Update System

The launcher checks for updates every time it opens:

1. Fetches the **update manifest** from the GitHub Contents API.
2. Compares the published version against the installed version.
3. Downloads new `BnlCommunityFixes.exe` and `BnlUpdater.exe` from **GitHub Releases**.
4. Verifies SHA256 checksums.
5. Hands off to the updater, which replaces the launcher and restarts.

**Default manifest URL:**
```
https://api.github.com/repos/devprbtt/blocknload-community-launcher/contents/updates/manifest-stable.json?ref=main
```

This can be overridden by:
- `%LocalAppData%\BNL-CommunityFixes\data\launcher-settings.json`
- `BNL_MANIFEST_URL` environment variable

### External Launcher Refresh
When you run an older downloaded copy of `BnlCommunityFixes.exe` (from Downloads, Desktop, etc.), the launcher detects it, starts the installed copy, and refreshes the older EXE to match the installed version — preventing repeat update prompts.

---

## Release Workflow

### Build Release Artifacts

```powershell
& "K:\BNL EXPORTED\v2\tools\Build-ReleaseArtifacts.ps1" `
  -Version "2.2.5" `
  -Repository "devprbtt/blocknload-community-launcher" `
  -ReleaseTag "v2.2.5" `
  -Channel "stable" `
  -MinimumSupportedVersion "2.2.0"
```

Outputs to `release/<version>/`:
- `launcher/BnlCommunityFixes.exe`
- `updater/BnlUpdater.exe`
- `manifest-stable.json`

### Publish to GitHub Releases

1. Create a GitHub release with tag `v<version>`.
2. Upload `BnlCommunityFixes.exe` and `BnlUpdater.exe`.
3. Promote the generated manifest:

```powershell
& "K:\BNL EXPORTED\v2\tools\Publish-ReleaseManifest.ps1" `
  -SourceManifestPath "K:\BNL EXPORTED\v2\release\2.2.5\manifest-stable.json" `
  -Channel "stable"
```

4. Commit and push `updates/manifest-stable.json`.

See [Release Workflow](docs/release-workflow.md) for details.

---

## Local Testing

### Update Smoke Test

Run the full local self-update flow:

```powershell
& "K:\BNL EXPORTED\v2\tools\Invoke-LocalUpdateSmokeTest.ps1"
```

Publishes a test update path, points the launcher at a `file://` manifest, and verifies launcher replacement and restart.

### Interactive Smoke Test

```powershell
& "K:\BNL EXPORTED\v2\tools\Invoke-LocalUpdateSmokeTest-Interactive.ps1"
```

Same flow but keeps the launcher window visible for manual interaction.

---

## Key Documentation

| File | Description |
|---|---|
| [Release Workflow](docs/release-workflow.md) | Full release process from build to manifest promotion |
| [First Release Checklist](docs/first-release-checklist.md) | Initial setup guide (reference only after first release) |
| [First Publish Sequence](docs/first-publish-sequence.md) | First-time GitHub publish steps (reference only) |

### Release Notes

- [v2.2.5](release-notes/v2.2.5.md) — Latest: manifest URL migration
- [v2.2.4](release-notes/v2.2.4.md) — Clean version display
- [v2.2.3](release-notes/v2.2.3.md) — Version shown in title/UI, post-update perf
- [v2.2.2](release-notes/v2.2.2.md) — External refresh self-awareness
- [v2.2.1](release-notes/v2.2.1.md) — Bootstrap log scanning
- [v2.2.0](release-notes/v2.2.0.md) — External launcher refresh
- [v2.1.17](release-notes/v2.1.17.md) — Predicted saucer drop, FOV re-enabled
- [v2.1.14](release-notes/v2.1.14.md) — Death Cam HP
- [v2.1.13](release-notes/v2.1.13.md) — Aim Healthbar
- [Full changelog →](release-notes/)
