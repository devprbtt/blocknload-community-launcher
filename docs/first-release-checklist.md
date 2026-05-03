# First Release Checklist

Use this for the first real `v2.0.0` release on:

- `devprbtt/blocknload-community-launcher`

## Before Push

1. Make sure the repo contains:
   - `v2/src`
   - `v2/tools`
   - `v2/updates`
   - `v2/docs`
2. Make sure `v2/updates/manifest-stable.json` is still the placeholder template, not a sample manifest from a local test.
3. Make sure the default manifest URL is correct:
   - `https://raw.githubusercontent.com/devprbtt/blocknload-community-launcher/main/v2/updates/manifest-stable.json`

## First Push

1. Push the repository contents to GitHub.
2. Confirm these URLs load publicly:
   - repo root
   - `v2/updates/manifest-stable.json`

## Build Release Artifacts

Run:

```powershell
& "K:\BNL EXPORTED\v2\tools\Build-ReleaseArtifacts.ps1" `
  -Version "2.0.0" `
  -Repository "devprbtt/blocknload-community-launcher" `
  -ReleaseTag "v2.0.0" `
  -Channel "stable" `
  -MinimumSupportedVersion "1.9.0"
```

Expected outputs:

- `v2/release/2.0.0/launcher/BnlCommunityFixes.exe`
- `v2/release/2.0.0/updater/BnlUpdater.exe`
- `v2/release/2.0.0/manifest-stable.json`

## Create GitHub Release

1. Create tag:
   - `v2.0.0`
2. Create GitHub release from that tag.
3. Upload:
   - `BnlCommunityFixes.exe`
   - `BnlUpdater.exe`

## Publish Stable Manifest

Run:

```powershell
& "K:\BNL EXPORTED\v2\tools\Publish-ReleaseManifest.ps1" `
  -SourceManifestPath "K:\BNL EXPORTED\v2\release\2.0.0\manifest-stable.json" `
  -Channel "stable"
```

Then commit and push:

- `v2/updates/manifest-stable.json`

## Validate

1. Open the raw manifest URL in browser.
2. Confirm it references:
   - `https://github.com/devprbtt/blocknload-community-launcher/releases/download/v2.0.0/BnlCommunityFixes.exe`
   - `https://github.com/devprbtt/blocknload-community-launcher/releases/download/v2.0.0/BnlUpdater.exe`
3. Run a local launcher build with the default settings and confirm it can see the manifest.

## After First Release

For `v2.0.1+`, repeat:

1. build artifacts
2. upload release assets
3. promote generated manifest
4. commit updated `v2/updates/manifest-stable.json`
