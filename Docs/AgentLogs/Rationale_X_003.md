# Rationale_X_003

Status: PARTIAL PASS - CORE CONCRETE SIBLING REFS CUT 15->0 / PRODUCT RUNTIME SIBLING REFS 91 CURRENT / STATIC CRITICAL SOURCE USING-FQN ZERO / DIRECT PLAYER CONCRETE CAST ZERO / AI-PHYSICS-PHYSIOLOGY CAST ZERO / CONCRETE CAST FINDINGS 1151->1049 / RT LIFECYCLE+POOL CONTRACT SLICE COMPLETE / REBUILD GUARD CLOSED
Evidence boundary: STATIC_SOURCE / STATIC_DOC plus latest guarded compile eligibility checks. Loop 31 moved pure brine constants/math into Core.Contracts, moved `FluidImpulseJob` and `MacroSwarmTravelJob` into Core-owned runtime namespaces, replaced Core consumers of fluids/CCD/determinism with contract math, removed the empty AI migration asmdef, and removed four Core concrete sibling asmdef refs. Loop 32 promoted `AcousticEcholocationRayHit` to Core.Contracts, moved inventory defrag/corrosion jobs into Core-owned inventory source, deleted two empty inventory job asmdefs, and removed a stale `FaunaBrain` physics import. Loop 33 collapsed the remaining Core concrete refs (`Audio.Virtualization`, `Audio.Propagation`, `Animation.IK`, `Cartography`, `Logistics`, `Logistics.Grid`, `World.Terrain`, `Input`), aligned generated CLI project files, and removed the stale `Hecton8.Input.csproj` route. Loop 34 promoted `UniversalInputStateSignal` into Core.Contracts and removed the `Hecton8.UI.VR` -> `Hecton8.Input.Universal` edge. Loop 35 removed two manually verified unused asmdef refs: `Hecton8.UI.Diegetic -> Hecton8.Core` and `Hecton8.Graphics.Caustics -> Hecton8.Core.Memory`. Loop 36 replaced a reintroduced `FaunaBrain` physics determinism route with Core signal reads, cut two unused asmdef references plus one dev runtime harness edge, made direct-player audit rows explicit, converted PDA/progression/exploration player checks to interfaces, and moved `PlayerKinematicsRuntime` off concrete movement/motor casts. Loop 37 fixed test-assembly runtime classification and moved atmosphere/fluid/vegetation consumers onto narrow read-model routes. Latest full-project assembly audit pass: 167 asmdefs, 0 cycles, 91 product-runtime concrete sibling refs, `autoReferencedFalse=167`, Core concrete sibling refs 0, unresolved first-party refs 0. Latest source audit: critical `using` findings 0, critical FQN findings 0, concrete casts 1065, global direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0, and AI/Physics/Physiology direct player concrete coupling 0. Cable selected blast radius is 98->3 and no longer reaches UI/audio; metabolism remains radius 2 and no UI/audio. New build was not launched because the latest guard sample was CPU 63.3% with active `dotnet`; last green Core build remains Loop 39 and does not cover Loop 41 edits. Unity import/Console/PlayMode proof is absent.

Current Loop 42-43 evidence: full-project assembly audit reports 167 asmdefs, 393 edges, 0 cycles, 92 product-runtime concrete sibling refs, first-party `autoReferenced=false` 167/167, Core concrete sibling refs 0, unresolved first-party refs 0, and Core.Contracts boundary violations 119. Compile-wall audit reports concrete casts 1057, critical source `using` 0, critical FQN 0, direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0, AI/Physics/Physiology direct player concrete coupling 0, and hot-path lookup 0. Build was not launched after Loop 43 because the latest guard sample was CPU 74.9% with an active `dotnet` process.

Current Loop 44-45 evidence: `AssemblyDependencyAudit.py` reports 167 asmdefs, 393 edges, 0 cycles, 91 product-runtime concrete sibling refs, `autoReferenced=false` 167/167, Core concrete sibling refs 0, unresolved first-party refs 0, and Core.Contracts boundary violations 119. `CompileWallX003Audit.py` reports concrete casts 1058, critical source `using` 0, critical FQN 0, direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0, AI/Physics/Physiology direct player concrete coupling 0, and hot-path lookup 0. First guarded Core build passed with 0 errors and 5 warnings in 00:01:17.09, then X_003 removed the new duplicate-source include; rebuild retry was blocked by CPU 90.1%.

Current Loop 46 evidence: `AssemblyDependencyAudit.py` reports 167 asmdefs, 0 cycles, 91 product-runtime concrete sibling refs, `autoReferenced=false` 167/167, Core concrete sibling refs 0, and unresolved first-party refs 0. `CompileWallX003Audit.py` reports concrete casts 1049, critical source `using` 0, critical FQN 0, direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0, AI/Physics/Physiology direct player concrete coupling 0, and hot-path lookup 0. Scoped `git diff --check` passed with CRLF warnings only. New build was not launched because CPU/process guard stayed closed.

## Decision 000 - Mission Boundary

Problem: X_003 must reduce compile-wall coupling without mutating runtime authority blindly in a 20+ agent workspace.
Solution: Start with graph and source evidence, then edit only proven assembly or contract boundaries. Use Contracts, cold cached interfaces, typed SignalBus payloads, or GlobalDataVault handles as allowed routes.
Rejected Alternatives: Direct sibling references, concrete casts, `#pragma` suppression, catch-all event buses, and moving runtime logic into contracts. These preserve compile pressure or create hidden authority routes.
Scalability potential: Low uses static graph gates and fewer recompiles; Middle/High/Ultra gain faster iteration without changing runtime truth ownership.
Hardware Impact: Expected runtime impact is neutral until runtime code changes. Editor CPU impact is reduced only after asmdef blast radius shrinks; no microsecond runtime claim without profiler proof on i3/MX350.

## Decision 001 - Static Graph Gate First

Problem: The prompt demands asmdef decoupling, but the workspace is heavily dirty and multiple agents are editing source/asmdefs concurrently.
Solution: Add `Tools/CompileWallX003Audit.py` and run the existing `Tools/AssemblyDependencyAudit.py` into X_003-owned artifacts before structural edits. After scope correction to `Assets/_Project`, the graph found 178 asmdefs, 423 edges, 0 cycles, 116 runtime concrete sibling references, 0 unresolved first-party refs, and 2,207 using-boundary violations.
Rejected Alternatives: Directly deleting `Hecton8.Core.asmdef` references. That would convert known source coupling into compile breakage with no replacement route.
Scalability potential: Low/Middle/High/Ultra all benefit from a fail-fast static gate because build churn is exposed before Unity import. It does not change gameplay truth or quality tiers.
Hardware Impact: 0 runtime microseconds. Editor CPU benefit is only potential until asmdef cuts happen; current selected Core gameplay files still have 98-assembly static blast radius.

## Decision 002 - No Blind Contract Moves

Problem: The top extraction candidates are shared but not uniformly pure: `IDataVault`, `BufferID`, `VaultGenerationHandle`, hot-swap interfaces, and `IPlayerRuntimeContext` are public, broad, and/or concrete Unity/player facades.
Solution: Record the census and block physical movement until legacy wrappers and pure DTO/interface splits are defined. Existing interface mutation in `Hecton8.Core.Contracts` is forbidden during active batch unless wrappered.
Rejected Alternatives: Moving runtime logic, concrete player object properties, managed Unity object references, or mutable vault owner APIs into `Hecton8.Core.Contracts`.
Scalability potential: Low keeps contract assembly small; Middle/High/Ultra avoid bloating hot shared APIs with visual-overkill concerns.
Hardware Impact: 0 runtime microseconds. Avoided creating a larger shared rebuild nucleus and avoided managed payload drift in contracts.

## Decision 003 - EndingSystem Hot Poll Cleanup

Problem: `EndingSystem.SlowTick()` polled `GlobalRegistry.AtlasSignal` and `GlobalRegistry.Quest`, violating cold dependency cache rules.
Solution: Add cached `_atlasSignal` and `_questRuntime` fields, fill them during `OnEnable`, refresh them through `IGlobalRegistryHotSwapListener`, and read cached fields inside `SlowTick()`.
Rejected Alternatives: Leaving per-SlowTick registry reads, or adding a new signal lane for one private condition check.
Scalability potential: Low avoids registry traffic in slow cadence; Middle/High/Ultra preserve the same gameplay truth while keeping presentation/quest activation logic cached.
Hardware Impact: Static estimate removes two registry property reads per SlowTick. Exact microseconds on i3/MX350 are unmeasured; profiler proof absent.

## Decision 004 - Sibling Ref Amputation Block

Problem: `Hecton8.Core.asmdef` directly references 17 concrete sibling runtime assemblies and current full-project source has 2,207 cross-domain using violations. The monolithic root `Hecton8.Core` owns many gameplay/world/audio/UI files that would need their own domain assemblies and contracts.
Solution: Fail the sibling-reference gate intentionally and document a phased migration instead of deleting refs. The next safe phase is to carve root domain folders into assemblies only after their concrete references are replaced by contracts/vault/signal routes.
Rejected Alternatives: Pretending zero unauthorized refs, or removing asmdef references while Core source still imports `Hecton8.Physics`, `Hecton8.World`, `Hecton8.Audio`, `Hecton8.UI`, and other sibling domains.
Scalability potential: Low benefits once root gameplay/UI/audio files stop rebuilding Core; High/Ultra get faster iteration for visual-overkill domains without touching physics/AI compile units.
Hardware Impact: Runtime unchanged. Editor compile-wall remains high: selected Core-owned files still produce 98-assembly static blast radius.

## Decision 005 - Compile Verification Scope

Problem: X_003 touched `EndingSystem.cs` inside `Hecton8.Core.csproj`, but the project is Unity-generated and the wider workspace contains known cross-domain assembly debt.
Solution: Wait until CPU and compiler-process guards were clean, then run `dotnet build Hecton8.Core.csproj --no-restore` only for the changed runtime assembly. Result: build succeeded, 0 warnings, 0 errors, 00:02:03.32.
Rejected Alternatives: Full solution rebuild or Unity import cycle during multi-agent work. That would measure unrelated dirty state and risk colliding with other compilers.
Scalability potential: Low/Middle/High/Ultra all get the only relevant compile proof for this slice without turning verification into a whole-project contention point.
Hardware Impact: 0 runtime microseconds. Editor proof confirms X_003's edited assembly compiles; compile-wall debt remains documented at 116 runtime sibling refs under full-project scope.

## Decision 006 - Disable Residual First-Party Auto References

Problem: The rerun request demanded auto-reference cleanup, and audit still found two first-party assemblies with `autoReferenced=true`: `Hecton8.Lighting.Editor` and `Hecton8.InventoryRouting.Editor`.
Solution: Set both Editor asmdefs to `autoReferenced=false` and rerun the full-project assembly audit. Result: `autoReferencedFalse=178`, meaning every first-party asmdef is explicitly referenced only.
Rejected Alternatives: Leaving Editor tools implicitly visible to predefined assemblies. That preserves hidden compile fan-out and makes source ownership harder to prove.
Scalability potential: Low/Middle/High/Ultra benefit in editor iteration because optional tooling no longer leaks through implicit reference lanes. Runtime gameplay truth is unchanged.
Hardware Impact: 0 runtime microseconds. Compile graph clarity improves; exact editor compile microseconds are unmeasured.

## Decision 007 - Audit Scope Correction

Problem: The previous `Assets/_Project/Scripts` scan reported one unresolved first-party reference because `Hecton8.Input.Generated.asmdef` lives in `Assets/_Project/Input`, outside the Scripts root.
Solution: Rerun `AssemblyDependencyAudit.py` with `--source-root Assets/_Project` and update `CompileWallX003Audit.py` to scan the same root. Result: 178 asmdefs, 423 edges, 0 unresolved first-party refs, 0 cycles, and 116 runtime concrete sibling refs.
Rejected Alternatives: Removing the valid `Hecton8.Input.Generated` reference from `Hecton8.Input.asmdef`. That would break the real generated input assembly route.
Scalability potential: Low/Middle/High/Ultra benefit from a graph that covers generated input/domain editor assemblies instead of hiding them outside the report.
Hardware Impact: 0 runtime microseconds. Compile-wall metrics are now stricter and less flattering, which is required for honest migration planning.

## Decision 008 - Dead Core Edge Removal

Problem: `Hecton8.Core.asmdef` contained zero-hit first-party refs that made unrelated assemblies part of Core's compile pressure.
Solution: Removed only refs with no Core-owned source `using` or fully-qualified hits: `Hecton8.Bootstrap.Contracts`, `Hecton8.World.Contracts`, `Hecton8.Environment.Fluids.Contracts`, `Hecton8.Habitat.Deformation.Contracts`, and `Hecton8.UI.Localization`.
Rejected Alternatives: Removing live refs such as `Hecton8.Physics.CCD`, `Hecton8.AI.Cognition`, `Hecton8.Input`, or `Hecton8.Audio.Virtualization`; source still imports those namespaces, so deleting refs would create compiler errors.
Scalability potential: Low/Middle/High/Ultra benefit from less hidden Core fan-out. The remaining wall requires moving Core-owned gameplay/physics/audio/UI files into real domain assemblies or replacing their calls with contracts/signals/vault handles.
Hardware Impact: 0 runtime microseconds. Static compile graph changed: edges 423->418, Core refs 45->40, Core first-party refs 32->27, Core concrete sibling refs 17->16, total runtime concrete sibling refs 116->115.

## Decision 009 - Seaglide Concrete Physics Cast Removal

Problem: `SeaglideHydrodynamicsRuntime` cached `GlobalRegistry.Physics as PhysicsApplySystem`, a concrete class cast behind an interface slot.
Solution: Store `IPhysicsService` and change `DrainSeaglideForcePackets` to accept `IPhysicsService`; it now calls the public `QueueForce` route, whose implementation still uses Critical priority by default.
Rejected Alternatives: Keeping the cast, adding a second concrete GlobalRegistry slot, or expanding contracts with a seaglide-specific service before compile proof.
Scalability potential: Low/Middle/High/Ultra preserve the same force queue behavior while removing one concrete coupling point from physics runtime.
Hardware Impact: 0 runtime microseconds claimed. Static scan now reports AI/Physics/Physiology direct player concrete coupling count 0; full compile is pending because active dotnet processes remain present after the CPU guard dropped from 100% to 21%.

## Decision 010 - AcousticEchoTap Contract Promotion

