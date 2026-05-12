# COMBAT_DAMAGE Rationale

## Session Facts

Problem: Batch source path shifted during the run; `CURRENT_BATCH.md` is now present and authoritative.
Solution: Re-extracted the `<AGENT_PROMPT id="COMBAT_DAMAGE">` block from `Docs/Tasks/CURRENT_BATCH.md` using CLI regex after task 3.
Rejected Alternatives: Continuing with stale `.txt` memory would violate anti-amnesia.
Scalability potential: No runtime impact.
Hardware Impact: 0 us frame cost.

Problem: `CombatDamageRuntime.cs` already existed and was modified before this pass.
Solution: Treat existing edits as shared-agent/user work and patch only the required combat behavior.
Rejected Alternatives: Reverting or rewriting the file would risk destroying concurrent work.
Scalability potential: Keeps implementation aligned to the current native queue/SoA shape.
Hardware Impact: Avoids regression churn; runtime estimate unchanged.

## Decisions

Problem: Player hit direction lost spatial readability when snapped or approximated on low settings.
Solution: Player targets use guarded `math.normalize` for incoming direction; non-player fauna remains dominant-axis.
Rejected Alternatives: Reusing rsqrt for player low LOD was faster but violated the assignment and made HUD feedback less readable.
Scalability potential: Low keeps fauna cheap; High preserves exact player feedback and richer wound presentation.
Hardware Impact: Player-only exact normalize is sub-1 us at 1024 queued hits on i3/MX350 because player targets are sparse.

Problem: High fidelity wounds need exact hit point and normal without hard binding to a decal system that another agent may own.
Solution: Added `SurfaceNormal` to `CombatDamageResult` and `ICombatDamageFeedbackReceiver` LOD callback. High path supplies exact normals; low path sends zero normal so receivers can pulse `_HitFlash`.
Rejected Alternatives: Direct call into a decal manager or material mutation inside the job; both create brittle cross-domain coupling or violate Burst constraints.
Scalability potential: Low/Middle get cheap vertex-color flash. High/Ultra can spawn deferred decals using result point and normal.
Hardware Impact: Low saves decal draw/CPU work. High pays one normal and 16 bytes per result slot.

Problem: Flora trait splicing is outside the combat hot loop but required by the batch.
Solution: Added a static 64-bit bitmask splice helper on the combat runtime so flora systems can merge parent masks without managed mutation state.
Rejected Alternatives: Creating a flora genetics dependency from combat; that would violate the simultaneous-agent boundary.
Scalability potential: Low/Middle perform cheap deterministic bit merge. High/Ultra can layer visual mutation response on the resulting mask.
Hardware Impact: Constant-time ALU only, effectively 0 us at frame scale.

Problem: Kinetic fallback damage had scalar magnitude but no player/fauna distinction.
Solution: Added kind-aware fallback: player uses exact `math.length` on the impulse vector when scalar magnitude is absent; fauna uses `lengthSq * rsqrt`.
Rejected Alternatives: Always using exact length or always trusting scalar magnitude; first wastes fauna cycles, second fails signals that only carry vectors.
Scalability potential: Toaster path keeps fauna impacts cheap; high-end player impact remains spatially precise.
Hardware Impact: Fauna swarm path avoids sqrt. Player path cost is sparse and below 1 us for expected burst volume.

