# Unity Signal Residual Contract Cleanup - UNKNOWN - 2026-05-27

Evidence class: static source plus existing `SignalBusContractAuditCli` binary recheck. No Unity import, Play Mode, profiler, GC, player build, or full-solution compile proof is claimed.

## What Was Wrong

- `PersistentWorldRegistry.RunSectorOverrideCommitAsync()` used `List<T>.ToArray()` to snapshot sector override commit work. That allocated a managed array on a scheduled runtime route.
- `TetherTensionSignal` was configured as cache-line-critical while its payload is 192 bytes. The payload carries two AUP endpoints and tension state; forcing it into the critical class was the wrong contract.
- Three clean local DTO/sandbox names duplicated signal-like names across unrelated domains:
  - `ModdingAPI.MockAcousticSignal` vs VFX `MockAcousticSignal`
  - UI `MockDepthSignal` vs Audio `MockDepthSignal`
  - Graphics/Culling `MockQualityWeightSignal` vs `Core.Contracts.MockQualityWeightSignal`

## What Was Done

- Replaced the sector override `ToArray()` snapshot with a cold-owned fixed `SectorOverrideCommitWork[16]` buffer and bounded commit pass.
- Changed `TetherTensionSignal` lane setup from `ConfigureCacheLineCritical(128)` to normal `Configure(128, maxFrameSignals: 128, lowTierFrameSignals: 32)`.
- Renamed only clean, owner-local DTO/sandbox carriers:
  - `SandboxMockAcousticSignal`
  - `GlitchMockDepthSignal`
  - `TBDRMockQualityWeightSignal`
- Did not edit dirty `HectonFluidEngine.cs`.
- Did not touch full project compile errors; another agent owns that wall.

## Proof

- Final audit: `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_TBDR_UI_SANDBOX_TETHER_SECTOR_RECHECK.json`
  - `errors=0`
  - `confirmedErrors=0`
  - `warnings=148`
  - `infos=1169`
  - `CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT=0`
  - `ZERO_GC_HOT_PATH_ENUMERATION_REVIEW=0`
  - `DUPLICATE_SIGNAL_LIKE_NAME_REVIEW=8`
- Previous final layout audit was `warnings=155`, `infos=1171`, `CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT=1`, `ZERO_GC_HOT_PATH_ENUMERATION_REVIEW=1`, `DUPLICATE_SIGNAL_LIKE_NAME_REVIEW=14`.
- Scoped `git diff --check` on touched source files passed with line-ending warnings only.
- Brace-balance spot checks returned `0` deltas for touched source files.
- Documentation gates after removing raw audit markdown bloat:
  - `VerifyDocStructure.py`: `pass=true`, `activeDocCount=693`, `encodingWithoutUtf8Sig=0`
  - `OOP_Doc_Scanner.py`: `finalPass=true`, `activeFileCount=693`, `sourceSyncPass=true`, `wordReductionPercent=31.34291167072261`

## Residual Debt

- Remaining duplicate signal-like names are in cross-domain telemetry owners: ocean/fluid, structural, atmosphere, and thermal. One side of the ocean pair is dirty under concurrent work.
- `LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT=3` remains: one mod projection ring and two TBDR graphics/culling rings. They have sentinel coverage, but still need owner-by-owner Vault/blackbox route decisions.
- `RUNTIME_SYNC_FILE_IO_REVIEW=65` and `SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW=69` remain review debt, not fixed in this pass.

Runtime microseconds saved: `0` claimed. The value is contract hygiene and allocation-risk removal without profiler proof.