Problem: The source-level audit found `Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs` importing `Hecton8.Audio.Virtualization` only to consume the unmanaged `AcousticEchoTap` transit payload. That is a source-domain AI->Audio coupling even while the file is masked by the root `Hecton8.Core` asmdef.
Solution: Move the 144-byte `AcousticEchoTap` DTO to `Hecton8.Core.Contracts`, remove the audio-owned DTO copy, and remove `using Hecton8.Audio.Virtualization` from `AcousticEchoLocationRuntime`. The DTO keeps explicit layout and uses a byte flag field so it does not pull audio enum ownership into Core.Contracts.
Rejected Alternatives: Keeping the AI sensory import, leaving duplicate DTO owners, or moving audio runtime interfaces wholesale into Core.Contracts. The first preserves hidden source coupling; the second violates one-fact-one-owner; the third would bloat the contract nucleus with broad audio service APIs before wrapper design.
Scalability potential: Low/Middle/High/Ultra share the same fixed transit ABI. Low-tier does not pay for audio-domain recompiles for sensory DTO shape; higher tiers can still add acoustic fidelity through producers without changing the sensory contract layout.
Hardware Impact: 0 runtime microseconds claimed. Static source coupling improved by one AssemblyDependencyAudit using-boundary violation: 2209->2208. The asmdef blast radius did not shrink because `Hecton8.Core` still has live audio virtualization service dependencies through `GlobalRegistry`.

## Decision 011 - Path-Domain Source Audit

Problem: The root `Hecton8.Core.asmdef` covers folders such as `Scripts/AI/Sensory` and `Scripts/Physics/Seaglide`, so asmdef-derived source domains can hide source-level AI/Physics/UI/Audio imports as `Hecton8.Core`.
Solution: Change `Tools/CompileWallX003Audit.py` so source-domain scans infer domain from `Assets/_Project/Scripts/<Domain>` path ownership while keeping asmdef ownership for blast-radius graph metrics.
Rejected Alternatives: Trusting asmdef ownership only. That under-reports the exact class of hidden source coupling requested by the APEX override.
Scalability potential: Low/Middle/High/Ultra get stricter CI evidence before compile-wall changes are made. It changes no runtime logic and no quality-tier behavior.
Hardware Impact: 0 runtime microseconds. Latest source-domain audit reports 470 cross-domain source edges, 3,374 cross-domain `using` directives, and 0 critical AI/Physics/UI/Audio findings after the DTO move.

## Decision 012 - Latest Compile Guard And Coverage Boundary

Problem: The latest static pass touched Core.Contracts, Audio.Virtualization.Contracts, AI sensory, seaglide, and asmdefs, but AGENTS.md forbids `dotnet build` when CPU is above 50% or any `dotnet`/`csc` process is running. After the guard opened, the generated project set still covered only `Hecton8.Core.csproj`.
Solution: Rechecked the guard, waited until CPU was 28% with no active `dotnet`/`csc`, then ran `dotnet build Hecton8.Core.csproj --no-restore`. Result: PASS, 0 warnings, 0 errors, 00:01:18.12. Coverage check with `rg` found only `HectonSignalLaneContract.cs` among the latest DTO/AI/audio/seaglide touched files in the generated csproj.
Rejected Alternatives: Running `dotnet build` while the guard was closed, or claiming domain compile proof from a generated project that omits `AcousticEchoLocationRuntime.cs`, `AudioVirtualizationContracts.cs`, `SeaglideHydrodynamicsRuntime.cs`, and `PhysicsApplySystem.SeaglideQueue.cs`.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime. Multi-agent editor stability is preserved by not contending with active compilers.
Hardware Impact: 0 runtime microseconds. Latest Core compile proof covers `Core.Contracts` and root Core files only; broader Unity/domain compile proof requires generated project regeneration or Unity import.

## Decision 013 - Critical Cast Gate Cleanup

Problem: The APEX override required an explicit check for hidden casts and interface-to-concrete leaks. After widening `CompileWallX003Audit.py` to include C# `(Type)` casts, the AI/Physics/Physiology critical lane had 7 findings: six `BufferID` value casts and one vehicle command enum cast. None were interface-to-concrete player casts, but they polluted the gate and hid real debt.
Solution: Replace the critical-lane BufferID casts with existing `BufferID.ShinobuMetabolismStates` and `BufferID.Shinobu274RadiationStates` enum members, and replace the physics vehicle command enum cast with a byte-mask check against the typed signal flag. Re-run result: total concrete cast pattern findings 1559->1552, AI/Physics/Physiology concrete cast findings 7->0, AI/Physics/Physiology direct player concrete coupling findings 0.
Rejected Alternatives: Moving `BufferID` into `Hecton8.Core.Contracts` during this pass, adding duplicate contract-owned buffer enums, or hiding the value casts with analyzer exclusions. Those would either expand the shared rebuild nucleus or make the audit less useful.
Scalability potential: Low/Middle/High/Ultra get cleaner compile-wall evidence without changing gameplay truth, DTO layout, or quality scaling.
Hardware Impact: 0 runtime microseconds claimed. `HydrodynamicKccRuntime.cs` and `SubmarineDynamicsRuntime.cs` compiled under `Hecton8.Core.csproj`; physiology files still need Unity-generated project coverage.

## Decision 014 - Cable Solver Non-Move

Problem: `CablePhysicsSolver132.cs` remains in `Hecton8.Core` with 98-assembly blast radius and reaches UI/audio. `TetherManager` directly calls `CablePhysicsSolver132`, `CableNodeFlags132`, and `TetherTelemetryEntry`, so moving only the solver into a new physics asmdef would force Core to reference that new assembly and preserve the reverse closure.
Solution: Block the asmdef move until the owner bridge is designed: TetherManager must communicate through a contract/service or vault/signal descriptor route instead of direct static calls. Report the unsolved blast radius instead of faking a compile-wall win.
Rejected Alternatives: A blind file move, new Core->Physics asmdef reference, or duplicate DTO types. All preserve or worsen the wall and violate one fact/one owner.
Scalability potential: Low/Middle/High/Ultra require the same authority route; only after the bridge exists can cable physics edits stop pulling UI/audio through Core.
Hardware Impact: 0 runtime microseconds. Editor compile wall remains objectively high for cable: radius 98, direct inbound 92, UI=true, audio=true.

## Decision 015 - Namespace-Domain Audit Correction

Problem: `Scripts/Fauna/*` declares `namespace Hecton8.AI` while living outside a local AI asmdef, so a path-only or asmdef-only audit can mask AI-owned source as Core/Fauna and miss AI->Physics imports.
Solution: Update `Tools/CompileWallX003Audit.py` to classify source by declared Hecton8 namespace with path fallback. Re-run source-domain audit after code changes.
Rejected Alternatives: Keep folder-only classification or manually exclude Fauna. That would produce flattering but false critical-source metrics.
Scalability potential: Low/Middle/High/Ultra get stricter CI evidence without changing gameplay truth or quality tiers.
Hardware Impact: 0 runtime microseconds. Static audit now reports 586 cross-domain source edges, 3,619 directives, and 0 critical AI/Physics/UI/Audio using findings.

## Decision 016 - KinematicStateDTO Contract Promotion

Problem: AI steering read KCC kinematic state through `Hecton8.Physics.KCC.KinematicStateDTO`, creating a source-level AI->Physics dependency for a pure 64-byte unmanaged transit row.
Solution: Move `KinematicStateDTO` to `Hecton8.Core.Contracts.Physics`, update KCC/Fauna/editor consumers, and remove AI/Fauna `using Hecton8.Physics` / `using Hecton8.Physics.KCC`.
Rejected Alternatives: Keep the KCC-owned DTO, duplicate the struct in AI, or move KCC behavior into contracts. The first preserves the wall; the second violates one fact/one owner; the third bloats the contract assembly with behavior.
Scalability potential: Low/Middle/High/Ultra share one fixed ABI for GlobalDataVault kinematic rows while KCC implementation remains physics-owned.
Hardware Impact: 0 runtime microseconds. Compile-wall source coupling reduced; selected asmdef blast-radius metrics remain unchanged because Core/AI graph edges are still live.

## Decision 017 - AI Audio And Force Routing

Problem: `FaunaBrain` emitted procedural audio through `Hecton8.Audio.ProceduralAudioEvents`, applied forces through `PhysicsForceRouter`, and fell back to direct `HectonPlayerHealth` lookup if combat queueing failed.
Solution: Replace predator audio calls with `SignalBus<AudioEvent>` payloads, cache `IPhysicsService` via GlobalRegistry hot-swap, and remove the direct `HectonPlayerHealth` fallback so bites route through `CombatDamageRuntime` registration.
Rejected Alternatives: Keep legacy managed audio bridge calls or fully-qualified physics router calls after removing `using` directives. That would hide coupling cosmetically but leave the real dependency.
Scalability potential: Low-tier keeps bounded signal payloads and cached service reads; High/Ultra can enrich audio/force consumers without changing AI source ownership.
Hardware Impact: 0 runtime microseconds claimed. Static proof: critical AI/Physics/UI/Audio source imports 0 and AI/Physics/Physiology direct player concrete coupling 0.

## Decision 018 - Remaining Concrete Cast Debt Is Not Cleared

Problem: After stricter namespace classification and string/value-cast filtering, the AI/Physics/Physiology lane still has 49 concrete class/service cast findings, mostly legacy service caches and same-domain object tests.
Solution: Record the residual debt explicitly and avoid claiming total cast elimination. Direct player concrete coupling in the critical lane is zero; broader concrete service cast burn-down requires per-owner interfaces or DTO/signal routes.
Rejected Alternatives: Analyzer exclusions, broad ignore lists, or blind edits across FaunaDirector/FaunaBrain/HectonFluidEngine service caches. Those would either hide debt or break active multi-agent work.
Scalability potential: Low/Middle/High/Ultra benefit only after the remaining service caches become interfaces. Current change improves proof accuracy and removes the most dangerous player/physics/audio routes.
Hardware Impact: 0 runtime microseconds. Runtime profile unchanged; compile proof is pending because active `dotnet`/`csc` and CPU 100% blocked the build guard.

## Decision 019 - Fully-Qualified Source Reference Audit

Problem: The APEX override specifically asked for hidden source dependencies, not only `using` directives. A file can bypass the using audit by writing `Hecton8.Physics.SomeType` inline.
Solution: Extend `Tools/CompileWallX003Audit.py` with `sourceReferenceDomainAudit`, stripping string literals and skipping namespace/using declarations. It now reports cross-domain fully-qualified references separately from `using` edges and applies the same AI/Physics/UI/Audio critical rule. First run exposed 6 critical FQN findings in `FaunaDirector` and 1 stale Physics->AI `using` in `GlobalPhysicsStateManager`; after edits the rerun reports 0 critical `using` findings and 0 critical FQN findings.
Rejected Alternatives: Trusting `rg "using Hecton8.Physics"` or broad text grep. That misses fully-qualified code references and overcounts strings/editor text.
Scalability potential: Low/Middle/High/Ultra benefit from a stricter static gate that prevents source-level compile-wall debt from being hidden behind syntactic style.
Hardware Impact: 0 runtime microseconds. Editor compile-wall proof improved; no runtime path changed by the Python audit itself.

## Decision 020 - FaunaDirector Acoustic Route Decoupling

Problem: `FaunaDirector` was an AI-domain source file implementing `Hecton8.Physics.IAcousticPingEventListener` and registering directly with `Hecton8.Physics.PhysicsEventBus`. This is a hidden AI->Physics compile dependency even without a `using Hecton8.Physics` line.
Solution: Remove the physics listener interface and direct PhysicsEventBus registration. `FaunaDirector` now cold-initializes `SignalBus<AcousticPingSignal>` and consumes each new snapshot generation into its existing bounded acoustic panic ring.
Rejected Alternatives: Adding `using Hecton8.Physics` for readability, keeping fully-qualified physics types, or adding a new private event lane. Those preserve the compile leak or create a single-use signal surface.
Scalability potential: Low consumes the already bounded `AcousticPingSignal` lane and the existing 8-command local ring; Middle/High/Ultra can enrich acoustic producers without AI depending on physics event bus internals.
Hardware Impact: 0 runtime microseconds claimed. Runtime behavior moved from callback registration to snapshot consumption; no profiler sample exists. Static compile proof: critical FQN findings 6->0 and critical source using findings 1->0 in the requested lane.

## Decision 021 - Latest Compile Guard Block

Problem: Latest edited files are included in `Hecton8.Core.csproj`, but AGENTS.md forbids launching `dotnet build` when CPU is above 50% or any `dotnet`/`csc` process is active.
Solution: Rechecked guard before build. CPU samples initially hit 89-100% and active `csc`/`dotnet` appeared, so no compile was launched. After CPU dropped to 27-34% and no compiler process was active, ran `dotnet build Hecton8.Core.csproj --no-restore`; result: PASS, 0 warnings, 0 errors, 00:03:08.88.
Rejected Alternatives: Running build during the closed guard, or reporting the previous Core build as proof for the new FaunaDirector route edit.
Scalability potential: Low/Middle/High/Ultra unchanged; this is multi-agent workstation hygiene.
Hardware Impact: 0 runtime microseconds. Latest edited Core assembly has CLI_COMPILE proof; Unity import/runtime proof remains absent.

## Decision 022 - Contract CCD And Material Metadata Bridge

Problem: Removing `using Hecton8.Physics` from `FaunaBrain` exposed direct fully-qualified physics calls to `KinematicCcdMath`, `IPhysicsImpactMaterialProvider`, `GlobalPhysicsStateManager`, `CurrentVolume`, and `HectonContactJob`.
Solution: Move pure CCD math into `Hecton8.Core.Contracts.Physics.KinematicCcdContractMath`, make physics `KinematicCcdMath` a facade over the contract math, add contract `IImpactMaterialProvider` with the legacy physics interface deriving from it, and replace AI wall-slide projection with local pure math. Direct physics telemetry call was removed; the public impact fact remains the typed `HighSpeedImpactSignal`.
Rejected Alternatives: Re-adding `using Hecton8.Physics`, leaving fully-qualified physics calls, or adding a direct AI->Physics asmdef route. Those hide or preserve the compile wall.
Scalability potential: Low/Middle/High/Ultra share one contract math implementation. Low avoids physics-domain rebuilds for AI lunge code; High/Ultra can enrich physics consumers without changing AI source ownership.
Hardware Impact: 0 runtime microseconds claimed. Static proof: AI/Fauna direct `Hecton8.Physics.*` grep is clean and critical FQN findings remain 0.

## Decision 023 - Ambient Current Read Model

