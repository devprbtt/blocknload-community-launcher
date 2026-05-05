# v2.1.16

Release v2.1.16

- Fixed the runtime menu helper crash caused by missing framework dependencies in the game runtime
- Switched runtime menu persistence to a plain-text config format
- Disabled the FOV feature path in the experimental assembly builder to stop the `CameraFov.Update()` regression
- FOV config remains stored but is ignored by this build while the feature is quarantined

Assets:
- BnlCommunityFixes.exe
- BnlUpdater.exe
