🎯 **What:** Removed the deprecated `RaiseStrengthChanged` method from `AtlasSignalEvents`.
💡 **Why:** The method was marked obsolete with `true` (error) and provided a clear migration path to `TryRaiseStrengthChanged`. Removing it cleans up dead code and improves maintainability.
✅ **Verification:** Ran full verify sweep and checked references. All usages are already using `TryRaiseStrengthChanged`.
✨ **Result:** Cleaner API surface without deprecated dead code.