Problem: `FaunaBrain.ApplyAmbientCurrentDrift` sampled `Hecton8.Physics.CurrentVolume` directly, binding AI to the physics authored-current owner.
Solution: Add `IAmbientCurrentReadModel` to `GlobalRegistryContracts`, route it through `GlobalRegistry.TryGet<T>` to `FluidRuntime`, implement it in `HectonFluidEngine`, and cache it in `FaunaBrain` through cold registry refresh/hot-swap.
Rejected Alternatives: Direct `GlobalRegistry.Fluid` concrete reads, `CurrentVolume` static access from AI, or scene searches. Those violate cold DI and source-domain boundaries.
Scalability potential: Low can sample the same authored current through a cached interface; Middle/High/Ultra can replace the backing flow implementation without recompiling AI.
Hardware Impact: Runtime cost is one cached interface call where a static call existed; no profiler claim. Compile proof: AI source no longer references `Hecton8.Physics.CurrentVolume`.

## Decision 024 - Latest Compile Block Is Foreign

Problem: The guarded Core build after X_003 fixes first failed on a missing local audio helper, then after repairing that helper failed on unrelated UI/Power edits: `_materialBufferBound` in `PDADecryptionSpectrogramPanel` and missing Jacobi/delta-pass fields/types in `ShinobuLogisticsRouter`.
Solution: Restored the missing `ResolvePriorityBitIndex` helper in `VocalWarningSystem` because the local method already existed only inside a nested helper and the fix was unambiguous. Stopped before reconstructing the Power/UI refactor and marked latest CLI compile as `[BLOCKED BY DEPENDENCY]`.
Rejected Alternatives: Claiming the old green Core build as current proof, editing broad Power/UI algorithms from X_003 without ownership context, or reverting user/other-agent work.
Scalability potential: Low/Middle/High/Ultra unaffected by the report decision. The build must be made green by the Power/UI owners or integrator before a new compile proof can be claimed.
Hardware Impact: 0 runtime microseconds. Build evidence: X_003-caused Fauna/CCD/current-volume/unsafe errors are gone; latest compile stops are outside X_003's dependency-decoupling slice.

## Decision 025 - Generated Project Include For Signal Bridge Routes

Problem: After concurrent UI/Power edits changed the failure surface, guarded build attempt 3 stopped in `GlobalSignals.cs` because `SurvivalSignalRoute`, `AupSignalRoute`, `CraftingSignalRoute`, and `SimulationSignalRoute` were unresolved. `rg` showed those route owners exist in `Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs`, but `Hecton8.Core.csproj` did not compile that file.
Solution: Add `Assets\_Project\Scripts\Core\Signals\SignalBridgeRoutes.cs` to `Hecton8.Core.csproj` beside the other Core signal files. This is generated-project hygiene only; no signal route logic was changed.
Rejected Alternatives: Duplicating route classes in `GlobalSignals.cs`, reverting the untracked route file, or claiming a foreign compile block after the missing include was objectively identified.
Scalability potential: Low/Middle/High/Ultra unchanged at runtime. Editor compile proof becomes less stale once the generated project matches the source tree.
Hardware Impact: 0 runtime microseconds. Rebuild is pending because the AGENTS guard stayed closed with active `dotnet/csc` for 5 minutes after the include patch.

## Decision 026 - Interface Facade Burn-Down For AI/Physics Service Casts

Problem: The rerun APEX audit still showed 49 AI/Physics/Physiology concrete cast findings. The highest-signal findings were not player casts; they were service-owner casts in `FaunaBrain`, `FaunaDirector`, and `SubmarineFluidDynamics` to pool, hazard, atmosphere, micro-fauna, thermodynamics, and ecosystem owner classes. These casts keep AI/physics source coupled to concrete implementation classes even when `using`/FQN critical gates are clean.
Solution: Added narrow read/write facades in `GlobalRegistryContracts`: `IObjectPoolService`, `IAtmosphereReadModel`, `IMicroFaunaPresentationPulseSink`, plus extensions to `IHazardZoneReadModel`, `IThermodynamicsService`, and `IEcosystemDirectorService`. Existing owners implement the interfaces: `ObjectPoolManager`, `HectonAtmosphereManager`, `HazardZoneManager`, `SargassumMicroFaunaBoids`, `AbyssalThermalManager`, and `EcosystemDirector`. `GlobalRegistry` now resolves the new facades. `FaunaBrain`, `FaunaDirector`, and `SubmarineFluidDynamics` consume the interfaces instead of the owner classes.
Rejected Alternatives: Blindly moving owner classes into `Hecton8.Core.Contracts`, casting through `object`, analyzer suppressions, or deleting asmdef refs before replacing call sites. Those options either widen the shared rebuild nucleus, hide coupling, or break compile.
Scalability potential: Low uses cached facades and unchanged bounded buffers; Middle keeps deterministic owner routes; High and Ultra can add richer presentation pulses, thermal sampling, and spawn policy inside the owners without recompiling AI/physics consumers.
Hardware Impact: 0 runtime microseconds claimed; no profiler sample. Static editor evidence improved: total concrete cast findings 1314->1305, AI/Physics/Physiology concrete cast findings 49->40, direct player concrete coupling in that lane remains 0. Latest guarded `dotnet build Hecton8.Core.csproj --no-restore` passed in 00:00:46.15 with 0 warnings and 0 errors.

## Decision 027 - Read-Model Facade Burn-Down For Fluid, Terrain, Celestial, And Brine Routes

Problem: After Decision 026, the APEX rerun still had 40 AI/Physics/Physiology concrete cast findings. The next safe cluster was concrete read-model access across fluid/celestial/brine/terrain/dynamic-resolution routes: `HectonFluidEngine`, `HectonCelestialEngine`, `ResourceDistributionDirector`, `MapMagicBridge`, and `DynamicResolutionScaler` owner-class caches. These are not direct player casts, but they keep source coupled to concrete domain owners.
Solution: Added narrow contract routes in `GlobalRegistryContracts`: `IAnalyticalFlowReadModel`, `ICelestialSkyDirectionReadModel`, `IBrineFluidDensityReadModel.TrySampleBrineLayer`, and `ITerrainProvider.TryGetBiomeIndex`. Existing owners implement or expose those interfaces: `HectonFluidEngine`, `HectonCelestialEngine`, `ResourceDistributionDirector`, and `MapMagicBridge`. `GlobalRegistry` maps analytical flow and celestial sky direction through existing runtime slots. `FaunaDirector`, `SubmarineFluidDynamics`, `HectonFluidEngine`, and `GlobalPhysicsStateManager` now consume the interfaces instead of owner-class casts.
Rejected Alternatives: Moving fluid/world/celestial owner classes into `Hecton8.Core.Contracts`, severing asmdef references before replacing source call sites, or adding broad service interfaces that expose whole MonoBehaviours. Those would either widen the shared rebuild nucleus, break compile, or preserve the same concrete dependency under a new name.
Scalability potential: Low uses cached deterministic read models and existing bounded data paths; Middle keeps terrain/brine/fluid facts behind owner-owned snapshots; High and Ultra can enrich fluid and celestial presentation internally without recompiling AI/physics consumers.
Hardware Impact: 0 runtime microseconds claimed; no profiler sample. Static editor evidence improved: total concrete cast findings 1305->1297, AI/Physics/Physiology concrete cast findings 40->32, AI/Physics/Physiology direct player concrete coupling remains 0, critical source `using` findings remain 0, and critical fully-qualified findings remain 0. Latest guarded `dotnet build Hecton8.Core.csproj --no-restore` passed in 00:01:11.13 with 1 duplicate-source warning and 0 errors.

## Decision 028 - Ecosystem, Terrain, Depth, Biome, And Drag Facade Burn-Down

Problem: The APEX rerun still had 32 AI/Physics/Physiology concrete cast findings. The next safe cluster was concrete owner routing through `EcosystemDirector`, `HectonMapMagicVegetationBridge`, `DepthZoneDirector`, `WorldProceduralFieldSampler`, and `SargassumGlobalDragManager`. These were not direct player casts, but they kept AI/fluid code tied to concrete domain owners and kept source-level coupling visible.
Solution: Added narrow contract routes in `GlobalRegistryContracts`: `ITerrainHeightSampleReadModel`, `IVegetationThreatReadModel`, `IVegetationThreatPulseSink`, `IBiomePhysicsInfluenceReadModel`, `ISargassumDragReadModel`, and `IDepthZoneReadModel`. Existing owners implement the interfaces. `GlobalRegistry` exposes and resolves the routes through existing slots. `FaunaBrain`, `FaunaDirector`, and `HectonFluidEngine` now consume interfaces for corpse/scavenge/ecology behavior, terrain height payloads, vegetation threat pulse/weight, depth zone readout, biome buoyancy influence, and sargassum drag.
Rejected Alternatives: Moving world/physics owner MonoBehaviours into contracts, deleting `.asmdef` references before replacing source call sites, casting through `object`, or claiming the existing Core gravity well was solved. Those options hide the wall or break compile authority.
Scalability potential: Low keeps cached interface routes and existing owner-owned buffers; Middle keeps deterministic DTO/read-model ownership; High and Ultra can enrich vegetation, biome, fluid, and ecosystem internals without forcing AI/fluid consumers to recompile against concrete owner classes.
Hardware Impact: 0 runtime microseconds claimed; no profiler sample. Static editor evidence improved: total concrete cast findings 1297->1292, AI/Physics/Physiology concrete cast findings 32->24, AI/Physics/Physiology direct player concrete coupling remains 0, critical source `using` findings remain 0, and critical fully-qualified findings remain 0. Latest guarded Core build did not produce a pass: it stopped on unchanged signal split files before X_003-edited files; a second build attempt was blocked by CPU 62.2% and active `dotnet/csc`.

## Decision 029 - Fauna Contact And Sensory Interface Burn-Down

Problem: After Decision 028, the remaining high-signal AI lane still used concrete object tests for fauna spatial contacts, bait pickups, flare distractors, player bleeding signals, and noise receivers. These were not direct player-parameter casts, but they kept AI sensory code bound to concrete owner classes.
Solution: Added narrow routes `IFaunaSpatialContact`, `IFaunaBaitSource`, `IFaunaDistractorSignalSource`, and `IPlayerBleedingReadModel`, plus `IFaunaNoiseSignalReceiver` in the AI noise lane. Existing owners implement the interfaces: `FaunaBrain`, `PickupItem`, `DeployableFlare`, and `HectonSurvivalSystem`. `FaunaBrain`, `FaunaSensorSuite`, and `NoiseSystem` now consume interfaces instead of concrete owner checks for those contact paths.
Rejected Alternatives: Moving concrete gameplay/interaction/player owners into contracts, using analyzer suppressions, or deleting live `.asmdef` refs before replacing call sites. Those would hide the wall, widen the shared nucleus, or break compile.
Scalability potential: Low keeps the current spatial hash and bounded signal paths; Middle keeps deterministic owner-owned facts; High and Ultra can enrich fauna sensory behavior without recompiling consumers against concrete player/item/flair owner classes.
Hardware Impact: 0 runtime microseconds claimed; no profiler sample. Static editor evidence improved: total concrete cast findings 1292->1271, AI/Physics/Physiology concrete cast findings 24->2, AI/Physics/Physiology direct player concrete coupling remains 0, critical source `using` findings remain 0, and critical fully-qualified findings remain 0. The remaining two critical-lane findings are Unity component checks (`ParticleSystem`, `ParticleSystemRenderer`), not direct player/domain owner casts. Latest Core build passed with 2 unrelated `CS0168` unused-variable warnings, 0 errors, 00:01:17.46.

## Decision 030 - Alpha Leviathan Contract Extraction

Problem: `Hecton8.Core.asmdef` still referenced `Hecton8.AI.Cognition` only because Core-owned Fauna/World code read pure Alpha Leviathan DTOs and byte flags from the AI cognition runtime assembly. That made edits inside `UtilityAICognitionVault.cs` or `ShinobuApexBrainVault.cs` reverse-reach Core/UI/audio despite the payloads being unmanaged transit contracts.
Solution: Moved `AlphaLeviathanCognitionContracts.cs` and `AlphaLeviathanStalkContracts.cs` with their `.meta` files into `Assets/_Project/Scripts/Core/Contracts/AI`, changed their namespace to `Hecton8.Core.Contracts.AI.Cognition`, updated AI runtime/Fauna/World consumers, and removed `Hecton8.AI.Cognition` from `Hecton8.Core.asmdef`. The stale generated `Hecton8.Core.csproj` was updated to include the moved contract files for guarded CLI coverage.
Rejected Alternatives: Keeping the Core->AI runtime edge, duplicating Alpha Leviathan DTOs, or moving cognition jobs into contracts. Keeping the edge preserved the wall; duplication violated one fact/one owner; moving jobs would bloat contracts with behavior.
Scalability potential: Low keeps the contract assembly limited to fixed unmanaged DTO/flag rows; Middle keeps GlobalDataVault ABI stable; High/Ultra can evolve cognition jobs and visual-overkill steering internally without recompiling Core/UI/audio consumers.
Hardware Impact: Runtime 0 microseconds claimed; only namespace/assembly ownership changed. Static editor impact: asmdef edges 418->417, Core refs 40->39, Core first-party refs 27->26, Core concrete sibling refs 16->15, runtime concrete sibling refs 115->114. Selected AI cognition blast radius changed 99->2 and UI/audio reach changed true->false. Cable physics remains unsolved at radius 98, UI=true, audio=true.

## Decision 031 - Cable 132 Service Bridge And Assembly Extraction

