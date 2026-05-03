# BNL Community Fixes v2.0.4

Maintenance release for the `v2` manifest lookup path.

Included in this release:

- default manifest source now uses the GitHub contents API
- launcher can decode base64 manifest payloads returned by the contents API
- manifest requests still use cache-busting and no-cache headers

Important:

- this avoids stale release detection caused by delayed `raw.githubusercontent.com` edge updates
- GitHub Releases remains the binary host; only the manifest fetch endpoint changed
