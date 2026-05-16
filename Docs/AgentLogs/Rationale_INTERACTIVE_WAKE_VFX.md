# Rationale - INTERACTIVE_WAKE_VFX

## Decision 1 - Missing Active XML Prompt (Resolved)

Problem: The launcher requested `INTERACTIVE_WAKE_VFX`, but `Docs/Tasks/CURRENT_BATCH.md` has no matching XML tag.

Solution: Stop before code and mark the assignment `[BLOCKED BY MISSING XML PROMPT]`. This follows the Batch Prompt Protocol, strict parsing, and the active batch audit decision that missing prompts must not be synthesized.

Rejected Alternatives: Borrowing `VOLUMETRIC_SILT_ADVECTION`, `SCREEN_SPACE_REFRACTION`, or archived `WAKE_TURBULENCE_COMPUTE` scope would contaminate domain ownership. Standard Unity-style "just implement a wake component" would bypass GlobalRegistry/DataVault ownership, risk duplicate wake buffers, and violate parallel-agent decoupling.

Scalability potential: Not implemented. Intended wake work would require Low/Middle/High/Ultra math LODs, with Low using a cheap analytic radial or triangle-wave fake, Middle sampling a fixed wake ring, High adding stronger normal displacement, and Ultra buying visual overkill through richer shader response. This cannot be specified without an authorized prompt.

Hardware Impact: No runtime code changed. Estimated gain for low-end silicon such as i3/MX350: 0 us because execution was blocked before implementation. Estimated risk avoided: duplicate GPU/CPU wake pipeline and possible compile break in a concurrent batch.

## Domain Boundary Note

Authoritative domain inferred from instruction list only: VFX/WAKE. This is not enough to authorize edits. The domain definition file and active batch audit both require a concrete task boundary before code changes.

## Decision 2 - Phase 1 Scope

Problem: User directed Phase 1 after XML injection: purge WindZones, remove wake singleton access, and move active wake source data into GlobalDataVault.

Solution: Execute Phase 1 only, using the existing wake owner (`FloraInteractionManager`) instead of creating a duplicate manager. Add `IWakeDisplacementService` as a registry-facing contract alias over the existing procedural sway wake service. Move wake source native storage to a DataVault buffer ID owned by `SystemID.Vfx`.

Rejected Alternatives: Editing GPU Instancer vendor WindZone code would violate third-party integrity. Creating `WakeManager.Instance` would violate the prompt. Creating a second wake buffer in `Assets/_Project/Scripts/VFX/Wakes/` would fork the existing wake publisher and produce inconsistent flora shader globals.

Scalability potential: Low/MX350 keeps a capped 16-source analytic wake vector array. Middle keeps the same cap with stronger shader response. High adds vortex curvature in shader. Ultra spends saved CPU on richer normal perturbation and particle/boid coupling without increasing CPU source count.

Hardware Impact: Moving private wake state to DataVault removes a local persistent native allocation owner and centralizes memory accounting. Estimated low-end gain: 0-5 us/frame from lower owner churn and no scene scan; the visible gain is architectural safety, not raw frame time.

## Decision 3 - Registry Alias Instead of New Wake Singleton

Problem: Phase 1 requires `IWakeDisplacementService` in `GlobalRegistry`, but the project already has an active wake publisher inside `FloraInteractionManager` through `IProceduralSwayDirector`.

Solution: Add `IWakeDisplacementService` as the narrow wake contract and make `IProceduralSwayDirector` inherit it. Expose `GlobalRegistry.WakeDisplacement` as an alias of the existing procedural sway runtime slot and map `IWakeDisplacementService` to `ProceduralSwayDirectorRuntime` for service resolution.

Rejected Alternatives: A new `WakeManager.Instance` would violate the prompt. A second registry slot would create two possible wake authorities. A Unity `WindZone` or `ParticleSystem.forceOverLifetime` path would move the system back to managed component wind instead of raw shader arrays.

Scalability potential: Low keeps the single authoritative source list capped at 16. Middle can publish all 16 analytic wake vectors. High can spend shader math on curvature. Ultra can reuse the same contract for particle advection and boid avoidance without adding a second CPU manager.

Hardware Impact: No new hot-path allocations and no singleton lookup. Estimated gain for i3/MX350: 0-3 us/frame versus duplicated manager lookup and sync. Primary value is deterministic ownership and avoiding two buffers fighting over shader globals.

## Decision 4 - DataVault Wake Source Ownership

Problem: Active wake sources were locally owned by `FloraInteractionManager` as a persistent native array, which violates Phase 1 data eviction and hides the buffer from global memory telemetry.

Solution: Add `BufferID.WakeSources` and resolve `NativeArray<ProceduralWakePoint>` through `IDataVault.GetBufferHandle<ProceduralWakePoint>(..., SystemID.Vfx)`. Keep only a generation-checked view and handle in `FloraInteractionManager`; DataVault owns the storage. Store AUP alongside runtime position, velocity, radius, intensity, age, and source kind.

Rejected Alternatives: Keeping the old persistent native allocation would fail the prompt. Using a managed `List<WakeSource>` would violate zero-GC policy. Storing only world-space positions would break AUP integrity on origin shifts.

Scalability potential: Low/MX350 uses 16 fixed slots and shader radial push. Middle keeps all 16 with stronger local displacement. High/Ultra can increase visual cost in shader while the CPU source buffer stays fixed and predictable.

Hardware Impact: Estimated low-end gain: 0-5 us/frame from centralized native memory ownership and smaller CPU source cap. The bigger impact is stable accounting and no extra allocation owner in scene lifecycle.

