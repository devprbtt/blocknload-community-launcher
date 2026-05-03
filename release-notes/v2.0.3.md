# BNL Community Fixes v2.0.3

Maintenance release for the `v2` update delivery path.

Included in this release:

- manifest HTTP requests now use cache-busting query parameters
- manifest checks send no-cache headers
- release asset download flow is unchanged

Important:

- this reduces the chance of stale `manifest-stable.json` responses from GitHub raw edge caching
- it does not change the configured manifest URL or the GitHub Releases hosting model
