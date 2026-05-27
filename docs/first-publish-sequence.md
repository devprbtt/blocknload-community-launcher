# First Publish Sequence

Use this after the repo is pushed to GitHub.

Repo:

- `devprbtt/blocknload-community-launcher`

## 1. Prepare Release Files

Run:

```powershell
& "K:\BNL EXPORTED\v2\tools\Prepare-FirstGitHubRelease.ps1"
```

That will:

- publish `BnlCommunityFixes.exe`
- generate `release\2.0.0\manifest-stable.json`
- include the text from `release-notes\v2.0.0.md`

## 2. Create GitHub Release

Create a GitHub release:

- tag: `v2.0.0`

Upload:

- `v2\release\2.0.0\launcher\BnlCommunityFixes.exe`

## 3. Promote Manifest

Run:

```powershell
& "K:\BNL EXPORTED\v2\tools\Publish-ReleaseManifest.ps1" `
  -SourceManifestPath "K:\BNL EXPORTED\v2\release\2.0.0\manifest-stable.json" `
  -Channel "stable"
```

## 4. Commit And Push Manifest

Commit:

- `v2/updates/manifest-stable.json`

Push to `main`.

## 5. Validate Raw Manifest URL

Open:

- `https://raw.githubusercontent.com/devprbtt/blocknload-community-launcher/main/updates/manifest-stable.json`

Confirm:

- version is `2.0.0`
- release URLs point to `devprbtt/blocknload-community-launcher`
- SHA256 values are populated

## 6. Validate Launcher Default

The launcher default manifest URL is:

- `https://raw.githubusercontent.com/devprbtt/blocknload-community-launcher/main/updates/manifest-stable.json`

That means a fresh `v2` launcher install will check that path automatically unless overridden by:

- `%LocalAppData%\BNL-CommunityFixes\data\launcher-settings.json`
- `BNL_MANIFEST_URL`