## Decision 5 - Compile Wall Classification

Problem: `dotnet build .\Hecton8.Core.csproj -v:minimal` exits 1 before this wake slice can be fully validated.

Solution: Classify the build failure as `[BLOCKED BY DEPENDENCY]` because the visible 159 errors are missing cross-domain contracts and namespaces (`IJobAdmissionService`, `ISimulationBucketer`, `MacroDatabase*`, `IPlayerMovementContracts`, `FoveatedSimulationTier`, `H8WorldPage*`, and related systems). Keep Phase 1 static validation complete and do not edit unrelated domains to chase global dependency breakage.

Rejected Alternatives: Patching broad core/save/player/streaming contracts from the VFX wake prompt would violate domain boundaries. Reverting shared files would destroy other agents' work. Claiming validation passed would be false.

Scalability potential: No runtime scaling change. This protects the wake implementation from cross-domain repair churn until the Integrator restores missing contracts.

Hardware Impact: 0 us/frame. Risk avoided: destabilizing unrelated systems while attempting to fix a non-VFX compile wall.

## Decision 6 - Multiplatform Wake Data Layout

Problem: The previous wake source payload was a private sequential struct inside `FloraInteractionManager`, with implicit padding risk and no authoritative file under the XML domain.

Solution: Created `Assets/_Project/Scripts/VFX/Wakes/WakeDisplacementData.cs` with explicit `Pack = 1` layouts for `WakeSource` (128 bytes) and `WakeTelemetryEntry` (64 bytes). All wake source, global wake vector, and blackbox storage resolves through DataVault handles. `FloraInteractionManager` no longer owns a persistent `NativeArray<WakeSource>` field.

Rejected Alternatives: Keeping a private sequential wake struct would be fragile for ARM64/Quest. Using managed classes or lists would violate zero-GC. Moving all flora manager arrays to DataVault in this wake pass would cross into non-wake flora ownership and risk unrelated breakage.

Scalability potential: Low/MX350 caps active wake math to 4 slots. Middle/High/Ultra publish up to 16 slots through the same layout. High/Ultra shader work can use `_GlobalWakeVectors` for curvature without changing CPU storage.

Hardware Impact: Estimated i3/MX350 gain is 4-12 us/frame versus component/object fanout and duplicated local wake arrays. Quest/Android gain is crash avoidance: explicit offsets remove implicit-layout surprises.

## Decision 7 - Typed Wake Signal Lane

Problem: Wake generation still had a public legacy `NativeQueue` reader/writer surface in `GlobalSignals`, which created two possible transport lanes for the same packet.

Solution: `GlobalSignals.Publish(in WakeGeneratedSignal)` now pushes only into `SignalBus<WakeGeneratedSignal>`. `FloraInteractionManager` consumes `ReadOnlySpan<WakeGeneratedSignal>` snapshots. The public `WakeGeneratedSignalWriter` and `TryDequeueWakeGenerated` APIs were removed.

Rejected Alternatives: Keeping both typed and legacy queues would allow duplicate wake injection. Managed delegates were not considered because the project signal policy is native typed lanes.

Scalability potential: Low tier drops old signal pressure through typed lane budgets. High/Ultra can emit more wake producers without inventing new signal types.

Hardware Impact: Estimated low-end gain is 3-8 us/frame during noisy wake frames by removing duplicate drain surfaces and avoiding managed dispatch.

## Decision 8 - Blackbox and Homeostasis Cap

Problem: The wake system had no 300-frame high-level heartbeat and no stress response; a bad velocity or origin-shift state could poison shader globals without postmortem evidence.

Solution: Added `WakeBlackBox` DataVault storage with 300 entries. Each publication writes active count, slot cap, strongest wake, generation, AUP shift sequence, stress, low-tier flag, and hash. Invalid/NaN input writes `Docs/AgentLogs/Dump_INTERACTIVE_WAKE_VFX.bin`. Slot limit resolves to 4 when low tier or `SystemStress01 > 0.8`, otherwise 16.

Rejected Alternatives: Debug logs are allocation/noise and do not satisfy crash forensics. Always publishing 16 slots on mobile would spend GPU/CPU budget where the visual return is weakest.

Scalability potential: Toaster mode uses 4 wake slots and radial fake data. PC God-Mode keeps 16 slots and exposes direction/radius for vortex curvature, silt, and normal perturbation.

Hardware Impact: Blackbox ring write is estimated 1-3 us/frame. Stress cap can save 6-18 us/frame downstream on weak hardware or thermal spikes by limiting shader/compute wake loops.

## Decision 9 - Compile Wall After Kernel Pass

Problem: Repeated `dotnet build .\Hecton8.Core.csproj -v:minimal` attempts still fail after wake kernel work.

Solution: Stopped compile-chasing after dependency wall evidence. Sampled blockers are outside wake ownership: `DiegeticGyroCompassRuntime`, `HomeostasisBrain`, `LockstepStateValidator`, `PickupItem`, and `TetherSignals`. A project-file include experiment widened unrelated failures and was reverted.

Rejected Alternatives: Renaming unrelated visor methods, filling missing homeostasis fields, or patching UI/navigation from the VFX wake prompt would violate domain ownership. Keeping the bad `.csproj` include change would widen the wall.

Scalability potential: No direct runtime change. Preserves the wake kernel while Integrator repairs unrelated compile state.

Hardware Impact: 0 us/frame. Avoided destabilizing non-wake systems in pursuit of a false green build.