Problem: `CablePhysicsSolver132.cs` was a Core-owned physics implementation with static calls from `TetherManager`. That made a cable solver edit behave like a Core edit: radius 98, direct inbound 92, UI=true, audio=true.
Solution: Move `CablePhysicsSolver132.cs` and `CablePhysicsDebugGizmo132.cs` with `.meta` files into `Assets/_Project/Scripts/Physics/Cable132`, add `Hecton8.Physics.Cable132.asmdef` with `autoReferenced=false`, and introduce `ICablePhysics132Service` in `GlobalRegistryContracts`. `CablePhysics132Service` wraps the solver, registers through `GlobalRegistry.CablePhysics132Runtime`, and `TetherManager` calls the cached interface instead of concrete solver/static flag types. `VerletCableLayout` and `VerletCableSimdMath` were made public because they are pure layout/math support already owned by Core DTO infrastructure. `Hecton8.Editor.asmdef` now explicitly references the cable assembly for the tuner window.
Rejected Alternatives: Adding `Hecton8.Physics.Cable132` as a Core asmdef reference, duplicating cable DTOs, moving tether/winch/player MonoBehaviours in the same pass, or using reflection. A Core->Cable reference would preserve the reverse closure; DTO duplication violates one fact/one owner; a broad component move is unsafe in the active multi-agent workspace.
Scalability potential: Low keeps the same bounded GlobalDataVault buffers and quality-weighted mock cable path. Middle keeps deterministic owner-published cable rows. High and Ultra can increase spline/solver presentation inside the cable assembly without recompiling UI/audio consumers.
Hardware Impact: Runtime 0 microseconds claimed; no gameplay path was profiled. Static editor impact: selected cable file moved from `Hecton8.Core` radius 98/direct inbound 92/UI=true/audio=true to `Hecton8.Physics.Cable132` radius 3/direct inbound 1/UI=false/audio=false, a 95-assembly and 91-direct-inbound reduction. Honest debt: project-wide runtime concrete sibling refs increased 114->116 because the new cable assembly explicitly depends on `Hecton8.Core` and `Hecton8.Core.Memory`; root Core still has 15 concrete sibling refs. Build proof is pending because latest five guard samples stayed above the CPU limit: 66.3%, 75.2%, 99.8%, 99.4%, 98.4%.

## Decision 032 - Native Input Concrete Surface Contraction

Problem: UI/rebinding/gameplay files still reached the concrete `Hecton8.Input.InputManager` through direct imports, static helper calls, and `GlobalRegistry.NativeInputManager`. That kept non-input consumers coupled to the input runtime implementation even after the asmdef graph was cycle-free.
Solution: Expand `INativeInputManagerRuntime` with the existing rebind/display/action-map/persistence surface and implement those members in `InputManager`. Convert `RebindingManager`, `PDAControlsRebindUI`, `PauseControlsPanel`, debug overlays, PDA/fabricator/interaction consumers, gameplay verifiers, and related runtime files to `GlobalRegistry.NativeInputRuntime` plus interface events. Remove `GlobalRegistry.NativeInputManager`; `GameBootstrapper` now performs the only concrete `InputManager` access because it owns validation and component creation.
Rejected Alternatives: Leaving UI panels on concrete `InputManager`, wrapping static binding helpers with more Core-side casts, or moving the concrete input MonoBehaviour into contracts. Those preserve the source wall, hide a cast behind a different helper, or pollute the contract assembly with a Unity owner.
Scalability potential: Low keeps the same cached input/action buffers and no new hot-path allocations. Middle keeps deterministic input snapshots through the existing dispatcher. High and Ultra can extend input display/persistence internals without recompiling UI/rebinding consumers against the concrete owner.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static editor evidence improved: total concrete cast findings 1206->1203 after the final GlobalRegistry cleanup, `using Hecton8.Input` remains only in `GameBootstrapper`, critical source using findings remain 0, critical FQN findings remain 0, and AI/Physics/Physiology direct player concrete coupling remains 0. Build proof is pending because the latest guard stayed closed at CPU 99-100% with active `dotnet`/`VBCSCompiler`.

## Decision 033 - Fauna Predation Target Contract And Engine Leaf Filter

Problem: The rerun APEX audit still had 2 AI/Physics/Physiology concrete-cast findings. One was real source debt: `FaunaBrain` resolved attack targets through concrete `FaunaBrain` component lookups and then called private implementation methods. The other was `SubmarineStructuralGrid` reading Unity's `ParticleSystemRenderer` immediately after creating a pooled spark `ParticleSystem`; that is an engine component leaf, not a domain-owner cast.
Solution: Added `IFaunaPredationTarget` as a narrow route over the existing fauna spatial contact contract, implemented it on `FaunaBrain`, and replaced `TryGetComponent/GetComponentInParent<FaunaBrain>` predation lookups with a parent-walking interface resolver. Predation damage, biolum prey checks, apex-retreat forcing, and health checks now move through the contract. Added `ParticleSystemRenderer` to the concrete-cast audit ignore set because it is a Unity renderer component created and configured inside the same physics owner.
Rejected Alternatives: Suppressing the `FaunaBrain` finding, moving `FaunaBrain` into contracts, exposing the whole controller through a broad service, or deleting the particle spark renderer setup. Those options hide coupling, poison the contract nucleus, or remove an owner-local visual path for no compile-wall gain.
Scalability potential: Low keeps the same bounded predation and spark behavior with no new allocation path. Middle keeps deterministic fauna ownership behind a narrow interface. High and Ultra can enrich fauna predation or spark presentation inside owners without recompiling consumers against concrete AI controller types.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static editor evidence improved: total concrete cast findings 1203->1201, AI/Physics/Physiology concrete cast findings 2->0, AI/Physics/Physiology direct player concrete coupling remains 0, critical source `using` findings remain 0, and critical fully-qualified findings remain 0. Latest build was not launched because AGENTS guard stayed closed with active `dotnet` and CPU samples 59.8%, 99.8%, 100%.

## Decision 034 - Physics Velocity Set Service Route And Audio Concrete Check Burn-Down

Problem: The stricter FQN audit exposed 19 AI-owned direct calls to `Hecton8.Physics.PhysicsForceRouter.QueueLinearVelocitySet/QueueAngularVelocitySet`. `PlayerCriticalProceduralAudioRenderer` also used concrete `FaunaBrain` and `SubmarineStructuralGrid` checks for predator sonar and structural fatigue audio.
Solution: Added `QueueLinearVelocitySet` and `QueueAngularVelocitySet` to `IPhysicsService` and implemented them in `PhysicsApplySystem`. `FaunaBrain`, `FaunaBrain.Foveated`, `FaunaSteeringEngine`, `FaunaSimplifiedRagdollHandoff`, and `FaunaDirector` now use cached `IPhysicsService` routes instead of fully-qualified physics statics. Audio predator checks now use `IFaunaSpatialContact.IsLeviathanContact`, and structural fatigue/transient impact audio reads public values from `ISubmarineHullBreachReadModel`. The guarded Core build also exposed an unrelated illegal `RuntimeOriginRoute` overload pair differing only by `ref/out`; the unused `out` overloads were removed.
Rejected Alternatives: Leaving the fully-qualified physics static calls, adding `using Hecton8.Physics` back to AI for cosmetic cleanup, or suppressing the FQN audit. Those preserve the compile wall. Moving all acoustic impulse and submarine breach contracts to a new contract assembly was deferred because it is broader API surgery than needed to close the current FQN breach.
Scalability potential: Low keeps the same deferred physics packet path and bounded audio reads. Middle keeps the physics owner authoritative for velocity assignment. High and Ultra can extend physics packet internals, fauna steering, or structural audio presentation without AI/audio code reaching concrete owner classes for these paths.
Hardware Impact: Runtime 0 microseconds claimed; the same deferred force packet queue is used. Static editor evidence improved: critical fully-qualified findings returned 19->0, concrete cast findings 1201->1198, AI/Physics/Physiology concrete cast findings remain 0, and direct player concrete coupling in that lane remains 0. Guarded Core build was attempted and failed on `RuntimeOriginRoute`; X_003 fixed that blocker, but rebuild was blocked by CPU samples 86.3%, 71.1%, 65.9%, then 94.4%, 79.3%, 75.1%.

## Decision 035 - Player Concrete Fallback Burn-Down

Problem: The global direct-player concrete gate still contained fallback casts/lookups outside the already-clean AI/Physics/Physiology lane. Several were not authority requirements: `PlayerRuntimeContextService` cast services back to concrete managers, interaction modules scraped player inventory/tool owners from the interactor hierarchy, `BaseModule` cached `HectonPlayerMovement` for environment commands, `VehicleDockingModule` used concrete transport owners/coordinators for dock lock, and `EcosystemHealthDirector` read explored chunk keys through `PlayerExplorationTracker`.
Solution: Replace only the narrow safe surfaces with contract routes. `PlayerRuntimeContextService` now reads `IPlayerInventoryService` and `IPlayerSensoryService` directly. `BatteryChargerModule`, `MaintenanceStationModule`, and `ResourceRecyclerModule` use `IPlayerRuntimeContext`/`IPlayerInventoryService`. `BaseModule` uses the existing `IPlayerMovementEnvironmentSink` for gravity and a Core-owned `IPlayerHypoxiaPresentationSink` for CO2 distortion, so it no longer caches `HectonPlayerMovement`. `ITransportDockControlLock` and `IPlayerTransportLifecycleResolver` keep docking off `MountablePlayerTransport`/`PlayerTransportCoordinator` concretes. `IPlayerExplorationChunkReadModel` exposes the one PDA exploration read needed by `EcosystemHealthDirector`.
Rejected Alternatives: Moving player managers, transport coordinators, or PDA trackers into `Hecton8.Core.Contracts`; suppressing the audit; or deleting bootstrap concrete slots. Those options either poison the shared contract nucleus, hide real debt, or break owner bootstrap. `PlayerBuilder` and bootstrap-only concrete routes remain recorded because their current public contracts still expose concrete Unity owners and need a separate builder/read-model split.
Scalability potential: Low keeps the same cached service reads and no scene-search fallback in interaction paths. Middle keeps player, transport, and PDA facts owner-published. High and Ultra can enrich movement presentation, dock behavior, and cartography internals without recompiling these consumers against concrete player owner classes.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample and behavior routes are equivalent cold/service calls. Static editor evidence improved: concrete cast findings 1198->1189, global direct player concrete coupling 39->32, AI/Physics/Physiology concrete casts remain 0, AI/Physics/Physiology direct player concrete coupling remains 0, critical source `using` findings remain 0, and critical fully-qualified findings remain 0. Guarded rebuild was not launched because a `VBCSCompiler` process was active after CPU dropped to 30.5%, 13.6%, 25.9%.

## Decision 036 - Interaction Inventory And Transport Tail Burn-Down

Problem: After Decision 035, the global player-concrete gate still had safe fallback routes outside the AI/Physics/Physiology lane: reactor/charger interaction hierarchy scraping, outcrop and organic-drop direct `PlayerInventoryRuntime` reads, `PlayerBuilder` recovery casts from `CurrentTool`, and a bootstrap `NativeInputRuntime as InputManager` cast.
Solution: Routed `BioReactor` and `BatteryCharger` through cached `IPlayerRuntimeContext`; routed `HarvestableOutcrop`, `DestructibleOrganicManager`, `ScrapManager`, `PlayerActionController`, `ResourceNode`, `SuitUpgradeManager`, `LootMagnetSystem`, and `ModRuntimeState` through `IPlayerInventoryService`; removed the `CurrentTool as PlayerBuilder` recovery path from `PlayerRuntimeContextService`/`PlayerInventoryManager`; added cold owner handles for `PlayerInventoryManager.ActiveRuntimeInstance` and `InputManager.ActiveRuntimeInstance` so bootstrap/service creation does not downcast through interface slots.
Rejected Alternatives: Moving `PlayerInventory`, `PlayerToolManager`, or `InputManager` into `Hecton8.Core.Contracts`; suppressing the audit; replacing player inventory calls with scene searches; or deleting bootstrap owner creation. Those would widen the shared rebuild nucleus, hide the debt, or break existing authority ownership.
Scalability potential: Low keeps the same owner-published services and no hierarchy scans in interaction paths; Middle keeps inventory/tool/input truth behind owner services; High and Ultra can enrich inventory, input, and interaction presentation without recompiling consumers against concrete player managers for these paths.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static editor evidence improved: concrete cast findings 1189->1173, global direct player concrete coupling 32->15, AI/Physics/Physiology concrete casts remain 0, AI/Physics/Physiology direct player coupling remains 0, critical source `using` findings remain 0, and critical fully-qualified findings remain 0. Guarded rebuild was not launched because CPU samples were 97.7%, 98.2%, 87.1% with active `csc`/`dotnet` processes.

## Decision 037 - Save Inventory Commit Sink

Problem: `SaveManager.NotifyMappedInventoryWritesCommitted()` still used a concrete `PlayerInventory` type test inside the save pipeline to call a mapped-inventory write callback.
Solution: Added `IMappedInventoryWriteCommitSink` as a narrow Core contract, implemented it on `PlayerInventory`, and changed `SaveManager` to call the interface through the existing `ISaveable` registry.
Rejected Alternatives: Keeping the concrete save dispatch, moving inventory save logic into `SaveManager`, or suppressing the player-concrete finding. Those preserve save-to-inventory implementation coupling or centralize inventory authority in the wrong owner.
Scalability potential: Low keeps the same callback with no allocation; Middle keeps persistence routing implementation-neutral; High and Ultra can change inventory internals without making the save pipeline depend on `PlayerInventory`.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static editor evidence improved: global direct player concrete coupling 15->14. Concrete cast finding total stayed 1173 because the interface type test is still counted as a generic pattern by the broad audit, but it is no longer a concrete player owner cast.

## Decision 038 - Seat Lock And Spatial Audio Player Contract Split

Problem: Interaction and audio presentation paths still reached concrete player owners: LifePod seat-lock wrote through `HectonPlayerMotor`, and SpatialAudio cached `HectonPlayerMovement` for delayed trauma, listener AUP, and underwater density.
Solution: Added `IPlayerSeatLockMotorSink`, exposed it through `GlobalRegistry.PlayerSeatLockMotor`, implemented it on `HectonPlayerMotor`, and moved LifePod commands onto that route. SpatialAudio now uses `IPlayerMovementTraumaSink` and `PlayerMovementRuntimeState`/`PlayerRuntimePoseSnapshot` DTOs instead of concrete movement access.
Rejected Alternatives: Keeping `GlobalRegistry.PlayerMotor` in interaction, casting `IPlayerRuntimeContext.PlayerMovement` back to `HectonPlayerMovement`, or widening `IPlayerRuntimeContext` with more concrete owner properties.
Scalability potential: Low/Middle/High/Ultra all keep the same deterministic owner phase. Low avoids extra scene lookups and concrete compile fan-out; High/Ultra can add richer audio/seat-lock presentation through the owner without UI/interaction recompiles.
Hardware Impact: Runtime microseconds saved: 0 claimed. Static source evidence improved concrete casts 1173->1167 and direct player coupling 14->12; profiler proof absent.

## Decision 039 - Spatial Audio Presentation Read Models

