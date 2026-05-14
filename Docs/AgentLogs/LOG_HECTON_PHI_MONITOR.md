# LOG_HECTON_PHI_MONITOR

## 2026-05-14 Active Pass Restart

What was wrong:
- Active H-Phi monitor status/rationale/log and `Tools/Architecture/HectonPhiAudit.ps1` were missing from the live workspace after concurrent cleanup.
- The latest trusted static H-Phi score remains source-only: narrow `0.000896018`, risk-adjusted `0.000081638`, compared with original `0.00062`.
- Data Sovereignty is still the dominant metric bottleneck: `GlobalDataVault` references are near-zero compared with `NativeArray<T>` references.

What was done:
- Recreated the active task, rationale, log, and audit tool files.
- Rechecked `Docs/Tasks/CURRENT_BATCH.md`; no active `HECTON_PHI_MONITOR` prompt exists.
- Identified `SaveBinaryPayloadCodec` malformed-count preallocation hardening as the next low-risk improvement candidate.

Cinematic Cheats used:
- None. This is architecture/static tooling and save-load cold-path hardening.

Exact Microseconds saved:
- Runtime: 0 us measured. Static/CLI work only.
- Corrupt save path: unmeasured avoided allocation risk; profiler evidence absent.

Compile Status:
- Pending. A dependency-excluded Core build failed with missing symbols that exist on disk; full build still required after edits.

Phi Gain:
- Static formula gain pending next scan. This hardening may not move the current H-Phi formula because it improves robustness rather than `SignalBus`, `GlobalDataVault`, or `[StructLayout]` counts.

## 2026-05-14 Save Codec Hardening Patch

What was wrong:
- The active workspace had reverted the prior bool DTO save hardening.
- `ProceduralFaunaStateDTO[]` and `HibernatedFaunaStateDTO[]` were again serialized via raw `WriteStructArray<T>` / `ReadStructArray<T>` despite bool fields.
- Variable-size save readers allocated arrays/lists/dictionaries from serialized counts before checking the minimum remaining payload bytes.

What was done:
- Added fixed `[StructLayout]` sizes to the two fauna DTOs without adding false `[BinaryBlittableSafe]`.
- Replaced fauna raw array blits with explicit fixed-stride field codecs: 16 bytes for procedural fauna and 112 bytes for hibernated fauna.
- Added `CanConsumeBytes` / `CanConsumeCollectionItems` to `BufferReader`.
- Added preallocation guards for string arrays, string/float lists, string dictionaries, int hash sets, custom DTO arrays, PDA arrays, and module arrays.
- Restored `Tools/Architecture/HectonPhiAudit.ps1` and reran static H-Phi.

Cinematic Cheats used:
- None. This was save persistence and static audit work.

Exact Microseconds saved:
- Runtime gameplay: 0 us measured; no hot path was changed.
- Save/load: no profiler measurement. The gain is failure-mode containment: corrupt payloads now fail before large managed allocations in covered collection readers.

Compile Status:
- BLOCKED BY EXISTING DEPENDENCIES. Full command:
  `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal`
- Result: 72 Core errors outside the changed save codec files.
- Representative blockers: unresolved existing `HardwareProfileCatalog`, `SaveMasterHashV10Result`, `SaveFileHeaderV10`, `SaveMasterHashV10`; unrelated `VoxelDeltaProcessor` missing `FastFloorToInt` and double/float conversion errors.

Phi Gain:
- Before this patch on current tree: `H-Phi_static_narrow = 0.000842629`, `H-Phi_static_risk = 0.000009935`.
- After this patch: `H-Phi_static_narrow = 0.000844101`, `H-Phi_static_risk = 0.000009953`.
- Delta: narrow `+0.000001472`, risk `+0.000000018`.
- Compared with the original dialogue baseline `0.00062`: current narrow score is about `+36.15%`.