Problem: Compile verification cannot complete because a world-domain file is missing methods unrelated to this patch.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies`; recorded the `ProceduralWreckGenerator.cs` dependency wall and continued per 3-strike protocol.
Rejected Alternatives: Editing world wreckage domain to force a green build; that is outside COMBAT_DAMAGE and would be architectural sabotage.
Scalability potential: No runtime change.
Hardware Impact: 0 us.

Problem: Tool melee impacts needed combat/audio/VFX fanout without coupling to soundscape or camera systems.
Solution: `ToolHitUtility.ApplyImpulse` now publishes `ImpactSignal` to `GlobalSignals` after the existing queued physics force.
Rejected Alternatives: Calling soundscape, camera, or VFX components directly; those systems already consume the signal corridor.
Scalability potential: Low/Middle can play cheap impact cues. High/Ultra can layer debris/audio from the same signal.
Hardware Impact: One NativeQueue enqueue per tool impact, below 1 us expected.

Problem: Weakspots and tail crippling require localized child trigger data but combat runtime must not own fauna limb authoring.
Solution: Added small interfaces for weakspot, limb health, and mobility scale. Tool hits resolve child trigger metadata; central damage packs weakspot/status bits; fauna consumes the mobility contract.
Rejected Alternatives: String tags, layer-name checks, or fauna-specific casts inside the Burst job. Those are brittle and not Burst-safe.
Scalability potential: Low gets x3 damage and one speed multiplier. High can pair the same metadata with wound decals and animation reaction.
Hardware Impact: Component lookup happens only at hit ingress; hot job path remains branchless for weakspot multiplier.

Problem: Poison diffusion needs spatial spread without `Physics.OverlapSphere` allocation or broad managed searches.
Solution: Dispatch uses `WorldSpatialHashGrid.CollectContactsNonAlloc` into a fixed 16-hit buffer and queues status-only toxic packets for the next frame.
Rejected Alternatives: `OverlapSphere`, LINQ, or list fanout. All risk allocations and unstable hit ordering.
Scalability potential: Low caps spread to 16 receivers. High/Ultra can increase presentation intensity via listeners without changing combat cost.
Hardware Impact: Bounded spatial hash query on status change only; no per-frame poison scan.

Problem: Suit armor values must reduce damage, but no stable Logistics armor-slot API exists in the current contracts.
Solution: Added `SyncTargetProtection` as the decoupled ingestion point for armor and shield SoA sums. Runtime job subtracts the synced armor value.
Rejected Alternatives: Inventing methods on `ILogisticsService` or reading suit components directly from combat.
Scalability potential: Low stays one subtract per hit. High/Ultra can compute richer armor slots externally and push the same sum.
Hardware Impact: Sync is off-hot-path; damage pass cost is one subtract.

Problem: Blood scent belongs to ecosystem/world systems but must originate from combat wounds.
Solution: Combat job marks fauna wound results with `BloodScent`; managed dispatch resolves world position and writes to `ChemicalInfluenceGrid`.
Rejected Alternatives: Calling the scent grid from Burst or scanning fauna after damage.
Scalability potential: Low gets sparse scent pings. High/Ultra can amplify Eco-Director behavior from the same grid value.
Hardware Impact: Only wounded fauna results pay managed side-effect cost.

Problem: Compile verification surfaced obsolete `GetInstanceID()` warnings from the impact signal patch.
Solution: Replaced impact body ids with the project `EntityId` path.
Rejected Alternatives: Ignoring warnings; AGENTS requires production-grade integration.
Scalability potential: No runtime behavior change.
Hardware Impact: 0 us practical difference.

## OMEGA POLISH CHANGES

Problem: Poison diffusion could waste queue capacity when multiple colliders from one registered target appeared in the 2m spatial hash result.
Solution: Added `_poisonDiffusionTargetIds` fixed scratch buffer and skipped duplicate or unregistered targets before queueing spread packets.
Rejected Alternatives: Managed `HashSet<int>`, LINQ `Distinct`, or accepting duplicate status packets. All violate zero-GC or waste the 1024 signal budget.
Scalability potential: Low/Middle keep poison spread bounded to 16 unique targets. High/Ultra can still add richer VFX from status listeners.
Hardware Impact: Adds at most 120 integer comparisons per poison diffusion burst; saves redundant queued packets under multi-collider fauna.

Problem: Honest calculations audit.
Solution: Kept exact `math.normalize` only where the prompt requires player spatial awareness and high ricochet normals. Fauna hit direction uses dominant-axis. Fauna kinetic fallback uses `lengthSq * rsqrt`. Weakspot uses branchless `math.select`. Health fraction uses precomputed reciprocal.
Rejected Alternatives: Full exact normalize/sqrt for every entity and every LOD. That buys no gameplay readability on fauna swarms.
Scalability potential: Low path is LUT + dominant axis + flash. High/Ultra path enables exact normal/point result data for deferred decals and ricochet response.
Hardware Impact: Low path stays table lookup and bitmask math; high path pays exact math only behind `ResolveRuntimeMathLod`.

Problem: Zero-GC purge.
Solution: No `foreach`, LINQ, `string.Format`, `OverlapSphere`, `OnCollisionEnter`, or hot-path managed collection allocation was introduced in combat runtime. New arrays are static cold scratch with explicit COLD ALLOC comments. Tool impact signal uses struct packets and project `EntityId`.
Rejected Alternatives: Managed lists for poison, physics overlap, or direct presentation component references.
Scalability potential: Queue and scratch sizes are hard capped. Visual overkill remains listener-side.
Hardware Impact: Runtime additions are bounded and data-oriented.

Problem: Silo violation audit.
Solution: Cross-domain edits are limited to `ToolHitUtility` for combat ingress/ImpactSignal publish and `FaunaBrain` mobility receiver consumption. Both are direct requirements for melee impact and tail crippling. All other interactions stay behind interfaces, `GlobalSignals`, or existing world grids.
Rejected Alternatives: Editing Logistics contracts, decal managers, or Eco-Director internals.
Scalability potential: Systems can consume contracts independently without compile-time coupling to future implementations.
Hardware Impact: No extra per-frame polling.

Final Git Diff:
- `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs`: native damage queue extensions, exact player direction, high/low feedback result data, status bits, poison diffusion, armor/shield sync, reciprocal health accessor.
- `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs.meta`: restored Unity `MonoImporter` metadata while preserving GUID.
- `Assets/_Project/Scripts/ToolHitUtility.cs`: localized weakspot/status packing, melee `ImpactSignal` publish, tail-cripple mobility hook.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`: consumed combat mobility modifier for tail crippling. Note: this file already contained concurrent uncommitted fauna LOD/hit-flash edits; only the mobility hook was added for COMBAT_DAMAGE.
- `Docs/Tasks/Status_COMBAT_DAMAGE.md`, `Docs/AgentLogs/Rationale_COMBAT_DAMAGE.md`, `Docs/AgentLogs/LOG_COMBAT_DAMAGE.md`: evidence and reporting.

Build Health:
`dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -v:q /nologo /p:UseSharedCompilation=false /p:BuildInParallel=false` ends with 0 warnings and 3 errors in `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs`: ambiguous `InteractionSignal` at lines 3049 and 4666, plus `WreckIntegritySignalProxy` missing `IInteractionSignalConsumer.ApplyInteractionSignal(in InteractionSignal, Vector3)`. This is outside COMBAT_DAMAGE.