Problem: Acoustic radar UI, SignalBeacon, and RandomEventSystem were coupled to concrete `SpatialAudioManager` for impact samples, listener cave state, and meteor boom playback. That keeps UI/gameplay code tied to the audio owner type.
Solution: Added `SpatialAudioImpactEmitterSample`, `ISpatialAudioImpactEmitterReadModel`, `ISpatialAudioListenerCaveReadModel`, and `IMeteorShowerAudioSink`. `SpatialAudioManager` implements the contracts; UI and gameplay consumers cache only the interfaces and read player position through DTO snapshots.
Rejected Alternatives: Extending `IAudioService` with presentation-only APIs, preserving `SpatialAudioManager` casts, or routing these hot presentation samples through managed events.
Scalability potential: Low keeps radar/meter samples as fixed preallocated arrays; Middle/High/Ultra can raise sample capacity or fidelity behind the audio owner without changing UI compile ownership.
Hardware Impact: Runtime microseconds saved: 0 claimed. Static source evidence improved concrete casts 1167->1160. Assembly graph remains unchanged: 179 asmdefs, 0 cycles, 116 runtime concrete sibling refs.

## Decision 040 - Spatial Audio Runtime Surface Contracts

Problem: After Loop 29, runtime code outside audio/bootstrap/editor still reached concrete `SpatialAudioManager` for world-emitter samples, low-pass playback, eclipse/parasite modulation, SFX mixer group, narrative radio bit-crush, inventory runaway explosions, flora harvest/spore playback, and weather thunder playback.
Solution: Added narrow owner routes: `SpatialAudioActiveEmitterSample`, `ISpatialAudioWorldEmitterReadModel`, `ISpatialAudioLowPassPlayback`, `ISpatialAudioEnvironmentModulationSink`, `ISpatialAudioSfxMixerRouteReadModel`, `ISpatialAudioNarrativeRadioSink`, `ISpatialAudioInventoryRunawaySink`, `ISpatialAudioHarvestPlaybackSink`, and `ISpatialAudioWeatherPlaybackSink`. `SpatialAudioManager` implements the routes; consumers now cache only those interfaces. `AcousticZoneController`, `SpectrumSystem`, `PhysicalPanelButton`, `TraumaDispatcher`, `EclipseGameplaySystem`, `BaseModule`, `AudioLogSystem`, `PlayerThrusterAudio`, `PlayerInventory`, `DestructibleOrganicManager`, `HectonSurfaceWeatherDirector`, and `CelestialSyncSmokeTester` no longer type-test or store concrete `SpatialAudioManager`.
Rejected Alternatives: Widening `IAudioService` with every specialty method, keeping concrete casts because they are "just audio", or routing one-off calls through managed event buses. Widening the base service would turn optional presentation paths into mandatory service API; concrete casts preserve source fan-out; managed events add allocation/ordering risk.
Scalability potential: Low keeps the same fixed arrays and existing audio budgets. Middle keeps weather/harvest/narrative audio owner-authored. High and Ultra can expand DSP, spore, weather, or narrative coloration inside audio without recompiling UI/world/gameplay consumers against the concrete owner.
Hardware Impact: Runtime microseconds saved: 0 claimed; no profiler sample. Static evidence improved concrete cast findings 1160->1140 and direct player concrete findings 12->6 after the combined player/audio reroute pass. `rg` for runtime `SpatialAudioManager` concrete references outside bootstrap/editor/audio-owner code now returns comments only. Assembly graph remains 179 asmdefs, 0 cycles, 116 runtime concrete sibling refs.

## Decision 041 - Remaining Direct Player Findings Classification

Problem: The direct player concrete gate still reports 6 findings after Loop 30. Four are inside `PlayerKinematicsRuntime`, which owns and synchronizes same-player `HectonPlayerMovement`/`HectonPlayerMotor` components. Two are cold installers (`PDARuntimeInstaller` and `ProgressionRuntimeInstaller`) that add player-owned PDA/progression MonoBehaviours.
Solution: Do not hide these as green. They remain in the report as owner/bootstrap debt, not AI/Physics/Physiology cross-domain leakage. The next correct cut is a dedicated player-root composition/read-model split, not replacing `AddComponent<T>` with reflection or non-generic `Type` calls to fool the static grep.
Rejected Alternatives: Suppressing installer/player-owner rows, rewriting `TryGetComponent<T>` into `GetComponent(typeof(T))`, or moving concrete player owner classes into contracts. Those approaches either falsify the audit or poison the shared contract nucleus.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime. Keeping the rows visible prevents future work from confusing owner composition with decoupled domain traffic.
Hardware Impact: Runtime 0 microseconds. Editor evidence is honest: `directPlayerConcreteCouplingFindings=6`, `AI/Physics/PhysiologyDirectPlayerConcreteCoupling=0`, critical source using/FQN remain 0.

## Decision 042 - Compile Guard Closed For Loop 30

Problem: Loop 30 touched contracts and multiple runtime consumers, but AGENTS.md forbids launching `dotnet build` while CPU is above 50% or compiler processes are active.
Solution: Rechecked guard before build. CPU samples were 99.7%, 100%, and 100%; active compiler processes were `csc` and `dotnet`. Build was not launched. Static gates and `git diff --check` are the only current proof for Loop 30.
Rejected Alternatives: Running a build through active compiler contention, or claiming Loop 19's green Core build covers Loop 30 edits. Both would be false proof.
Scalability potential: Low/Middle/High/Ultra unaffected. The guard protects the multi-agent workspace from measuring somebody else's compile storm.
Hardware Impact: Runtime 0 microseconds. Verification boundary remains STATIC_SOURCE / CLI_STATIC_TOOL; Unity import, Console, PlayMode, profiler, GC, and player-build proof are absent.

## Decision 043 - Core Concrete Sibling Ref Amputation: Fluids, CCD, Determinism, Migration

Problem: `Hecton8.Core.asmdef` still referenced concrete sibling assemblies for AI migration, environment fluids, physics CCD, and physics determinism. Core source used those refs only for pure math/constants or small jobs, so the references made Core rebuild pressure larger than the authority route required.
Solution: Moved `BrineLayerConstants` and `BrineLayerMath` to `Hecton8.Core.Contracts.Fluids`, moved `FluidImpulseJob` into the Core-owned `Hecton8.Physics` source area, moved `MacroSwarm` into the Core-owned `Hecton8.World.MacroSwarmTravelJob`, added deterministic/CCD primitive math mirrors to `Hecton8.Core.Contracts.Physics`, replaced Core consumers with contract namespaces, removed the now-empty `Hecton8.AI.Ecology.Migration.asmdef`, and removed Core refs to `Hecton8.AI.Ecology.Migration`, `Hecton8.Environment.Fluids`, `Hecton8.Physics.CCD`, and `Hecton8.Physics.Determinism`.
Rejected Alternatives: Leaving Core dependent on concrete physics/fluid/AI assemblies for constants, moving Unity MonoBehaviour owners into contracts, duplicating brine/CCD constants in gameplay files, or suppressing the graph debt. Those options preserve the compile wall, poison the contract assembly with runtime owners, or violate one fact/one owner.
Scalability potential: Low keeps the same fixed primitive math and no hot allocations. Middle keeps gameplay/world/fluid facts routed through contracts without reintroducing direct concrete refs. High and Ultra can expand actual fluid/CCD/migration implementations inside their owner assemblies without forcing Core consumers through those concrete compile edges.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static editor evidence improved: Core concrete sibling refs 15->11, runtime concrete sibling refs 116->112, asmdefs 179->178 after deleting the empty migration asmdef, cycles remain 0, critical source using/FQN remain 0, AI/Physics/Physiology concrete casts remain 0. Build was not launched because CPU guard samples were 100%, 100%, 99% with no compiler processes.

## Decision 044 - Echolocation Ray-Hit Payload Promotion

Problem: `Hecton8.Core.asmdef` still referenced `Hecton8.Audio.Echolocation` for one blittable `AcousticEcholocationRayHit` payload. The raymarch implementation is an audio owner, but the payload is a fixed transit row used by Core-owned audio renderer code.
Solution: Moved `AcousticEcholocationRayHit` to `Assets/_Project/Scripts/Core/Contracts/Audio` while keeping its namespace stable, removed the duplicate struct from the echolocation runtime file, added the Core csproj include, and removed the Core asmdef reference to `Hecton8.Audio.Echolocation`.
Rejected Alternatives: Keeping Core dependent on the echolocation runtime assembly for a 56-byte DTO, moving the raymarch job into Core.Contracts, or duplicating the struct. Those either preserve the compile wall, put Burst behavior into the contract nucleus, or violate one fact/one owner.
Scalability potential: Low keeps the same fixed payload with no allocation. Middle/High/Ultra can alter echolocation raymarch internals without recompiling Core consumers against the concrete audio runtime assembly.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static editor evidence improved by one Core concrete sibling ref: 11->10 before the inventory pass, cycles stayed 0.

## Decision 045 - Inventory Job Assembly Collapse

Problem: `Hecton8.Core.asmdef` referenced `Hecton8.Inventory.Algorithms` and `Hecton8.Inventory.Corrosion` only because `PlayerInventory` used two isolated Burst jobs: `InventoryDefragJob` and `ItemSalinityCorrosionJob`. Each job assembly had one source file and no independent runtime owner.
Solution: Moved both job files and metas under Core-owned `Assets/_Project/Scripts/Inventory`, kept their namespaces stable for `PlayerInventory`, kept `Hecton8.Inventory.Corrosion.Contracts` as the only remaining corrosion contract dependency, deleted the empty algorithm/corrosion job asmdefs, and removed the two concrete refs from Core.
Rejected Alternatives: Leaving two one-file runtime assemblies referenced by Core, moving the jobs into Core.Contracts despite Burst attributes, or changing `PlayerInventory` logic during a compile-wall pass. Those choices preserve compile fan-out, pollute contracts with implementation jobs, or expand behavioral risk.
Scalability potential: Low keeps the same native SOA jobs and black-box behavior. Middle/High/Ultra can expand inventory internals inside the inventory owner without extra concrete asmdef edges from Core.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Final Loop 32 static evidence: 176 asmdefs, 424 DAG edges, 0 cycles, Core concrete sibling refs 8, runtime concrete sibling refs 110, critical source using/FQN 0, AI/Physics/Physiology concrete casts 0. Build was not launched because CPU guard samples were 100%, 100%, 100% with no compiler processes.

## Decision 046 - Core Concrete Sibling Ref Zero And CLI Ref Scrub

Problem: After Loop 32, `Hecton8.Core.asmdef` still referenced eight concrete sibling runtime assemblies: audio virtualization, audio propagation, animation IK, cartography, logistics, logistics grid, world terrain, and input. The Unity asmdef graph was cleaner than the CLI graph because `Directory.Build.targets`, `Hecton8.Core.csproj`, and `Hecton8.slnx` still contained stale references to moved/deleted assemblies and old source paths. Those stale DLL refs could reintroduce concrete dependencies during local `dotnet` builds even when Unity asmdefs were decoupled.
Solution: Moved only small Core-consumed implementation/job files into Core-owned source folders with `.meta` preservation, deleted the now-empty/orphan asmdefs, deleted stale generated `Hecton8.Input.csproj` after `Hecton8.Input.asmdef` removal, removed its `Hecton8.slnx` entry, removed the remaining concrete Core DLL refs from `Hecton8.Core.csproj` / `Directory.Build.targets`, and updated moved source paths in the targets file.
Rejected Alternatives: Leaving stale CLI references because Unity could regenerate later, adding replacement Core->domain references, moving behavior owners into contracts, or using reflection to hide input source ownership. Stale CLI references are false proof; replacement references preserve the compile wall; behavior in contracts pollutes the shared nucleus; reflection would hide, not solve, assembly ownership.
Scalability potential: Low avoids recompiling UI/audio/physics-adjacent domains for small Core-owned job/DTO edits. Middle keeps deterministic owner routes intact. High and Ultra can extend concrete domain implementations without Core depending on those domain assemblies for isolated helper jobs.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample and no runtime route was intentionally changed. Static editor evidence improved: Core concrete sibling refs 8->0 in Loop 33 and 15->0 across Loops 31-33; runtime concrete sibling refs 110->100 in Loop 33 and 116->100 across Loops 31-33; first-party asmdefs 176->168 in Loop 33. Build was not launched because `dotnet build Assembly-CSharp.csproj` and `csc.exe` were active.

## Decision 047 - Universal Input Payload Contract Promotion

Problem: `Hecton8.UI.VR` still referenced `Hecton8.Input.Universal` for a single unmanaged `UniversalInputStateSignal` struct. That is a concrete UI->Input assembly edge for a pure transit payload.
Solution: Moved `UniversalInputStateSignal` into `Hecton8.Core.Contracts.Signals`, updated `OpenXRManualOverrideLever`, added the file to the stale Core CLI include paths, removed the `Hecton8.UI.VR` asmdef reference to `Hecton8.Input.Universal`, and deleted the empty input-universal asmdef. The gate also caught a reintroduced `using Hecton8.Physics` in `FaunaBrain`; that import was removed again.
Rejected Alternatives: Keeping a whole input assembly for one DTO, duplicating the payload in UI contracts, or hiding the edge through reflection. Those either preserve compile fan-out, violate one fact/one owner, or convert a visible dependency into an invisible one.
Scalability potential: Low keeps the same 48-byte input payload and no hot allocation. Middle keeps UI/VR consuming a stable contract. High and Ultra can evolve input implementation without recompiling the UI VR assembly for this payload.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static evidence improved runtime concrete sibling refs 100->99 and first-party asmdefs 168->167. Critical source using/FQN findings returned to 0 after the `FaunaBrain` import cleanup. Build was not launched because `dotnet` and `csc.exe` were active.

## Decision 048 - Unused Runtime Asmdef Edge Pruning

Problem: The residual runtime sibling list contained declared refs with no source usage. `Hecton8.UI.Diegetic` had only an assembly anchor in its runtime assembly but still referenced `Hecton8.Core`; `Hecton8.Graphics.Caustics` used Core service interfaces but did not use `Hecton8.Core.Memory`.
Solution: Removed `Hecton8.Core` / `Hecton8.Core.Contracts` from `Hecton8.UI.Diegetic.asmdef` and removed `Hecton8.Core.Memory` from `Hecton8.Graphics.Caustics.asmdef`. Source was manually checked before the cut.
Rejected Alternatives: Blindly removing every zero-hit result from the crude scanner, or keeping unused refs because they are harmless. The first would break assemblies where short type names depend on referenced namespaces; the second keeps hidden compile fan-out.
Scalability potential: Low removes two unnecessary dependency edges from editor iteration. Middle/High/Ultra keep the same runtime behavior while letting caustics memory internals and Core/UI internals evolve with less compile fan-out.
Hardware Impact: Runtime 0 microseconds claimed. Static evidence improved runtime concrete sibling refs 99->97. Build was not launched because CPU was 51%, above the guard threshold.

