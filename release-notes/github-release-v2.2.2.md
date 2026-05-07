# BNL Community Launcher v2.2.2

Follow-up fix for the external launcher replacement flow.

## Changes
- If the installed launcher is the same version or newer than the external EXE the user opened, bootstrap now skips the copy and starts the installed launcher.
- This avoids file-in-use errors after an old external launcher has already been refreshed to the current version.

## Files
- BnlCommunityFixes.exe
- BnlUpdater.exe

