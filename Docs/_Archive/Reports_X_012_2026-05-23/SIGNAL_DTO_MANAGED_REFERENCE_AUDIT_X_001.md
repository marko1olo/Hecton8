# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# SIGNAL DTO MANAGED REFERENCE AUDIT X_001

Generated: 2026-05-23 22:39:15 +04:00
Evidence class: STATIC SOURCE ONLY. Text/brace scanner over non-Editor C# files; compile/profiler proof is separate.

## Summary

- Files with ISignal structs scanned: 55.
- ISignal structs found outside Editor folders: 292.
- Managed/string/native-container field violations in ISignal structs: 0.
- Duplicate ISignal struct names: 0.
- Layout warning count by direct-attribute text scan: 0.

## Violations

- None found by field-line scanner.

## Duplicate Names

- None.

## Layout Warnings

Direct-attribute text warning only; some structs may rely on default sequential unmanaged layout or nearby partial contract proof. First 200 warnings are listed for follow-up.

- None.

## Fresh Verification - 2026-05-23 23:05:00 +04:00

- Line/brace scanner over non-Editor Assets/_Project/Scripts/**/*.cs: 292 ISignal structs.
- Managed/string/native-container field violations matched against GameObject, Transform, string, FixedString*, NativeArray, NativeQueue, NativeList, NativeHashMap: 0.
- No DTO files were changed after the previous duplicate/string scrub; current code edits were limited to bus lifecycle/registry/facade/reporting.

## Fresh Verification - 2026-05-23 23:18:00 +04:00

- Core payload declaration scan still reports 0 exact ISignal DTO definitions in `Assets/_Project/Scripts/Core/GlobalSignals.cs`.
- Core payload declaration scan still reports 166 exact ISignal DTO definitions in `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.*.cs`.
- Core payload poison scan against GameObject, Transform, string, FixedString*, NativeArray, NativeQueue, NativeList, and NativeHashMap found 0 payload hits; only transformer method names matched the word `Transform`.