## Decision 049 - Fauna Determinism Dependency Recurrence

Problem: `FaunaBrain` reintroduced a physics-determinism dependency to read the latest KCC velocity. That restored the exact AI->Physics source path the prompt forbids.
Solution: Removed `using Hecton8.Physics` and `PhysicsDeterminismSignals` from `FaunaBrain`; the brain now reads `KccVelocitySignal` through the Core determinism signal route and validates frame age locally. Predator player force/trauma fallbacks now use `IPlayerMovementForceSink` and `IPlayerMovementTraumaSink`.
Rejected Alternatives: Keeping a physics facade import, adding an AI reference to the physics asmdef, or using concrete `HectonPlayerMovement` fallback casts. Those preserve hidden compile fan-out or direct player implementation coupling.
Scalability potential: Low keeps the same deterministic velocity snapshot and no scene search. Middle/High/Ultra can increase fauna response fidelity inside AI without recompiling the physics determinism assembly for this read path.
Hardware Impact: Runtime 0 microseconds claimed; no profiler proof. Static evidence: `FaunaBrain` grep for `HectonPlayerMovement`, `using Hecton8.Physics`, `PhysicsDeterminismSignals`, and `Hecton8.Physics` returned no matches; AI/Physics/Physiology concrete/direct-player findings remain 0.

## Decision 050 - Runtime Ref Pruning Without Source Lies

Problem: The residual runtime graph still had declared asmdef refs not backed by current source usage, plus a development SpaceEngine smoke harness counted as runtime.
Solution: Removed the empty `Hecton8.World.Streaming` reference set, removed the unused `Hecton8.Core` ref from `Hecton8.Gameplay.Loot.Contracts`, and made `Hecton8.Dev.SpaceEngine098` Editor-only because its only active consumer is an Editor smoke-test runner.
Rejected Alternatives: Bulk-removing all low-hit refs, or leaving dev/test harnesses in runtime metrics. Bulk removal risks short-name compile breaks; leaving dev harnesses in runtime metrics inflates product blast radius.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. Editor iteration gets a smaller product-runtime graph and cleaner boundary evidence.
Hardware Impact: Runtime 0 microseconds claimed. Static editor evidence improved runtime concrete sibling refs 97->93 after this and adjacent interface passes; cycles remain 0, unresolved first-party refs remain 0.

## Decision 051 - PDA/Progression Player Read-Model Split

Problem: Narrative, PDA, and progression installers still detected player-owned systems through concrete `PlayerExplorationTracker` or `PlayerAchievementRegistry` checks. They were cold owner-composition paths, but the audit still correctly exposed concrete player implementation knowledge outside the owner.
Solution: Expanded read-only player exploration contracts with `ChunkWorldSize`, `CopyExploredChunks`, and `IsChunkExplored`; added `IPlayerAchievementRegistryRuntime`; added PDA-local `IPdaCartographyReadModel`; consumers now resolve interfaces instead of concrete player-owned components.
Rejected Alternatives: Suppressing installer rows, replacing generic concrete checks with reflection, or moving concrete PDA/progression MonoBehaviours into contracts. Those hide the smell, break IL2CPP clarity, or poison the shared contract nucleus.
Scalability potential: Low keeps the same chunk/cartography reads with no hot allocation. Middle/High/Ultra can enrich PDA fog/cartography rendering inside the PDA owner without making narrative/progression depend on player concrete classes.
Hardware Impact: Runtime 0 microseconds claimed. Static direct player concrete findings dropped 7->4 before the kinematics owner pass.

## Decision 052 - Player Kinematics Concrete Owner Cast Removal

Problem: The final direct-player concrete findings were all inside `PlayerKinematicsRuntime`: `GetComponent<HectonPlayerMovement>`, `GetComponent<HectonPlayerMotor>`, and `currentService as HectonPlayerMotor`. Same-owner composition is less dangerous than cross-domain leakage, but it still violates the no concrete cast gate.
Solution: Added `IPlayerKinematicsMovementRuntime` and `IPlayerKinematicsMotorSyncSink` to Core.Contracts, implemented them on `HectonPlayerMovement` and `HectonPlayerMotor`, and changed `PlayerKinematicsRuntime` to cache those interfaces. The locomotion enum remains gameplay-owned; the interface exposes only a byte code for the kinematics signal payload.
Rejected Alternatives: Moving `PlayerLocomotionMode` into contracts, suppressing same-owner rows, or using non-generic `GetComponent(typeof(...))`. Moving the enum widens the shared ABI; suppression or non-generic lookup would only falsify the audit.
Scalability potential: Low keeps the existing motor authority and movement roll path. Middle/High/Ultra can add richer player movement presentation without kinematics depending on concrete owner classes.
Hardware Impact: Runtime 0 microseconds claimed; no profiler proof. Static evidence improved global direct player concrete coupling 4->0; total broad concrete cast findings are 1158 because generic non-player concrete patterns remain outside this task lane.

## Decision 053 - Test Assemblies Are Not Product Runtime

Problem: `Hecton8.PlayModeTests` was counted as product-runtime sibling debt because the graph tools only recognized `.Editor` suffix and `includePlatforms=["Editor"]`. Its asmdef is explicitly gated by `optionalUnityReferences=["TestAssemblies"]` and `defineConstraints=["UNITY_INCLUDE_TESTS"]`, so counting its Core/Core.Memory refs as player-runtime blast radius inflated the metric by two edges.
Solution: Parse `optionalUnityReferences` and `defineConstraints` in both X_003 graph tools and classify Unity test assemblies as non-product runtime. The full DAG still includes the test assembly; only the product-runtime sibling debt excludes it.
Rejected Alternatives: Marking PlayMode tests Editor-only in the asmdef, deleting test refs, or continuing to count tests as player-runtime debt. Editor-only mutation could change test execution semantics; deleting refs breaks tests; inflated metrics are false evidence.
Scalability potential: Low/Middle/High/Ultra benefit from honest product-runtime compile-wall metrics. Test code remains available for editor validation without polluting player blast-radius claims.
Hardware Impact: Runtime 0 microseconds. Static product-runtime sibling refs improved 93->91. Full asmdef count remains 167 and cycles remain 0.

## Decision 054 - Atmosphere Concrete Reads To Read-Model

Problem: Multiple gameplay/audio/world consumers cached or cast `HectonAtmosphereManager` only to read scalar state: underwater flag, fog density, fog attenuation, temperature, radiation, cycle duration, and sea level. That preserved concrete owner knowledge outside the atmosphere domain.
Solution: Expanded `IAtmosphereReadModel` with those scalar read-only fields and rerouted `AcousticZoneController`, `SkySystemFollowCamera`, `AcousticEcholocationTranslator`, `HabitatIntegrityManager`, `HectonSurvivalSystem`, `BaseModule`, `BiomeMatrixDirector`, `HabitatGraphManager`, and `FloraInteractionManager` to `GlobalRegistry.AtmosphereReadModel` / `currentService as IAtmosphereReadModel`.
Rejected Alternatives: Duplicating atmosphere state into GlobalDataVault for same-frame presentation reads, moving `EnvironmentState` into contracts, or suppressing concrete owner rows. Vault writes would add authority ambiguity; enum moves widen ABI; suppression is fake.
Scalability potential: Low keeps the same scalar reads. Middle/High/Ultra can expand atmosphere visuals and simulation internals without these consumers binding to the concrete owner class.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static concrete-cast count dropped while direct player and critical AI/Physics/Physiology lanes stayed zero.

## Decision 055 - Fluid And Vegetation Flow Owner Routes

Problem: `FloraInteractionManager` cached concrete `HectonFluidEngine` for water/current shader parameters, and `AmbientBiotaDirector` cast `MapMagicVegetationRuntime` to concrete `HectonMapMagicVegetationBridge` for abyssal flow sampling. The latter reopened the AI concrete-cast gate.
Solution: Added `IFluidSurfaceCurrentReadModel` on `HectonFluidEngine` with water/current presentation scalars, exposed it through `GlobalRegistry.FluidSurfaceCurrent`, and changed flora to read that interface. Added `TrySampleAbyssalFlow` to `IAbyssalFlowVolumeReadModel` and changed ambient AI to read `GlobalRegistry.AbyssalFlowVolume` / interface hot-swap.
Rejected Alternatives: Leaving concrete casts because they are read-only, moving the vegetation bridge into AI contracts, or routing every sample through managed events. Read-only concrete still couples compile units; moving the owner poisons contracts; events are wrong for synchronous deterministic sampling.
Scalability potential: Low keeps cheap scalar/vector reads. Middle/High/Ultra can expand fluid or vegetation internals without forcing AI/flora consumers onto concrete owner class APIs.
Hardware Impact: Runtime 0 microseconds claimed; methods forward to existing fields/samplers. Static proof: AI/Physics/Physiology concrete cast count returned to 0; global direct player concrete count remains 0.

## Decision 056 - Lore Runtime Concrete Route Burn-Down

Problem: After Loop 37, broad concrete-cast debt stayed high and many gameplay/UI/world consumers still cached concrete Atlas, Quest, AudioLog, FirstHour, and Localization owners for read-only state or narrow command paths. That did not add new asmdef edges immediately, but it preserved source-level owner knowledge and made future physical assembly splits unsafe.
Solution: Added or expanded narrow contracts in Core.Contracts: `IAtlasSignalReadModel`, `IQuestSystem`, `IAudioLogRuntime`, `IFirstHourReadModel`, `IFirstHourRouteContactSink`, and `ILocalizationTextReadModel`. Converted 38 consumers to these routes. Kept `EndingSystem`'s concrete `AtlasSignalSystem` route only for the actual owner command `DecodeSignal`, because no stable command contract exists yet and hiding the command behind an underdesigned facade would be false decoupling.
Rejected Alternatives: Moving concrete MonoBehaviours into Core.Contracts; leaking `QuestPhaseGateType` into Core.Contracts; using reflection or `object` casts to hide debt from the scanner; converting localization span reads to allocating strings. The first build failure exposed the enum leak; the contract was corrected to `byte phaseGateCode`, with the enum cast contained inside `QuestManager`.
Scalability potential: Low keeps the same cached reads and avoids managed event fan-out. Middle keeps presentation systems consuming stable DTO/read-model routes. High and Ultra can increase Atlas, Quest, AudioLog, FirstHour, and Localization implementation complexity inside their owners without forcing UI/world/gameplay consumers onto concrete owner APIs.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. This was compile-wall/source ownership work. Static proof: concrete cast findings 1151->1108, direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0, AI/Physics/Physiology direct player coupling 0, critical source using 0, critical FQN 0. Assembly proof stayed cycle-free: 167 asmdefs, 91 product-runtime concrete sibling refs, Core concrete sibling refs 0, unresolved first-party refs 0. Guarded Core build passed with 0 errors and 4 duplicate-source warnings.

## Decision 057 - ObjectPool Consumer Interface Burn-Down

Problem: Broad concrete-cast debt remained high after Loop 38. A large, safe subset was consumers caching `ObjectPoolManager` only to call pool-service operations: spawn, despawn, warmup, memory-pressure trim, or despawn feasibility. That is concrete owner knowledge outside the pool owner and blocks future physical assembly splits.
Solution: Converted 26 ObjectPool consumers to `IObjectPoolService` fields, locals, hot-swap casts, and cold `GlobalRegistry.ObjectPoolService` reads. Expanded `IObjectPoolService` only with methods already implemented by `ObjectPoolManager`: `WarmupPrefabAsync`, `CanDespawnWithoutDestroy`, and `TrimInactivePoolsForMemoryPressure`. Kept `ObjectPoolManager.PoolItemMarker` references because they are marker-component identity checks, not service owner calls.
Rejected Alternatives: Replacing pool calls with `Instantiate`/`Destroy`, adding SignalBus events for private request/response pool operations, moving `ObjectPoolManager` into contracts, or hiding the concrete route with `object`/reflection. Those would add GC/authority drift, misuse broadcast lanes, pollute contracts, or fake the audit.
Scalability potential: Low keeps pooled spawn/despawn on the existing owner and avoids runtime allocation. Middle/High/Ultra can change the pool owner internals and memory-pressure policy without forcing all spawn/despawn consumers to bind to the concrete manager class.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static source proof improved concrete cast findings 1108->1087 while direct-player, AI/Physics/Physiology, critical source using, and critical FQN gates stayed 0. Assembly graph stayed cycle-free: 167 asmdefs, 91 product-runtime concrete sibling refs, Core concrete sibling refs 0. `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false` passed with 0 errors and 4 duplicate-source warnings; `Hecton8.World.Outposts` remains outside that generated project coverage.

## Decision 058 - ObjectPool Marker Boundary

Problem: `BaseModule` still held `ObjectPoolManager` and reached into `ObjectPoolManager.PoolItemMarker` only to check flooded-reef proxy reserve counts. That is not a hot allocation bug, but it is a concrete owner leak that blocks future physical splits.
Solution: Added `IObjectPoolService.TryGetAvailableCountForPooledInstance(GameObject, out int)` and implemented the marker lookup inside `ObjectPoolManager`. `BaseModule` now caches `IObjectPoolService` from `GlobalRegistry.ObjectPoolService` and calls the contract route.
Rejected Alternatives: Exposing `GetAvailableCountByPrefabId` publicly, moving `PoolItemMarker` into Core.Contracts, or passing the proxy through `GetAvailableCount(GameObject)`. Public prefab-id reads leak owner internals; moving the marker pollutes contracts with pool implementation; prefab lookup can register an instance as a prefab and change behavior.
Scalability potential: Low keeps the same reserve gate without new allocations. Middle/High/Ultra can change marker internals or pool reserve policy in the pool owner without recompiling `BaseModule` for nested owner type changes.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static ObjectPool owner knowledge is reduced while assembly graph stays stable: 167 asmdefs, 0 cycles, 91 product-runtime sibling refs, Core concrete sibling refs 0. Latest source audit stays at concrete cast findings 1084, direct player 0, AI/Physics/Physiology 0, critical using/FQN 0. New build was not launched because CPU was 11.9% but 7 `dotnet` processes plus `VBCSCompiler` were active.

## Decision 059 - Fluid Analytical Contract Split

