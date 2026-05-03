# BNL Community Fixes v2.0.2

Maintenance release for the `v2` updater flow.

Included in this release:

- fixed semantic version parsing for launcher self-update checks
- prevents forced-update loops when the local version includes build metadata
- no change to the manifest or release hosting model

Important:

- `v2.0.1` could re-enter the updater because the local version string included `+<commit>`
- `v2.0.2` correctly treats `2.0.2+...` as version `2.0.2` for comparison
