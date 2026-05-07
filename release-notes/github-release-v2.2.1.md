# BNL Community Launcher v2.2.1

Follow-up fix for the repeated update prompt loop.

## Changes
- The installed launcher no longer trusts only one saved bootstrap source path.
- It now scans recent bootstrap log entries and refreshes older launcher EXEs that users keep reopening from Downloads/Desktop/custom folders.
- This covers users updating from older launchers that could not pass the new external replacement target to the updater.

## Files
- BnlCommunityFixes.exe
- BnlUpdater.exe

