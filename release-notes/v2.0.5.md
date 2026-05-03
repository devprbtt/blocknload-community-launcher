# BNL Community Fixes v2.0.5

Maintenance release for the `v2` GitHub contents manifest flow.

Included in this release:

- strips UTF-8 BOM markers from decoded manifest payloads
- keeps the GitHub contents API manifest source introduced in `v2.0.4`
- no change to updater swap behavior or release asset hosting

Important:

- some GitHub contents responses decode to JSON text beginning with a BOM
- `v2.0.5` handles that cleanly and restores successful manifest parsing
