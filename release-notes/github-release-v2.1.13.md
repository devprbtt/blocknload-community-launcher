# v2.1.13

Release v2.1.13

- New feature: Aim Healthbar — enemy healthbars now appear when you aim at them
- Fixed NullReferenceException spam in ZoneServiceListener.Damage and UnitUpdate
- Fixed TypeLoadException in GuiHitAlertMaker.Start() and GuiDamageNumberDetector.Start() — resolved by removing bridge MonoBehaviours from the platform assembly and using reflection-based health change subscription
- General stability improvements to the experimental assembly build process

Assets:
- BnlCommunityFixes.exe
- BnlUpdater.exe

Committed and tagged as `v2.1.13`.
