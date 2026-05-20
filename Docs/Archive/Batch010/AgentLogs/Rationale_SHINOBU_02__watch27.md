# SHINOBU_02 Rationale

Date: 2026-05-20
Domain: Core & Memory Infrastructure / Global EventBus MPSC SignalBus
Status: BLOCKED BY EXTERNAL GENERATED-PROJECT WALL. SHINOBU-owned compile hits are absent from the latest Core build log.

## Decision 01 - R35 Documentation Boundary

Problem: `Docs/PROJECT_ATLAS.md` and `Docs/ARCHITECTURE/HECTON8_P0_FOUNDATION_PROOF_MATRIX.md` still implied pending or stronger R35 validation than the current disk evidence supports.

Solution: Updated the wording to the verified boundary: the static/tool validation tuple exists, AtlasCheck still reports red, and Unity/runtime proof is not available.

Rejected Alternatives: Leaving stale wording would create false confidence. Claiming Unity/runtime validation would be a fake report because no Unity import, Play Mode, IL2CPP, or profiler run was executed.

Scalability potential: Documentation now separates static evidence from runtime evidence, so weak-device and high-end validation are not conflated.

Hardware Impact: 0 measured microseconds. This is documentation correctness only.

Evidence: Edited `Docs/PROJECT_ATLAS.md` and `Docs/ARCHITECTURE/HECTON8_P0_FOUNDATION_PROOF_MATRIX.md`.

## Decision 02 - Generated Project Protection

Problem: `Hecton8.Core.csproj` is generated and previously carried stale binary/source identity conflicts plus stale cross-domain inclusions.

Solution: Patched only `Directory.Build.targets` so the generated project remains untouched. Added source-backed Core contract files, removed stale binary `Hecton8.Core.Bucketing`, and excluded known non-Core runtime/editor sources from the Core overlay.

Rejected Alternatives: Editing generated `*.csproj` files would be overwritten by Unity and would create churn. Pulling sibling runtime domains into Core to satisfy compile errors would make a compile wall and violate domain boundaries.

Scalability potential: Lower rebuild blast radius on low-end development machines; no direct runtime frame impact. High-tier devices gain nothing directly, but compile isolation preserves iteration speed for visual systems.

Hardware Impact: 0 measured frame microseconds. Compile-time savings are expected but not measured; no fake number is claimed.

Evidence: `DIRECTORY_BUILD_COMPILE_INCLUDE_MISSING=0`; latest guarded Core build has no SHINOBU-owned hits for `GlobalSignals`, `SystemDispatcher`, `SimulationBucketing`, `JobAdmission`, or Kinetic bridge symbols.

## Decision 03 - Kinetic Presentation Decoupling

Problem: `PlayerSwimPresentationController` referenced the concrete Animation runtime type, forcing Gameplay to know Animation implementation details and risking sibling-domain compile coupling.

Solution: Added `IKineticCharacterPresentationSink` to Core contracts and changed Gameplay to cache that interface. The Animation runtime implements the interface. Hot publish uses cached interface calls; cold resolution uses `TryGetComponent` only outside the hot path.

Rejected Alternatives: A direct Animation assembly reference was rejected because it violates the compile-wall rule. Reflection and string event routing were rejected because they add runtime fragility and allocation risk.

Scalability potential: Low-tier devices avoid hot-path component lookup. High/Ultra tiers can receive richer presentation data through the same interface without gameplay truth coupling.

Hardware Impact: Exact profiler saving not measured. Expected saving is removal of repeated concrete dependency resolution from swim presentation publication; 0 microseconds are claimed as measured.

Evidence: `Assets/_Project/Scripts/Core/Contracts/CoreContractsAssemblyMarker.cs`, `Assets/_Project/Scripts/Gameplay/PlayerSwimPresentationController.cs`, `Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorRuntime.cs`.

## Decision 04 - Equipment Signal Ambiguity

Problem: Including equipment thermal battery contracts introduced a `ToolDepletedSignal` name collision between Gameplay and Tools namespaces.

Solution: Qualified HUD explicit interface and handler signatures with `Hecton8.Gameplay.ToolDepletedSignal`.

Rejected Alternatives: Removing the Tools contract from Core would hide a real contract identity problem. Renaming either signal would be a wider API change outside this pass.

Scalability potential: No runtime performance change. Compile identity is explicit and deterministic.

