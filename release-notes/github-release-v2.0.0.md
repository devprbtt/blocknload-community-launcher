# BNL Community Launcher v2.0.0

Initial `v2.0` release with the new self-update foundation.

Included:

- compiled `BnlCommunityFixes.exe`
- compiled `BnlUpdater.exe`
- manifest-driven update flow
- SHA256 verification for downloaded assets
- launcher replacement and restart through updater handoff
- rollback path for failed launcher replacement
- GitHub Releases based distribution model

Important:

- this is the bridge release away from the old self-extracting launcher model
- Block N Load specific launcher and patching features are not yet migrated into the new compiled launcher
- the focus of `v2.0.0` is the install/update pipeline itself
