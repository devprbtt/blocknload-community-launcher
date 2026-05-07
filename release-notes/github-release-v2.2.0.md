# BNL Community Launcher v2.2.0

This release fixes the repeated update prompt loop caused by reopening an older downloaded launcher EXE after a successful update.

## Changes
- The launcher records the external EXE path used during bootstrap.
- After update, the updater and relaunched launcher refresh that external EXE with the latest installed launcher when possible.
- Update handoff now prefers the freshly downloaded updater helper, so new updater behavior is available immediately.

## Files
- BnlCommunityFixes.exe
- BnlUpdater.exe

