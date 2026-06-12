# DATA_MONOLITH_APEX_SOURCE_REVIEW_1313

Date: 2026-05-25
Agent: 1313
Evidence class: STATIC_SOURCE

## Patched Source

- `Assets/_Project/Scripts/Editor/DataMonolith/OOP_StaticData_Scanner.cs:249` now calls `IsCsvRouteName(source)` in the text fallback path.
- `Assets/_Project/Scripts/Editor/DataMonolith/OOP_StaticData_Scanner.cs:251` now reports `Csv parser route token`, not the narrow `TryLoad/Csv token`.
- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithGlobalDataVaultStressProbe.cs:413` now assigns `directory.SectionTableBytes` to `HeaderSnapshot.SectionTableBytes`.
- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithGlobalDataVaultStressProbe.cs:577` now emits JSON `sectionTableBytes` from `header.SectionTableBytes`.
- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithGlobalDataVaultStressProbe.cs:862` now declares `public uint SectionTableBytes`.

## Static Verification

- `rg` proof: `TryLoad/Csv token` hits = 0 in `OOP_StaticData_Scanner.cs`.
- `rg` proof: `DirectoryBytesValue` hits = 0 in `H8DataMonolithGlobalDataVaultStressProbe.cs`.
- `git diff --check` on the two touched editor files: pass, CRLF warnings only.
- `Docs/Reports/*1313*.json` parse via PowerShell `ConvertFrom-Json`: pass.
- Active `H8StaticDataArena` Windows release forbidden-token hits: 0.
- Active `H8StaticDataArena` Android/non-Windows release forbidden-token hits: 0.
- Strict owned bootstrap slice hits for `new NativeArray`, `new float3`, `new double3`, `UTF8Encoding`, `Substring`, `FatalBootCrashMessage`, `fixed (char* source)`, `string.Format`, and `ToString(`: 0.
- Dotnet/Unity build: not run by explicit user restriction.

## Verdict

This patch improves editor-only release-gate evidence. It does not change runtime hydration and does not resolve the remaining release blockers:

- Android/Quest/non-Windows production monolith loading remains fail-closed.
- 262 strict production parser/file/config blockers remain outside 1313 ownership.
- `H8DataBlobHeader` remains the only ABI-bound strict field-order violation. `H8ItemRecord` was reordered under schema v2 and now requires a monolith re-bake.
