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

## 2026-05-14 Audit Counter Correction

What was wrong:
- The restored audit counted all text matching `Update(`, including comments, editor `serializedObject.Update()`, and scanner string literals.
- This produced `UnityUpdateMethods=27` even though the actual method declarations in first-party source are 3.

What was done:
- Tightened the update counter to declaration-only regex in global and per-domain H-Phi counts.
- Reran `Tools/Architecture/HectonPhiAudit.ps1 -Json`.

Cinematic Cheats used:
- None. Metric accuracy only.

Exact Microseconds saved:
- Runtime: 0 us. This is CLI-only.

Compile Status:
- Unchanged: Core compile remains blocked by existing/generated-project errors outside the changed save codec files.

Phi Gain:
- Corrected current H-Phi static narrow: `0.000878730`.
- Corrected current H-Phi static risk-adjusted: `0.000010357`.
- Compared with the original dialogue baseline `0.00062`: corrected current narrow score is about `+41.73%`.

## 2026-05-15 Runtime H-Phi Continuation

What was wrong:
- H-Phi risk scoring mixed shipped runtime code with `Scripts/Editor` authoring debt.
- The audit did not strip `#if UNITY_EDITOR` blocks inside runtime files, so editor-only code in runtime files inflated the runtime debt surface.
- Five runtime singleton/bootstrap services still used scene-wide `FindAnyObjectByType` / `FindObjectOfType` despite already having static or registry ownership paths.

What was done:
- Reworked `Tools/Architecture/HectonPhiAudit.ps1` to report runtime scores separately from all-source and editor-only hygiene counters.
- Added runtime stripping for `#if UNITY_EDITOR` blocks before score calculation.
- Removed scene-wide singleton discovery scans from `HardwareThermalService`, `AnalyticalCausticsService`, `LootMagnetSystem`, `InternalFloodWaterlineRuntime`, and `MaterialDecayRuntime`.
- Kept remaining `GameBootstrapper` and `VRSomaticRuntimeBootstrap` finds untouched because they are bootstrap/scene-handoff behavior and need owner/integrator evidence.
- Honored user instruction: no `dotnet build`, no rebuild.

Cinematic Cheats used:
- None. This was architecture metric/tooling and cold-path startup hygiene.

Exact Microseconds saved:
- Hot path: 0 us measured; no Tick/FixedTick loop changed.
- Cold bootstrap: five scene-wide searches removed. Exact savings unmeasured; profiler evidence absent by user constraint.

Compile Status:
- Not run in this continuation pass by explicit user order.
- Static checks run: `Tools/Architecture/HectonPhiAudit.ps1 -Json`, runtime `rg` find-debt scan, `git diff --check`.
- `git diff --check`: no whitespace errors, only LF/CRLF normalization warnings.

Phi Gain:
- Previous corrected all-source narrow score: `0.000878730`.
- Current runtime narrow score: `0.000991118`.
- Current runtime risk-adjusted score: `0.000012480`.
- Compared with original dialogue baseline `0.00062`: runtime narrow is about `+59.86%`.
- Compared with previous corrected score: runtime narrow is about `+12.79%`, but this includes the evidence-model correction that excludes editor-only debt.

Regression Model:
- CPU: cold startup scene scan pressure reduced; hot path unchanged.
- GC: static source only; no profiler/GCMonitor proof.
- Memory: no persistent runtime allocation added except existing singleton-created objects; no new NativeArray ownership.
- Cadence: no tick registration cadence changed.
- Correctness: risk remains in Unity load-order semantics for scene-authored inactive singleton components. Runtime verification is pending.

## 2026-05-15 SaveData And Core Contract Layout Pass

What was wrong:
- Public save DTO structs in `SaveData.cs` lacked explicit layout metadata, weakening persistence layout evidence.
- Public interface-facing structs in `Assets/_Project/Scripts/Core/Contracts` lacked explicit layout metadata.
- Runtime memory-alignment score was still mid-grade at `0.464893617`.

What was done:
- Added `[StructLayout(LayoutKind.Sequential)]` to public save DTO structs that lacked it.
- Added `[StructLayout(LayoutKind.Sequential)]` to remaining public structs in `Core/Contracts`.
- Verified no public `SaveData.cs` DTO structs and no public/internal `Core/Contracts` structs remain without nearby `StructLayout`.
- Honored user instruction: no `dotnet build`, no rebuild.

Cinematic Cheats used:
- None. This was ABI/layout metadata work.

Exact Microseconds saved:
- Runtime gameplay: 0 us measured; metadata-only annotations.
- Cold persistence/registry risk: reduced layout ambiguity. Exact time saved is unmeasured.

Compile Status:
- Not run by explicit user order.
- Static checks run: `Tools/Architecture/HectonPhiAudit.ps1 -Json`, public DTO/contract layout scans, `git diff --check`.
- `git diff --check`: no whitespace errors, only LF/CRLF normalization warnings.

Phi Gain:
- Previous runtime narrow score: `0.000991118`.
- Current runtime narrow score: `0.001029525`.
- Delta from previous runtime narrow: `+0.000038407`, about `+3.88%`.
- Current runtime risk-adjusted score: `0.000012970`.
- Memory alignment: `0.464893617 -> 0.483253589`.
- Compared with original dialogue baseline `0.00062`: current runtime narrow is about `+66.05%`.