Problem: Runtime consumers still knew concrete `HectonFluidEngine`, `HectonAnalyticalFlowField`, or `GlobalRegistry.Fluid` for deterministic flow reads, water surface scalars, bubble-burst notification, buoyancy registration, and weather current writes. That was a compile-wall leak even when most paths were read-only.
Solution: Promoted unmanaged flow payloads and deterministic sampler math into `Hecton8.Core.Contracts.Fluids`; exposed narrow contracts through `GlobalRegistry`: `IAbyssalFlowGpuReadModel`, `IAnalyticalFlowReadModel`, `IFluidSurfaceCurrentReadModel`, `IFluidBubbleBurstSink`, `IBuoyancyObjectRegistry`, and `IFluidCurrentWriteSink`. Converted player kinematics/movement, fauna, boids, construction, visuals, weather, crash telemetry, and biome consumers to those routes.
Rejected Alternatives: Core referencing physics/fluid concrete assemblies, duplicate DTO structs in consumers, reflection/object casts, or SignalBus request/response for synchronous deterministic reads. Those preserve compile pressure, violate one fact/one owner, or misuse broadcast lanes.
Scalability potential: Low keeps cheap scalar/vector reads and fixed-layout DTOs. Middle keeps flow ownership in the fluid runtime. High and Ultra can expand GPU/analytical fluid internals without recompiling consumers that only read the contract snapshots.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static proof improved broad concrete cast findings 1084->1065 across the Loop 41 slice while direct-player, AI/Physics/Physiology, critical using, and critical FQN gates stayed 0.

## Decision 060 - Acoustic Weather First-Hour Read Models

Problem: Audio, localization, visuals, world readability, and narrative consumers still cached or cast concrete `AcousticZoneController`, `HectonSurfaceWeatherDirector`, or `FirstHourDirector` for read-only state or one narrow cue sink.
Solution: Added narrow contracts: `IAcousticZoneReadModel`, `IAcousticZoneMadnessCueSink`, `ISurfaceWeatherReadModel`, and byte-coded `SurfaceWeatherKindCodes`; reused `IFirstHourReadModel` for milestone checks. Converted the consumers to `GlobalRegistry` contract routes while leaving concrete serialized owner references inside the owner/bootstrap paths where Unity authoring still requires them.
Rejected Alternatives: Moving weather/acoustic MonoBehaviours or gameplay enums into Core.Contracts, using managed events for synchronous state reads, or deleting serialized concrete owner references blindly. Those would pollute contracts, add allocation/ordering ambiguity, or break scene authoring.
Scalability potential: Low keeps current weather/acoustic state reads cheap. Middle keeps audio/weather authority isolated. High and Ultra can add richer surface weather or acoustic-zone internals without recompiling consumers that only need stable read-models.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static proof: critical source using 0, critical FQN 0, direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0, product asmdef cycles 0, Core concrete sibling refs 0.

## Decision 061 - Fluid GPU Read Models For VFX And Construction

Problem: VFX and construction consumers still reached into `HectonFluidEngine`/`GlobalRegistry.Fluid` for GPU wake buffers, maelstrom upload, and surface current strength. These were read/transfer routes, but concrete owner knowledge would force future VFX or construction splits to keep a fluid-domain compile dependency.
Solution: Expanded `IAbyssalFlowGpuReadModel` with dynamic wake payload and active-maelstrom upload methods, expanded `IFluidSurfaceCurrentReadModel` with current strength scalars, and routed `HectonMarineSnowRenderer`, `CarveDebrisComputeRenderer`, and `DroneFleetManager` through those interfaces.
Rejected Alternatives: Leaving concrete `HectonFluidEngine` fields in consumers, duplicating GPU buffer ownership, or using SignalBus for synchronous buffer reads. The first preserves compile-wall debt; the second violates one owner; the third is the wrong lane for immediate readback/dispatch data.
Scalability potential: Low keeps cheap scalar/GPU-buffer access through stable contracts. Middle/High/Ultra can expand fluid GPU internals and maelstrom fidelity without recompiling consumers that only need the read-model surface.
Hardware Impact: Runtime 0 microseconds claimed; no profiler proof. The calls forward to existing owner data/dispatch paths. Static broad cast debt dropped during the slice; critical source using/FQN, direct-player, and AI/Physics/Physiology lanes stayed zero.

## Decision 062 - Audio World Owner Read-Model Burn-Down

Problem: World and audio consumers still cached concrete owner classes for narrow reads: `DepthZoneDirector` knew `QuestManager`/`LocalizationManager`, music knew `DepthZoneDirector`, acoustic zone knew `SoundscapeSystem`, psychosis audio knew `EnvironmentalStrainManager`, and critical player audio knew `SpatialAudioManager`.
Solution: Added narrow contracts to `GlobalRegistryContracts.cs`: formatted localization read, quest depth-context sink, soundscape tier read, environmental strain read, spatial listener cave read, and fixed-layout binaural emitter telemetry read. Routed the consumers through `GlobalRegistry` interface slots and made owner systems implement those slots.
Rejected Alternatives: Moving gameplay/audio/world enums or MonoBehaviours into Core.Contracts, preserving concrete casts with comments, or using reflection/object casts to silence scanners. Enums are byte-coded at boundaries; concrete interpretation stays inside the owner domain.
Scalability potential: Low keeps scalar/DTO reads cheap and scene authoring stable. Middle keeps audio/world domains independently adjustable. High and Ultra can add richer depth, soundscape, strain, and binaural emitter internals without forcing consumer assemblies onto concrete owner types.
Hardware Impact: Runtime 0 microseconds claimed; no profiler proof. Static evidence after the slice: concrete casts 1057, critical source using 0, critical FQN 0, direct player concrete coupling 0, AI/Physics/Physiology concrete/direct-player 0, product-runtime concrete sibling refs 92, cycles 0. Build proof is absent because the latest guard sample was CPU 74.9% with an active `dotnet` process.

## Decision 063 - Disabled Caustics Shim Zero-Ref Cut

Problem: `AnalyticalCausticsService` was a disabled compatibility shim, but it still implemented Core/Bootstrap service interfaces and its asmdef still referenced Core, Core.Contracts, Core.Memory, Unity.Collections, and Unity.Mathematics. That kept a dead graphics shim attached to shared rebuild nuclei.
Solution: Removed the service/heartbeat/shutdown/hot-swap interfaces and all unused properties/callbacks from the shim. Removed every first-party/collections/math reference from `Hecton8.Graphics.Caustics.asmdef`. The real caustics authority remains the active abyssal deferred caustics runtime, not this disabled component.
Rejected Alternatives: Deleting the asmdef and letting the file fall back into a parent assembly, leaving service interfaces on a component that disables itself, or claiming runtime perf gain from a disabled shim cleanup.
Scalability potential: Low/Middle/High/Ultra runtime visuals are unchanged. Editor iteration is cleaner because this graphics folder no longer compiles against shared Core/service assemblies.
Hardware Impact: Runtime 0 microseconds claimed. Static graph proof returned product-runtime concrete sibling refs to 91 and Core.Contracts boundary violations to 119 after the final cut.

## Decision 064 - RenderTexturePool Contract Route And Core UI Edge Prune

Problem: `ToolDiegeticDisplayController` imported `Hecton8.Optimization`, stored concrete `RenderTexturePool`, and cast hot-swap payloads back to that concrete owner. A separate graph audit also caught a reintroduced zero-hit Core -> `Hecton8.UI.Localization` concrete asmdef edge.
Solution: Added `IRenderTexturePoolService` in `Hecton8.Core.Contracts`, implemented it on `RenderTexturePool`, exposed `GlobalRegistry.RenderTexturePoolService`, and changed the tool display to cache/use the interface only. Removed the zero-hit `Hecton8.UI.Localization` reference from `Hecton8.Core.asmdef`. Added the new contract to `Directory.Build.targets` for CLI Core coverage; removed the duplicate explicit `.csproj` include after the first build exposed it.
Rejected Alternatives: Keeping the concrete UI -> Optimization route, adding a SignalBus request/response for synchronous texture rent/return, moving the entire pool owner into contracts, or removing other Core asmdef refs without source proof.
Scalability potential: Low keeps the existing pooled RT behavior and fallback texture path. Middle/High/Ultra can change pool internals independently while UI consumers keep the rent/return contract.
Hardware Impact: Runtime 0 microseconds claimed; calls forward to the existing pool owner. Static proof: Core concrete sibling refs 0, product-runtime concrete sibling refs 91, `ToolDiegeticDisplayController` has no `Hecton8.Optimization` import or concrete `RenderTexturePool` storage. First guarded Core build passed with 0 errors and 5 warnings; duplicate include fixed afterward, rebuild retry blocked by CPU 90.1%.

## Decision 065 - RenderTexture Lifecycle Contract Route

Problem: RenderTexture lifecycle and pool consumers outside the owner path still cached or cast concrete `RenderTextureLifecycleTracker` and `RenderTexturePool`, and the lifecycle record/category types were trapped in the Optimization namespace. That preserved source-level owner knowledge in UI, visor, diagnostics, and VRAM pressure code.
Solution: Move the lifecycle record/category to Core.Contracts, add `IRenderTextureLifecycleService`, expand `IRenderTexturePoolService` only with existing pool statistics and release methods, expose `GlobalRegistry.RenderTextureLifecycleService`, and convert non-owner consumers to interface fields, locals, hot-swap casts, and cold registry reads.
Rejected Alternatives: Keeping Optimization imports in PDA/vehicle UI, using SignalBus request/response for synchronous RT rent/return/report reads, moving full pool/lifecycle MonoBehaviours into contracts, or suppressing scanner rows. Those either preserve compile-wall owner knowledge, misuse broadcast lanes, pollute contracts with behavior, or fake the audit.
Scalability potential: Low keeps the same pooled RT behavior and cheap lifecycle snapshots. Middle keeps UI/visor/diagnostics decoupled from pool/lifecycle implementation churn. High/Ultra can change RT pool internals, leak detection, or visual RT retention policy without recompiling consumers that only need contract routes.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static proof improved broad concrete-cast findings 1058->1049 while critical source using/FQN, direct-player, and AI/Physics/Physiology gates stayed zero. Product-runtime sibling refs remain 91; this slice did not change asmdef edge count.

## Decision 066 - VRAM Fluid Decal Tool Durability Contract Routes

Problem: Non-owner consumers still cached or cast concrete `VRAMPressureMonitor`, `AbyssalFluidDecalManager`, `MapMagicBridge`, and `ToolDurabilitySystem` for read-only samples or narrow write commands. A zero-hit Core -> `Hecton8.UI.Localization` asmdef edge was also reintroduced concurrently, raising Core concrete sibling refs back above zero until cut again.
Solution: Added `IVramPressureReadModel`, `IVramPressureSampleSink`, `IVramPressureMipBiasSink`, `IFluidDecalPresentationSink`, and `IToolDurabilityService` to Core.Contracts. Owners implement these routes; consumers use `GlobalRegistry` contract properties and typed hot-swap casts. `BiomeMatrixDirector` now reads terrain through `ITerrainProvider` and emits fluid decal presentation through `IFluidDecalPresentationSink`. Removed the Core UI.Localization asmdef edge after grep proof showed Core source does not need it.
Rejected Alternatives: SignalBus request/response for synchronous VRAM/durability reads, moving MonoBehaviour owners into contracts, keeping direct `MapMagicBridge`/fluid decal owner casts, suppressing scanner rows, or leaving the Core UI edge because it was convenient for generated projects. Those would preserve compile-wall debt or pollute contracts with behavior.
Scalability potential: Low keeps cheap scalar reads and existing presentation calls. Middle keeps terrain/fluid visuals and tool wear behind stable contracts. High and Ultra can change VRAM policy, fluid decal implementation, terrain bridge internals, or durability mirroring without recompiling consumers that only need DTO/read-model/sink routes.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static proof improved broad concrete-cast findings 1049->1009 across the current slice, while critical source using/FQN, direct-player, and AI/Physics/Physiology gates stayed zero. Assembly graph stayed cycle-free: 167 asmdefs, 91 product-runtime concrete sibling refs, Core concrete sibling refs 0, unresolved first-party refs 0. Build was not launched because AGENTS.md guard stayed closed: CPU 65% first, then CPU 100% with active `csc` and `dotnet`.

## Decision 067 - Fauna Terrain Read-Model Recut

Problem: `FaunaKinematicsRuntime` reintroduced concrete `MapMagicBridge` knowledge after the previous terrain/vegetation cut. That put AI/Fauna back on a world-owner class path for a deterministic height sample.
Solution: Read the same terrain payload through `ITerrainHeightSampleReadModel` and `GlobalRegistry.TerrainHeightSamples`. Hot-swap handling now accepts the interface, while the owner can still be `MapMagicVegetationRuntime` internally.
Rejected Alternatives: Keeping the concrete bridge because the call is read-only, suppressing the scanner, or duplicating terrain sampling in fauna. Read-only concrete still couples assemblies; suppression is false evidence; duplicate sampling creates a second terrain authority.
Scalability potential: Low keeps the same cheap terrain payload. Middle/High/Ultra can change MapMagic bridge internals or add higher-fidelity terrain data without recompiling fauna kinematics.
Hardware Impact: Runtime 0 microseconds claimed; the route forwards to existing data. Static proof restored AI/Physics/Physiology concrete casts to 0 after the reopened finding.

## Decision 068 - VRAM Budget And ScanLog Contract Burn-Down

Problem: VRAM consumers and scan/log gameplay consumers stored or hot-swapped concrete `VRAMMonitor` and `ScanLogSystem` owners for scalar reads, budget pressure state, explicit sampling, or narrow archive/check calls. Those routes are compile-wall debt even when they do not add new asmdef edges today.
Solution: Added `IVramBudgetReadModel`, `IVramBudgetSampleSink`, byte-coded `VramPressureStateCodes`, `IAssetLifecyclePressureSink`, and `IScanLogService`. Exposed them through `GlobalRegistry`, implemented them on the existing owners, and converted 21 source files to interface storage/casts where the interaction is a read model or narrow sink.
Rejected Alternatives: Moving the MonoBehaviour owners into Core.Contracts, replacing synchronous reads with SignalBus request/response, hiding concrete casts behind `object`, or converting Unity serialized scene references to interfaces. Owners do not belong in contracts; SignalBus is broadcast, not request/response; `object` hides debt; Unity cannot serialize interfaces reliably.
Scalability potential: Low keeps scalar/DTO reads cheap. Middle keeps content, UI, VFX, scan tools, and world residency independent of VRAM/scan owner implementation churn. High/Ultra can expand VRAM policy and scan-log internals without recompiling consumers that only need the stable route.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static broad concrete-cast findings improved 1009->992 in this slice and 1151->992 since Loop 37, with critical source using/FQN, direct-player, and AI/Physics/Physiology gates at zero.

## Decision 069 - Addressable Lifecycle Handle Boundary Held

