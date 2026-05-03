# Release Workflow

## Goal

Produce:

- `BnlCommunityFixes.exe`
- `BnlUpdater.exe`
- `manifest-stable.json`

The launcher downloads the manifest, compares versions, downloads the two binaries from GitHub Releases, verifies SHA256, then hands off to the updater.

## Build Local Release Artifacts

Example:

```powershell
& "K:\BNL EXPORTED\v2\tools\Build-ReleaseArtifacts.ps1" `
  -Version "2.0.0" `
  -Repository "devprbtt/blocknload-community-launcher" `
  -ReleaseTag "v2.0.0" `
  -Channel "stable" `
  -MinimumSupportedVersion "1.9.0"
```

This writes:

- `release\2.0.0\launcher\BnlCommunityFixes.exe`
- `release\2.0.0\updater\BnlUpdater.exe`
- `release\2.0.0\manifest-stable.json`

## Publish To GitHub Releases

1. Create a GitHub release tag such as `v2.0.0`
2. Upload:
   - `BnlCommunityFixes.exe`
   - `BnlUpdater.exe`
3. Publish the release

The generated manifest already points to:

`https://github.com/<repo>/releases/download/<tag>/<asset>`

## Publish The Manifest

Recommended GitHub-hosted path:

- `https://raw.githubusercontent.com/devprbtt/blocknload-community-launcher/main/updates/manifest-stable.json`

The launcher should point to the stable manifest URL, not to the GitHub "latest release" page.

To promote a generated manifest into the repo-hosted path:

```powershell
& "K:\BNL EXPORTED\v2\tools\Publish-ReleaseManifest.ps1" `
  -SourceManifestPath "K:\BNL EXPORTED\v2\release\2.0.0\manifest-stable.json" `
  -Channel "stable"
```

Then commit and push:

- `updates/manifest-stable.json`

## Channels

Suggested:

- `manifest-stable.json`
- `manifest-beta.json`

Each manifest can point to different release tags or assets.

## Minimum Supported Version

Use `minimum_supported_version` to force upgrades when older launchers are no longer safe or compatible.

## Notes

`notes` is plain text intended for the update prompt or future release UI.

## Suggested Launcher Settings

Example launcher settings file:

```json
{
  "product": "BnlCommunityFixes",
  "channel": "stable",
  "manifestUrl": "https://raw.githubusercontent.com/devprbtt/blocknload-community-launcher/main/updates/manifest-stable.json"
}
```