Hardware Impact: 0 measured microseconds.

Evidence: `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`.

## Decision 05 - GlobalSignals Compile Defects

Problem: `GlobalSignals` passed a `NativeArray` indexer result as `in`, which is illegal because the indexer returns a value copy. Weather compatibility path also referenced a missing quality byte encoder.

Solution: Copied the snapshot entry to a local before comparison and added a local `EncodeSignalQualityWeightByte(float)` helper.

Rejected Alternatives: Removing the compatibility path would break stale generated consumers. Boxing or managed comparer paths were rejected because this is signal infrastructure.

Scalability potential: Keeps deterministic snapshot logic allocation-free. Low-tier devices preserve cheap byte signal compatibility; high-tier systems can still consume normalized float quality where available.

Hardware Impact: Exact microseconds not measured. Expected gain is compile recovery and no GC introduction; 0 measured microseconds claimed.

Evidence: Latest build log has no `GlobalSignals` errors.

## Decision 06 - Byte Compatibility Bridge For Stale Contracts

Problem: The generated-project build graph still referenced older `IJobAdmissionController` and `ISimulationBucketer` method shapes that consumed byte quality, while current standards require continuous `GlobalQualityWeight`.

Solution: Encoded the continuous quality once in `SystemDispatcher` only at the stale boundary. Added a byte overload in `SimulationBucketingContracts` and `ModuloSimulationBucketer` that immediately normalizes back to 0..1 before policy math.

Rejected Alternatives: Changing all domain consumers in one pass was rejected because it would expand outside SHINOBU_02 and risk a wider compile wall. Leaving the callsite broken would block Core compile diagnostics.

Scalability potential: Low/Middle/High/Ultra policy still flows through a continuous normalized float inside the bucketer. The byte bridge is compatibility debt at the generated-contract boundary and should be removed when stale binary references are eliminated.

Hardware Impact: One byte encode per dispatch frame; exact cost not measured and expected to be below profiler noise. 0 measured microseconds claimed.

Evidence: `Assets/_Project/Scripts/Core/SystemDispatcher.cs`, `Assets/_Project/Scripts/Core/Contracts/SimulationBucketingContracts.cs`, `Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs`; latest build log has no `SystemDispatcher`, `SimulationBucketing`, `ModuloSimulationBucketer`, or `JobAdmission` hits.

## Decision 07 - External Generated-Project Wall Classification

Problem: Latest guarded Core build still fails with 315 errors / 19 warnings after SHINOBU-owned compile errors were cleared.

Solution: Classified remaining errors as external to the SHINOBU_02 Core/Signal patch set instead of pulling runtime domains into Core. Examples include PDA data vault binary/source identity mismatch, Fabrication, Babel, Ballistics, Tether/Cable, Fauna/World partials, VR comfort partials, Visor unsafe/buffer signatures, Core binary manifest missing DTOs, and `DispatcherJobFence` consumers.

Rejected Alternatives: Importing every referenced runtime file into Core was rejected as architectural sabotage. Editing generated project files directly was rejected. Claiming compile complete was rejected.

Scalability potential: Compile isolation protects low-end developer machines from dependency explosions. Runtime scalability cannot be proven until external owners clear their compile walls.

Hardware Impact: 0 measured runtime microseconds. Build remains red, so runtime/profiler proof is unavailable.

Evidence: `Docs/AgentLogs/CoreBuild_SHINOBU_02_current40.txt`; search for SHINOBU-owned symbols returned no hits.

## Verification Ledger

- Build guard before latest compile: `CPU=42`, build tool processes `0`.
- Latest guarded command: `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -v:minimal /p:UseSharedCompilation=false /p:BuildInParallel=false /m:1`.
- Latest guarded result: failed, `315 Error(s)`, `19 Warning(s)`.
- SHINOBU-owned error pattern search in latest build log: no hits for `GlobalSignals`, `SystemDispatcher`, `PlayerSwimPresentationController`, `KineticCharacter`, `SuitHUDV4CanvasOverlay`, `ModuloSimulationBucketer`, `SimulationBucketing`, or `JobAdmission`.
- Exact scoped runtime `Pack=1` scan over Core and touched files: only cold `ContentAssetBinaryRecord` in `Assets/_Project/Scripts/Core/Content/ContentAssetHashMap.cs`.
- `git diff --check` over touched files: exit 0, CRLF conversion warnings only.