Problem: `WorldChunkResidencyManager` still calls concrete `AssetLifecycleGovernor` for addressable handle acquisition/release paths. A pressure-only interface exists now, but those handle-owner calls are not just scalar reads.
Solution: Converted only the VRAM budget read route in `WorldChunkResidencyManager` and left addressable lifecycle concrete owner calls in place until a separate handle-ownership contract is designed with explicit acquire/release authority.
Rejected Alternatives: Dumping addressable acquire/release methods into `IAssetLifecyclePressureSink`, converting handles to weak `object` payloads, or adding SignalBus request/response for synchronous asset ownership. Those would blur ownership, lose type safety, or make lifetime ordering nondeterministic.
Scalability potential: Low keeps current streaming safety. Middle/High/Ultra can later split pressure telemetry from addressable lifetime control without corrupting ownership.
Hardware Impact: Runtime 0 microseconds claimed. The residual concrete route is deliberately recorded; hiding it would create a false green graph.

## Decision 070 - Localization Presentation Contract Burn-Down

Problem: Localization presentation consumers in HUD, PDA, audio-log, interaction, boot, pause, movement, and item paths still stored or cast concrete `LocalizationManager` or read `GlobalRegistry.Localization` for text expansion, hull-stress corruption, madness preview, PDA corrosion, and transient language override operations.
Solution: Added narrow localization contracts in Core.Contracts and exposed matching `GlobalRegistry` routes: `ILocalizationTextExpansionReadModel`, `ILocalizationLanguageControl`, `ILocalizationStressPresentationReadModel`, `ILocalizationMadnessPresentationReadModel`, `IPdaCorrosionPresentationSink`, `ILocalizationTransientOverrideSink`, `ILocalizationStressHudRefreshSink`, and deterministic `LocalizationMadnessHash`. `LocalizationManager` remains the owner; consumers depend on the narrow interfaces.
Rejected Alternatives: Moving `LocalizationManager` into contracts, preserving concrete casts because they are UI-local, using SignalBus for synchronous text lookup, or returning managed strings where existing span routes exist. Those either poison the contract assembly, preserve source-owner coupling, misuse the broadcast lane, or add allocation pressure.
Scalability potential: Low keeps existing text/span behavior and corruption math. Middle keeps HUD/PDA/audio-log independent from localization owner churn. High/Ultra can expand localization madness presentation or language policy without recompiling consumers that only need the read-model/sink surface.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static broad concrete-cast findings are 979 after Loop 50, down from 992 at Loop 48 and 1151 at Loop 37; critical source using/FQN, direct-player, and AI/Physics/Physiology lanes remain zero.

## Decision 071 - AI Player Health Concrete Route Removal

Problem: `FaunaBrain` and `EnvironmentalHazard` still had direct fallback paths to concrete `HectonPlayerHealth` through transform/component lookup. The scanner reported direct-player coupling zero earlier because the critical lane was narrow, but the source pattern was still a real backdoor against the no concrete player-cast contract.
Solution: Added `CombatDamageRuntime.TryResolveRegisteredTarget(Transform, out int, out Transform)` as a narrow public wrapper over the existing registered-target resolver. `FaunaBrain` now resolves damage targets through the combat registry and `IDamageReceiver`. `EnvironmentalHazard` no longer searches the transform hierarchy for concrete player health; it uses cached/runtime-context health routes only.
Rejected Alternatives: Interface-to-concrete casts, per-hit scene hierarchy search, duplicating health state in fauna, or routing immediate damage through SignalBus request/response. Those preserve compile coupling, add hot-path lookup, create a second owner, or misuse broadcast semantics.
Scalability potential: Low keeps damage routing deterministic and cheap. Middle/High/Ultra can change player health implementation behind `IDamageReceiver` and combat registration without recompiling fauna/physics-like hazards.
Hardware Impact: Runtime microseconds not claimed. Static proof after Loop 50: direct player concrete coupling 0, AI/Physics/Physiology direct player coupling 0, AI/Physics/Physiology concrete casts 0.

## Decision 072 - EnvironmentalStrain LateFrame Hot Lookup Cut

Problem: `EnvironmentalStrainManager.LateFrameTick()` still polled `GlobalRegistry.EnvironmentalStrain` to suppress duplicate instances. That is not simulation math, but it is a registry lookup inside a hot dispatcher callback.
Solution: Removed the registry poll from `LateFrameTick`; pending duplicate instances now destroy themselves directly after the duplicate flag is set by the owner path.
Rejected Alternatives: Keeping the per-frame registry equality check, adding another hot-swap signal for a local duplicate flag, or suppressing the scanner row. The local flag already holds the fact; extra registry traffic and scanner suppression are unnecessary.
Scalability potential: Low removes one hot dispatcher registry read. Middle/High/Ultra keep environmental strain authority unchanged while making the callback route cleaner.
Hardware Impact: Exact microseconds unmeasured. Static proof after Loop 50: hot-path lookup findings 0; remaining hot-path registry mutation notes are three self-unregister lanes, not lookup/search rows.

## Decision 073 - Invalid UTF-8 Files Held As Residual Debt

Problem: `CorporateOrderSystem.cs` still has a concrete localization route suitable for the new contracts, but `apply_patch` failed with an invalid UTF-8 byte sequence. `SaveManager.cs` has the earlier VRAM residual for the same reason. Editing these files with shell/Python byte rewrites would violate the safe edit protocol and risks corrupting non-UTF source.
Solution: Leave both files unchanged in this pass and record the residual debt explicitly. Future cleanup requires an encoding-normalization pass owned by the integration lane before semantic edits.
Rejected Alternatives: Blind byte rewriting, shell redirection, Python write scripts, or claiming the routes are gone. Those risk data loss or false evidence.
Scalability potential: Low/Middle/High/Ultra are unaffected at runtime by this non-edit. The project still needs encoding normalization to keep future contract burn-down deterministic.
Hardware Impact: Runtime 0 microseconds. Residual is source hygiene and compile-wall debt, not a performance claim.

## Decision 074 - Beacon Network Service Boundary

Problem: `BeaconDeployerTool` and `SargassumMicroFaunaBoids` cached concrete `BeaconNetworkSystem` and the nested `BeaconSnapshot` type. That was source-level owner knowledge in tool/world consumers and blocked a future split of beacon storage/runtime internals from beacon readers.
Solution: Added `BeaconNetworkSnapshot` and `IBeaconNetworkService` in Core.Contracts, implemented the route on `BeaconNetworkSystem`, exposed `GlobalRegistry.BeaconNetworkService`, and converted the tool plus Sargassum formation consumer to interface storage, typed hot-swap casts, and contract snapshots. Concrete `BeaconNetworkSystem` remains for bootstrap/owner registration and `BeaconRuntime.NotifyRuntimeDestroyed`.
Rejected Alternatives: A separate duplicate registry slot for the interface, moving `BeaconRuntime` or the full `BeaconNetworkSystem` into contracts, using SignalBus request/response for deploy/retract reads, or converting labels to `FixedString` inside this slice. Duplicate slots create stale hot-swap state; behavior owners do not belong in contracts; SignalBus is broadcast, not synchronous command response; label storage migration touches save/UI and needs its own pass.
Scalability potential: Low keeps the existing bounded snapshot copy and deploy/retract behavior. Middle/High/Ultra can change beacon placement, lighting, persistence, or formation-selection internals without recompiling tool/world consumers that only need the service contract.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static proof after Loop 51: broad concrete-cast findings 979->973 in this continuation and 1151->973 since Loop 37; critical source `using` 0; critical FQN 0; hot-path lookup 0; direct player concrete coupling 0; AI/Physics/Physiology concrete casts 0.

## Decision 075 - Submarine Atmosphere Room Contract Route

Problem: Construction, power, gameplay, fluid, structural, equipment, and tool paths were reading or mutating room atmosphere through concrete `SubmarineAtmosphereSystem`. That keeps submarine room state coupled to the owner MonoBehaviour even when the consumer only needs deterministic room pressure/oxygen/fire/flood DTO-like scalars or a narrow mutation command.
Solution: Added `ISubmarineAtmosphereRoomReadModel`, `ISubmarineAtmosphereRoomMutationSink`, and `ComponentReferenceUtility.ResolveParentService<T>` in Core.Contracts. `SubmarineAtmosphereSystem` implements the mutation sink and exposes read-only room state through the same owner. Consumers with safe UTF-8 source were moved to interface fields, locals, and parent-service resolution.
Rejected Alternatives: Moving the room atmosphere owner into contracts, adding reflection/object escape hatches, converting room mutations into SignalBus request/response, or byte-rewriting invalid UTF-8 files. Owner movement pollutes contracts; reflection/object hides debt; SignalBus does not own synchronous mutation semantics; byte rewrites risk corrupting source.
Scalability potential: Low keeps current room-state math and bounded parent-walk service lookup. Middle/High/Ultra can replace submarine atmosphere internals, room graph policy, or presentation detail without recompiling consumers that depend only on read/mutation contracts.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static proof is part of Loop 51: broad concrete-cast findings 973, critical source `using` 0, critical FQN 0, hot-path lookup 0. Residual debt is explicit: `BaseModule.cs` still needs this conversion after encoding normalization.

## Decision 076 - Tool Trial Beacon Smoke Uses Service Contract

Problem: `ToolTrialRangeRuntimeSmokeTester` was development-only, but it still serialized and scene-scanned concrete `BeaconNetworkSystem`. Leaving dev smoke code as a concrete consumer keeps stale examples of the wrong route and lets future tests re-normalize concrete owner access.
Solution: Replace the serialized concrete field with a `MonoBehaviour` provider plus cached `IBeaconNetworkService`, keeping `FormerlySerializedAs("beaconNetwork")` so existing authored smoke references survive. Auto-resolve now checks the provider, `GlobalRegistry.BeaconNetworkService`, then a dev-only interface scene scan.
Rejected Alternatives: Leaving it because it is behind `UNITY_EDITOR || DEVELOPMENT_BUILD`, or using concrete `BeaconNetworkSystem` only for scene find. Dev code still teaches and compiles against the owner; concrete scene find preserves the source dependency.
Scalability potential: Low keeps the same smoke assertion. Middle/High/Ultra can change beacon internals without updating the smoke harness as long as the service contract holds.
Hardware Impact: Runtime 0 microseconds claimed; this path is editor/development smoke only. Static grep over Beacon tool/world/smoke consumers shows no `BeaconNetworkSystem` or nested `BeaconSnapshot` references outside the owner/bootstrap/runtime-notification lanes.

## Decision 077 - Construction Logistics And Habitat Graph Contract Route

Problem: Construction-side consumers still cached or serialized concrete `ConstructionManager` for module enumeration, catalog reads, module registration/clearing, habitat acoustic graph access, and parasite-spread graph decisions. That made UI, tools, flora, audio, mod runtime, and smoke code know the construction owner class instead of the route they consume.
Solution: Widened `ILogisticsService` with existing logistics owner operations, widened `IHabitatGraphService` with acoustic graph access, and added `IConstructionParasiteGraphService` for parasite-root graph reads/notifications. `ConstructionManager` remains the owner; consumers cache interfaces from provider fields, environment context, or `GlobalRegistry` contract routes. `GlobalRegistry.ConstructionParasiteGraph` maps to the logistics owner without a second stale slot.
Rejected Alternatives: Moving `ConstructionManager` into Core.Contracts, leaving concrete casts with comments, adding SignalBus request/response for synchronous module enumeration, or duplicating construction state in UI/flora/audio. Those pollute contracts, fake the audit, misuse broadcast semantics, or create a second owner.
Scalability potential: Low keeps module reads and graph queries on existing bounded owner data. Middle/High/Ultra can change construction manager internals, habitat graph representation, or parasite spread policy without recompiling consumers that only need logistics/habitat/parasite contracts.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static broad concrete-cast findings dropped from Loop 51's 973 to 969 after the construction contract slice, while critical source using/FQN, direct-player, and AI/Physics/Physiology lanes stayed zero.

## Decision 078 - Vehicle Docking Physics State Event Boundary

Problem: `VehicleDockingModule` imported `Hecton8.Physics` to call `PhysicsForceRouter`, `GlobalPhysicsStateManager` dock connection methods, and submarine-fluid concrete mass injection. That tied a construction/vehicle docking component to the physics implementation and fluid vehicle owner class.
Solution: Routed velocity/force application through existing `IPhysicsService`; added `IPhysicsStateEventService` for kinematic impact and dock connection ownership events; added `IDockedExternalMassSink` for the submarine-fluid external mass sink. `GlobalPhysicsStateManager` implements the state-event service and `SubmarineFluidDynamics` implements the mass sink. `VehicleDockingModule` now resolves only contracts and has no `Hecton8.Physics` import or concrete physics-state reference.
Rejected Alternatives: Keeping the physics import because the calls are narrow, moving physics state manager methods into vehicle code, using reflection/object casts, or broadcasting synchronous dock mass through SignalBus. Those preserve compile-wall coupling, duplicate authority, hide type debt, or use the wrong lane for immediate owner state.
Scalability potential: Low keeps current docking behavior and bounded parent-service resolution. Middle/High/Ultra can change physics event aggregation, dock impact telemetry, or submarine fluid internals without recompiling the docking module.
Hardware Impact: Runtime 0 microseconds claimed; no profiler sample. Static proof after the final Loop 52 pass: concrete cast findings 968, critical source using 0, critical FQN 0, hot-path lookup 0, direct-player 0, AI/Physics/Physiology concrete casts 0, runtime concrete sibling refs 96, cycles 0.

## Decision 079 - Build Guard Held After Loop 52

Problem: The source slice is static-clean by X_003 gates, but AGENTS.md forbids launching `dotnet build` while CPU is above 50% or compiler processes are active. The latest samples had active `csc`/`dotnet`, then CPU 97 with active compiler processes.
Solution: Do not launch a compile. Keep the proof class at STATIC_SOURCE plus guarded compile eligibility checks. Record the absence of Unity import, Console, PlayMode, profiler, GC, and player-build evidence.
Rejected Alternatives: Running build into compiler contention, killing unrelated compiler processes, or claiming the Loop 39/45 green Core builds cover the Loop 52 source edits. Those would violate the guard, sabotage other agents, or overstate proof.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unaffected by the guard decision. The architecture work remains staged for the next clean compile window.
Hardware Impact: Runtime 0 microseconds claimed. Compile proof is pending; exact editor wall-clock savings are not claimed. Static blast-radius proof still shows selected cable/metabolism/AI cognition files do not reach UI/audio.
