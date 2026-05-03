# BNL Community Launcher

`v2` of the Block N Load community launcher, built around a compiled launcher/updater pair and a GitHub-hosted manifest update flow.

Target repo:

- `devprbtt/blocknload-community-launcher`

## What This Contains

- `BnlCommunityFixes.Core`
  Shared models and services for manifests, hashing, downloads, paths, and logging.
- `BnlCommunityFixes.Launcher`
  The main launcher binary. Handles bootstrap, update checks, downloads, verification, and updater handoff.
- `BnlCommunityFixes.Updater`
  The file-swap helper that replaces the launcher, restores backup on failure, and restarts the app.

## Current Scope

- fixed install path under `%LocalAppData%\BNL-CommunityFixes`
- configurable manifest source
- local or remote manifest fetch
- SHA256 verification for downloaded assets
- launcher replacement and restart through updater handoff
- GitHub Releases based artifact flow
- stable/beta manifest promotion model

## Not Migrated Yet

- Block N Load path detection
- patch configuration UI
- DLL patching and deployment flow from the legacy launcher

## Repo Layout

- `src/`
  Source projects for launcher, updater, and core libraries.
- `tools/`
  Scripts for local smoke testing, release artifact generation, and manifest promotion.
- `updates/`
  Stable and beta manifest files that the launcher can poll remotely.
- `docs/`
  Release workflow and first-publish instructions.
- `release-notes/`
  Versioned release notes used when generating manifests.

## Default Manifest URL

The launcher defaults to:

- `https://api.github.com/repos/devprbtt/blocknload-community-launcher/contents/updates/manifest-stable.json?ref=main`

This can be overridden by:

- `%LocalAppData%\BNL-CommunityFixes\data\launcher-settings.json`
- `BNL_MANIFEST_URL`

## Local Update Test

Run the full local self-update smoke test:

```powershell
& "K:\BNL EXPORTED\v2\tools\Invoke-LocalUpdateSmokeTest.ps1"
```

That publishes a local `2.0.0 -> 2.0.1` update path, points the launcher at a `file://` manifest, and verifies launcher replacement and restart.

## First Release

Prepare the first GitHub release:

```powershell
& "K:\BNL EXPORTED\v2\tools\Prepare-FirstGitHubRelease.ps1"
```

That generates:

- `release/2.0.0/launcher/BnlCommunityFixes.exe`
- `release/2.0.0/updater/BnlUpdater.exe`
- `release/2.0.0/manifest-stable.json`

Then:

1. Create GitHub release tag `v2.0.0`
2. Upload `BnlCommunityFixes.exe`
3. Upload `BnlUpdater.exe`
4. Promote the generated manifest into `updates/manifest-stable.json`
5. Commit and push the updated manifest

## Key Files

- [Release workflow](docs/release-workflow.md)
- [First release checklist](docs/first-release-checklist.md)
- [First publish sequence](docs/first-publish-sequence.md)
- [GitHub Actions build workflow](.github/workflows/build-v2.yml)
- [Stable manifest template](updates/manifest-stable.json)
- [Beta manifest template](updates/manifest-beta.json)
- [GitHub launcher settings sample](testdata/launcher-settings.github.json)
- [First release notes](release-notes/v2.0.0.md)
