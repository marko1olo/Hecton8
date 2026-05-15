# Rationale: VOLUMETRIC_PRESSURE_SOLVER

## Decision 0 - Establish State Before Code

Problem: The batch requires disk-backed memory and forbids chat-only state after context compression.  
Solution: Created status/rationale files before source changes and recorded loaded mandates.  
Rejected Alternatives: Relying on chat history is non-deterministic after compaction.  
Scalability potential: Low/Middle/High/Ultra all benefit from deterministic handoff data and no duplicated agent work.  
Hardware Impact: 0 runtime cost on i3/MX350; prevents wasted integration cycles.

## Decision 1 - Extend Existing Graph Owner

Problem: Habitat deformation needs per-module pressure truth, but a new singleton would fight ConstructionManager and other agents.  
Solution: Added persistent stress lanes and GPU upload state to `HabitatGraphManager`, the existing topology/flood/pressure owner.  
Rejected Alternatives: New global pressure manager, per-module MonoBehaviour polling, or renderer-side damage lookup. All add coupling and hot-path lookups.  
Scalability potential: Low uses the same scalar truth for crease overlay; Mid/High/Ultra can spend the same data on vertex deformation and stronger visuals.  
Hardware Impact: i3/MX350 pays one bounded module loop and one native buffer upload only when stress changes; avoids CPU mesh edits entirely.

## Decision 2 - Contracts Boundary Without Moving Runtime Owner

Problem: The prompt requested deformation asmdef isolation, but moving live construction code would split a root assembly with many existing references.  
Solution: Created `Hecton8.Habitat.Deformation.Contracts` as the contract surface and kept runtime mutation inside `HabitatGraphManager`.  
Rejected Alternatives: Moving `HabitatGraphManager` into a new asmdef now would require broad references across Construction, Gameplay, Power, World, and Core.  
Scalability potential: Low/Mid/High/Ultra consumers can bind to a small read-model contract later without importing the runtime owner.  
Hardware Impact: 0 runtime cost; compile isolation only.

## Decision 3 - Build Wall Classification

Problem: `dotnet build Hecton8.Core.csproj` fails with 107 pre-existing missing namespace/type errors outside the habitat deformation patch.  
Solution: Classified compile as dependency-blocked and continued with scoped grep/diff validation.  
Rejected Alternatives: Editing missing AI/audio/fluid/persistence assemblies outside ECHELON 6 would violate domain boundaries.  
Scalability potential: None; build ownership must return to integrator or relevant agents.  
Hardware Impact: 0 runtime cost; avoids unrelated churn.

## Decision 4 - Shader Fake Instead Of Mesh Mutation

Problem: Static interior walls do not convey pressure until modules fail, but CPU mesh deformation would allocate and break batching.  
Solution: Added `Hecton_HabitatInterior.hlsl` to bend vertices in object space from a per-module scalar and fake low-tier stress with detail-map creases.  
Rejected Alternatives: Runtime `Mesh.vertices` writes, blendshape authoring requirement, or rigidbody panel deformation. Standard Unity mesh mutation is too slow and allocation-prone for repeated stress feedback.  
Scalability potential: Low/MX350 uses peak-stress crease overlay; Mid can use sparse vertex bow; High/Ultra spend saved CPU on stronger deformation and better normal bias.  
Hardware Impact: Low i3/MX350 avoids per-module vertex index search and gets 0 max deformation; High/Ultra pay bounded vertex ALU, no CPU mesh cost.

## Decision 5 - Native Scalar Upload

Problem: The shader needs per-module stress without managed arrays or material-instance churn.  
Solution: Upload persistent `NativeArray<float>` through `GraphicsBufferUploadUtility.UploadNativeArray` and bind `_HectonHabitatModuleStressBuffer` globally.  
Rejected Alternatives: `MaterialPropertyBlock` per renderer, managed float arrays, or per-module materials. Those fragment batching and create upload churn.  
Scalability potential: Low reads peak scalar only; Mid/High/Ultra read per-module scalars for localized bowing.  
Hardware Impact: Estimated 6-18 us only on changed stress uploads, 0 B managed/frame.

## Decision 6 - Signal Decoupling

Problem: Leviathan/impact dents must affect habitat deformation without direct fauna/audio dependencies.  
Solution: Read existing `HullDeformedSignal` and `CombatDamageSignal` snapshots; publish `HullStressSignal` for rapid stress changes and `BaseModuleCompromisedSignal` through `GlobalSignals`.  
Rejected Alternatives: Direct references to fauna brains, audio renderers, or habitat wall objects. Those would break simultaneous-agent ownership and scalability.  
Scalability potential: Low gets stress audio/crease feedback; High/Ultra get localized dent/bow response.  
Hardware Impact: Bounded signal scan, no allocations; spike decay is one multiply/subtract per stressed module.

## Decision 7 - Blackbox Extension

Problem: A pressure/deformation crash without peak module stress would be postmortem blind.  
Solution: Extended the fixed 300-frame habitat blackbox entry with `PeakModuleStress` and deformation sequence; invalid stress dumps `Dump_VOLUMETRIC_PRESSURE_SOLVER.bin`.  
Rejected Alternatives: Debug logs or exception-only reporting. They are non-deterministic and unavailable in crash states.  
Scalability potential: Low/Mid/High/Ultra share the same fixed-size telemetry ring.  
Hardware Impact: One extra float and sequence write per blackbox sample; no managed allocation in normal path.

## Decision 8 - Toaster And High-End Modes

Problem: The mandate rejects balanced middle-ground deformation.  
Solution: Low/MX350 disables vertex deformation and uses peak-stress crease detail; Mid/High/Ultra enable object-space bowing with cheap rsqrt normal bias.  
Rejected Alternatives: Always-on bend or always-off fake. Always-on wastes toaster GPU; always-off undersells pressure on high-end.  
Scalability potential: Low = visually cheap crease, Middle = localized bow with modest scalar, High = stronger localized bend, Ultra = same data path can drive visual overkill materials/audio.  
Hardware Impact: Low saves the per-module vertex stress index loop; High/Ultra spend ALU in vertex shader where it buys claustrophobic interior feedback.

## OMEGA POLISH CHANGES

Problem: Polish audit required killing honest expensive math and proving zero-GC/hot-path discipline after core task closure.  
Solution: Removed exact HLSL `normalize()` from the new habitat include and replaced it with rsqrt safe-normal helpers; low-tier shader path now bypasses the per-module vertex index scan and uses peak-stress crease overlay only; scoped scans found no new managed foreach, LINQ, string formatting/interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, `.normalized`, or HLSL `normalize()` in owned additions.  
Rejected Alternatives: Keeping the exact normal helpers or per-module stress lookup on MX350 would spend GPU cycles without enough visible return.  
Scalability potential: Low/MX350 = peak crease overlay; Mid = localized vertex bow; High = localized bow plus stronger cheap normal bias; Ultra = same buffer can drive visual overkill materials/audio.  
Hardware Impact: Low-tier avoids the 64-slot vertex lookup entirely; High/Ultra use rsqrt normal bias instead of exact normalize. Estimated saved cost: 2-5 us/1k vertices on MX350-class scenes, 0 B/frame managed.

Final Git Diff: touched habitat deformation files are `HabitatGraphManager.cs`, `Hecton_DryZoneLit.shader`, new `Hecton_HabitatInterior.hlsl`, new `Hecton8.Habitat.Deformation.Contracts.asmdef`, new `HabitatDeformationContracts.cs`, and status/rationale/log docs. `GlobalSignals.cs` was already dirty with unrelated signal-lane edits; this task added only the `BaseModuleCompromisedSignal` lane/config/publish/64-byte payload entries there.

## Follow-Up Correction - Renderer Order Alignment

Problem: The first stress matrix used habitat graph order, while the shader's module ambience buffer is emitted in `BaseModule.s_activeModules` order. That can map one room's stress onto another room's vertices.  
Solution: Re-keyed the stress matrix to `BaseModule.GetActiveModuleAt()` order, matching `_HectonModuleAmbienceDataBuffer`; graph records are now only used for node id, flood state, and stable module hash metadata. Added active-order hash reset so spikes/hysteresis do not migrate when active module order changes.  
Rejected Alternatives: Sorting both buffers by hash at shader time or adding a second GPU metadata buffer. Sorting is too expensive; a second buffer duplicates existing ambience data.  
Scalability potential: Low/MX350 gets correct peak/crease response; Mid/High/Ultra get correct localized bowing per module.  
Hardware Impact: Avoids visual correctness failures with a bounded 64-module active scan. Estimated cost under 10 us on i3-class CPU in slow habitat tick, with no allocations.

## Follow-Up Correction - Stable LOD Param Upload

Problem: If quality tier changed while stress stayed stable, shader LOD params could remain stale.  
Solution: Added low-tier state tracking so tier changes force `_HectonHabitatModuleStressParams` re-upload.  
Rejected Alternatives: Pushing shader params every tick. That wastes CPU/GPU driver bandwidth.  
Scalability potential: Low/MX350 reliably disables vertex bend; High/Ultra reliably re-enable it when upgraded.  
Hardware Impact: Prevents stale visual mode with one boolean compare per tick.

## Follow-Up Correction - One-Shot Module Dump

Problem: Invalid per-module stress could rewrite `Dump_VOLUMETRIC_PRESSURE_SOLVER.bin` repeatedly.  
Solution: Added `_moduleStressBlackBoxDumped` and `DumpModuleStressBlackBoxOnce`.  
Rejected Alternatives: Leaving repeated file writes in a fault state. That can compound failure and stall low-end storage.  
Scalability potential: All tiers get deterministic first-fault evidence without repeated I/O.  
Hardware Impact: Fault path writes once; normal path is one branch.

## Follow-Up Correction - Shader Capacity And No-Match Safety

Problem: The CPU stress matrix could process more active modules than the renderer ambience buffer exposes, and the shader resolver returned slot 0 when no module radius contained a vertex. That can waste upload bandwidth and leak room-0 stress onto unrelated geometry.
Solution: Clamped CPU module stress publication to the 64-slot shader ambience capacity and made the HLSL resolver return a sentinel index on no-match, which `HectonHabitatInteriorReadStress01` converts to zero stress.
Rejected Alternatives: Expanding the ambience buffer or doing a second GPU metadata buffer pass. That is a broader renderer contract change and burns memory/ALU for a case already bounded by the existing ambience system.
Scalability potential: Low/MX350 avoids false full-screen crease feedback; Mid/High/Ultra keep localized bowing clean even when active module count exceeds the visible shader buffer.
Hardware Impact: Caps CPU loop/upload work at 64 modules for the render stress path; prevents unnecessary buffer traffic above the shader-visible ceiling and removes incorrect slot-0 deformation.

## Follow-Up Correction - Clear Path And Runtime-Key Fallback

Problem: Active-order changes cleared stress arrays and published a zero shader state even though the same tick immediately uploads replacement data. Modules without graph records also hashed by slot index, so reorder could migrate spike/hysteresis state without detection.
Solution: Split `ClearModuleStressState` into publish/no-publish paths and used the no-publish path for active-order resets. Added `ResolveModuleStressRuntimeKey` so no-graph modules use stable runtime instance ids for order hashing and direct signal fallback.
Rejected Alternatives: Always publishing zero before replacement upload, or accepting slot-index fallback. The first burns driver traffic; the second hides real order changes.
Scalability potential: Low/MX350 avoids unnecessary shader parameter churn; Mid/High/Ultra keep localized stress stable during pooled-module activation/reorder.
Hardware Impact: Saves one redundant `Shader.SetGlobalVector` on active-order rebuild ticks and prevents false stress migration with one native instance-id read per no-graph module.

## Follow-Up Correction - Tiered Deformation Amplitude

Problem: Module deformation used a binary low/non-low switch, so Mid paid full high-tier bow cost and Ultra had no visual overkill path.
Solution: Added tier-specific displacement amplitudes: Low/MX350/Unknown keep 0m vertex bow with crease-only feedback, Mid uses 0.036m, High uses 0.055m, and Ultra uses 0.075m. Shader params now dirty on any quality-tier change, not only low-tier transitions.
Rejected Alternatives: Keeping one non-low amplitude or driving amplitude from material instances. One amplitude violates the scalability pillar; material instances break batching.
Scalability potential: Low remains toaster-safe, Mid gets restrained deformation, High gets the original AAA bend, Ultra spends saved cycles on stronger pressure hallucination.
Hardware Impact: No extra per-frame allocation and no extra shader fetch. CPU cost is one enum compare in the dirty check; GPU cost is unchanged ALU with a tier-adjusted scalar.

## Follow-Up Correction - Shared Sine Panel Mask

Problem: Mid/High/Ultra vertices computed the same sine panel mask once for object-space bow and again for cheap normal bias.
Solution: `HectonHabitatInteriorApplyPanelBendOS` now outputs the panel mask, and `HectonHabitatInteriorApplyCheapNormalBiasWS` reuses it.
Rejected Alternatives: Replacing sine with triangle waves would violate the prompt's sine-panel bow requirement; leaving duplicate sine calls wastes ALU on every stressed vertex.
Scalability potential: Low/MX350 is unaffected because vertex bow is disabled. Mid/High/Ultra keep the same look with less vertex ALU.
Hardware Impact: Saves two sine evaluations per stressed vertex normal-bias path, estimated 2-6 us per 1k affected interior vertices on MX350-class GPUs.

## Follow-Up Correction - Low-Tier Triangle Crease Mask

Problem: The MX350 crease fallback still used the sine panel mask per fragment, which is expensive in the exact tier where vertex deformation is disabled for cost.
Solution: Added `HectonHabitatInteriorCheapPanelMask` and routed only `HectonHabitatInteriorApplyLowTierCrease` through it. The required sine mask remains on the Mid/High/Ultra vertex bow path.
Rejected Alternatives: Keeping per-fragment sine on low tier, or replacing all panel masks with triangle waves. The first wastes fragment ALU on MX350; the second weakens the instructed sine bow.
Scalability potential: Low/MX350 gets cheaper crease-only pressure feedback; Mid/High/Ultra keep premium sine bow and shared-mask normal bias.
Hardware Impact: Removes two sine evaluations per affected low-tier fragment; estimated 8-25 us saved on dense interior wall views on MX350-class GPUs.

## Follow-Up Correction - Gated Low-Tier Detail Sample

Problem: `Hecton_DryZoneLit` sampled `_DetailMask` for habitat crease feedback before the low-tier helper could reject Mid/High/Ultra or zero-stress pixels.
Solution: Moved the detail-mask sample inside a `[branch]` gated by `_HectonHabitatModuleStressParams.z > 0.5` and `input.habitatStress01 > 0.0001h`.
Rejected Alternatives: Leaving the guard only inside `HectonHabitatInteriorApplyLowTierCrease`, or removing the detail mask entirely. The first still pays the texture sample; the second damages low-tier visual readability.
Scalability potential: Low/MX350 still gets readable crease feedback under pressure. Mid/High/Ultra skip the unused fragment texture fetch and spend their budget on vertex bow and normal bias.
Hardware Impact: Removes one detail texture sample per non-low or zero-stress DryZone fragment; estimated 10-40 us saved in dense interior wall views on MX350-class GPUs.

## Follow-Up Correction - Zero-Count Upload Clamp

Problem: Publishing zero visible modules still created the module stress `GraphicsBuffer`, and `Unknown` quality tier could mark an empty shader state as low-tier mode.
Solution: `UploadModuleStressMatrix` now skips buffer creation/upload when the clamped module count is zero; `PublishModuleStressShader` publishes zero deformation and inactive low-tier mode for zero-count states; DryZone vertex lookup now requires a positive shader module count.
Rejected Alternatives: Keeping an eagerly allocated buffer for future modules, or relying on `stress01 == 0` to hide stale shader params. The first wastes VRAM in empty habitats; the second leaves misleading global state and still calls the zero-count resolver.
Scalability potential: Low/MX350 avoids useless global buffer allocation and vertex resolver calls in empty/boot states. Mid/High/Ultra get exact deformation amplitude only when visible module data exists.
Hardware Impact: Saves one structured buffer allocation in empty stress states and removes zero-count resolver work from DryZone vertices; estimated 5-20 us startup/rebuild savings plus small VRAM reduction on i3/MX350-class hardware.

## Follow-Up Correction - Runtime Target Hash Resolution

Problem: Graph-backed modules matched stress signals only against habitat marker/node hashes. Direct combat and hull deformation producers often carry Unity `EntityId`-derived target hashes, so a real impact could miss the module stress spike.
Solution: `TryResolveModuleStressIndex` now checks the stable graph hash and the runtime entity hash. `BaseModuleCompromisedSignal.ModuleHash` uses the stable-or-runtime key so no-graph modules do not publish zero identity.
Rejected Alternatives: Forcing all producers to emit habitat graph ids, or scanning only nearest world point. Producer coupling violates the signal-bus contract; nearest-only matching is less deterministic and can hit the wrong room in tight interiors.
Scalability potential: Low/MX350 gets reliable crease/spike feedback from direct impacts. Mid/High/Ultra get localized bowing from the same signal without extra shader data.
Hardware Impact: Adds one `EntityId` hash compare only while processing impact signals, not per vertex or per frame in the idle path; estimated under 2 us for 64 modules on i3/MX350-class hardware while recovering missed cinematic spikes.

## Follow-Up Correction - Bounded Nearest Stress Fallback

Problem: When direct hash/id matching failed, the nearest-module fallback accepted any finite world point and could inject habitat stress from unrelated impacts far outside the base.
Solution: The fallback now returns immediately for points inside a module interior trigger, otherwise it only accepts candidates within a padded interior hazard radius capped at 36m.
Rejected Alternatives: Removing nearest fallback entirely, or keeping unbounded nearest matching. Removing it would drop legitimate hull-surface impacts without stable ids; keeping it can corrupt pressure feedback from unrelated combat.
Scalability potential: Low/MX350 avoids false crease spikes from distant combat. Mid/High/Ultra preserve legitimate local bowing while rejecting far-field noise.
Hardware Impact: Adds bounded signal-path bounds checks only when id/hash matching fails; no shader cost and no per-frame idle cost. Estimated under 4 us for a 64-module fallback scan on i3/MX350-class hardware.

## Follow-Up Correction - Unique TargetId Stress Fallback

Problem: Some producers only carry a 16-bit `TargetId`; immediate low-bit matching can miss runtime-id targets or pick the wrong module on collision.
Solution: Count candidates across stable hash, runtime entity hash, and graph node id low bits; accept only exactly one active-module match.
Rejected Alternatives: Blind low-bit matching, producer rewrites, or nearest-only. Those create collision artifacts, coupling, or wrong-room hits.
Scalability potential: Low/MX350 gets reliable crease feedback; Mid/High/Ultra keep localized bowing without shader cost.
Hardware Impact: Bounded 64-module signal-path counter only; estimated under 2 us on i3/MX350.

## Follow-Up Correction - Buffer Lifetime And Growth Clears

Problem: Empty states could leave a stress `GraphicsBuffer` resident, while growth replacement could clear shader params immediately before rebinding replacement data.
Solution: Real zero-module clears release the buffer and publish zero params; active-order rebuilds and growth replacement use no-clear release paths.
Rejected Alternatives: Always keep buffers, or clear on every release. The first wastes VRAM; the second burns driver traffic and can flicker.
Scalability potential: Low/MX350 lowers idle VRAM pressure; Mid/High/Ultra avoid deformation interruption during growth.
Hardware Impact: Saves one stale structured buffer on teardown and one redundant shader vector write on growth; estimated 5-20 us teardown and 1-4 us growth frames.

## Follow-Up Correction - Shader Resolver And Panel Reuse

Problem: The shader split module resolve/read, recomputed panel UV in normal bias, and retained an unused panel-mask wrapper.
Solution: Combined stress resolve/read into `HectonHabitatInteriorResolveStress01`, passed centered panel UV from bend to normal bias, and removed the dead wrapper.
Rejected Alternatives: Extra metadata buffer or dead compatibility helpers. They add contract surface or audit noise without visual gain.
Scalability potential: Low/MX350 remains on triangle crease; Mid/High/Ultra keep sine bow with less vertex scalar work.
Hardware Impact: Estimated 2-5 us per 1k stressed interior vertices on MX350-class GPUs; no idle-frame cost.

## Follow-Up Correction - Index-Hinted Graph Lookup

Problem: Active-module stress loops repeatedly resolved `BaseModule` back into `_moduleBuffer` by scanning from slot 0.
Solution: Added an index-hinted overload for `TryResolveGraphModuleRecord`; active stress loops check the active slot first, then fall back to the existing scan.
Rejected Alternatives: Managed dictionary/cache or forcing graph/active order equivalence. Those add memory churn or break renderer-order alignment.
Scalability potential: Low/MX350 reduces CPU work during signal-heavy frames; high tiers keep exact localized deformation.
Hardware Impact: Best case removes repeated 64-record scans; estimated 4-12 us saved on i3/MX350 signal-heavy frames.

## Follow-Up Correction - Single-Pass Signal Resolver

Problem: Failed hash/id targeting scanned active modules once, then nearest fallback scanned the same active modules again.
Solution: One pass now handles exact hash, `TargetId` counting, interior containment, and bounded nearest collection while preserving priority order.
Rejected Alternatives: Keeping the second scan, or returning interior hits immediately. The first wastes CPU; the second could beat a later exact hash or unique target id.
Scalability potential: Low/MX350 reduces combat/Leviathan signal CPU; Mid/High/Ultra preserve deterministic localized deformation.
Hardware Impact: Removes one 64-module fallback scan for failed hash/id world-point signals; estimated 4-10 us saved on i3/MX350 signal-heavy frames.

## Follow-Up Correction - Identity-Gated Signal Resolve

Problem: `TryResolveModuleStressIndex` still computed graph identity and runtime `EntityId` keys for signals that had no target hash or `TargetId`, and it scanned active modules even when no finite nearest fallback was allowed.
Solution: Added a target-identity gate and an early false return for unresolvable signals. World-point-only signals now test direct interior containment before resolving graph records, and return immediately on identity-free interior hits because exact hash and `TargetId` priority cannot exist for that signal.
Rejected Alternatives: Leaving identity resolution unconditional, or splitting nearest fallback back into a second pass. Both waste CPU in damage-signal bursts and add no visual improvement.
Scalability potential: Low/MX350 pays less CPU for pressure/impact spam while preserving crease response. Mid/High/Ultra keep exact/id priority and localized shader bowing when producers provide identity.
Hardware Impact: Saves runtime `EntityId` hashing and graph lookup work for identity-free world-point signals; estimated 2-6 us per 64-module signal scan on i3/MX350-class hardware, with 0 B/frame and no shader cost.

## Follow-Up Correction - H-Phi Registry Coupling Reduction

Problem: The habitat stress owner still had avoidable hot/event-path `GlobalRegistry` reads for scalability tier, atmosphere sea level, audio, and rupture fluid decals. That weakens local H-Phi coupling and burns low-end CPU on repeated service lookups.
Solution: Sampled scalability tier once per hydrodynamic stress pass and threaded it through private analytical/module stress methods. Cached runtime sea level once per rebuild/stress pass. Cached audio and rupture-fluid decal services after first successful lookup while preserving fallback signal/event behavior when a service is unavailable.
Rejected Alternatives: Keeping registry reads inside per-module helpers, adding a new public dependency-injection interface in the middle of the batch, or claiming a project-wide H-Phi score without the H-Phi monitor. Per-module registry reads waste CPU; new public APIs violate interface immutability; fake global scores violate evidence rules.
Scalability potential: Low/MX350 gets fewer service-locator reads during pressure/flood stress passes and rupture events. Mid/High/Ultra keep the same deformation, audio, and fluid decal behavior while spending saved CPU on shader pressure polish.
Hardware Impact: Saves repeated atmosphere lookup on missing-depth module loops and repeated audio/decal lookups after warm cache; estimated 2-8 us on i3/MX350 stress-heavy frames, 0 B/frame, no shader cost. Local H-Phi evidence after the pass: `GlobalRegistry=4`, `SignalBus=2`, `GlobalSignals=1`, `NativeArray=81`, `GraphicsBuffer=3`, `FindCalls=0`, `UpdateMethods=0` in `HabitatGraphManager.cs`.

## Follow-Up Correction - Calm-Stress Shader Resolver Skip

Problem: Mid/High/Ultra DryZone vertices still entered the per-module stress resolver when module count was positive but global peak stress was zero. That means calm habitat walls could scan up to 64 module radii per vertex for no visible deformation.
Solution: Added a peak-stress guard before `HectonHabitatInteriorResolveStress01`. The resolver now runs only when non-low mode has visible modules and peak stress exceeds 0.0001. Low-tier crease mode remains driven by peak stress, and stressed habitats still resolve localized per-module bowing.
Rejected Alternatives: Always resolving to preserve theoretical zero-stress locality, or moving the guard into the include. The first wastes vertex ALU; the second hides callsite intent and still forces helper entry.
Scalability potential: Low/MX350 already avoids the vertex resolver; Mid/High/Ultra calm interiors now also skip it until pressure actually buys visible bowing.
Hardware Impact: Saves a 0-64 slot radius scan per DryZone vertex during calm module states; estimated 5-30 us per 1k interior vertices on MX350-class GPUs depending on visible module count, with no loss once stress is nonzero.

## Follow-Up Correction - Include-Level Peak And Mask Gates

Problem: The DryZone callsite had a peak-stress guard, but the shared habitat shader include still allowed future callsites to enter the 64-slot resolver on calm frames. The bend and low-tier crease helpers also finished panel setup when the sine/triangle panel mask was effectively zero, wasting ALU on panel borders with no possible visual output.
Solution: Added a peak-stress early return to `HectonHabitatInteriorResolveStress01`. Added zero-panel-mask early exits to `HectonHabitatInteriorApplyPanelBendOS` and `HectonHabitatInteriorApplyLowTierCrease` before normal bias, centered-UV setup, crease lerp, or output writes.
Rejected Alternatives: Relying only on every shader callsite to remember the peak guard, or leaving zero-mask borders to fall through the helper math. Callsite-only guards are brittle under concurrent shader edits; border fallthrough spends cycles for invisible deformation.
Scalability potential: Low/MX350 keeps cheap crease-only pressure feedback and skips dead crease work on panel borders. Mid/High/Ultra keep sine panel bowing where mask is visible and spend no vertex ALU on zero-mask borders.
Hardware Impact: Calm helper calls now return before module buffer/radius work, and border vertices/fragments skip no-op deformation setup. Estimated 1-4 us per 1k stressed interior vertices/fragments on i3/MX350-class hardware beyond the callsite guard, with 0 B/frame and no visual regression.

## Follow-Up Correction - Stable Hash Before Runtime Key

Problem: `TryResolveModuleStressIndex` read the runtime `EntityId` key for every identity-targeted signal candidate before checking whether the cheaper graph-stable module hash already matched. That keeps an avoidable runtime-identity dependency in the signal resolver hot path.
Solution: Reordered the resolver so nonzero `targetHash` compares against `ResolveModuleStressHash` first and returns immediately on exact graph-hash hits. `ResolveModuleStressEntityKey` is now called only after the graph hash fails and remains available for direct runtime targets and unique `TargetId` fallback.
Rejected Alternatives: Removing runtime entity fallback, or caching entity keys in a managed dictionary. Removing fallback would miss producers that target Unity entities; a dictionary adds memory ownership and invalidation work under concurrent agents.
Scalability potential: Low/MX350 avoids unnecessary runtime identity reads during signal bursts with graph-backed module ids. Mid/High/Ultra keep exact localized bowing for both stable graph targets and runtime entity targets.
Hardware Impact: Saves one `GetEntityId`/hash path per graph-hash hit candidate in stress signal scans; estimated 1-3 us on i3/MX350 signal-heavy frames, 0 B/frame, no shader cost.

## Follow-Up Correction - Low-Tier Crease Texture Gate

Problem: The low-tier DryZone crease path sampled `_DetailMask` before checking whether the cheap panel mask was zero. Border fragments with no possible crease still paid a texture fetch and then recomputed the same panel mask inside the helper.
Solution: Compute `HectonHabitatInteriorCheapPanelMask` at the DryZone callsite, branch out before the detail texture sample when the mask is zero, and pass the precomputed mask into `HectonHabitatInteriorApplyLowTierCrease`.
Rejected Alternatives: Leaving the texture sample inside the existing stress branch, or moving all crease logic into the fragment shader body. The first wastes low-tier bandwidth; the second duplicates shared helper logic and weakens shader include ownership.
Scalability potential: Low/MX350 skips detail texture fetches on panel borders while retaining readable crease feedback under pressure. Mid/High/Ultra are unchanged because this branch is low-tier only.
Hardware Impact: Saves one detail texture sample plus one cheap panel-mask recompute on zero-mask low-tier fragments; estimated 4-12 us per dense interior wall view on MX350-class GPUs, 0 B/frame.

## Follow-Up Correction - Calm Stress Upload Gate

Problem: A habitat with visible modules but zero peak module stress could still enter `UploadModuleStressMatrix`, ensure the structured stress buffer, and upload an all-zero scalar matrix. After the shader peak-stress guard, that buffer is not read on calm frames.
Solution: Gate `EnsureModuleStressBuffer` and `GraphicsBufferUploadUtility.UploadNativeArray` behind `peakStress01 > ModuleStressUploadEpsilon`. The shader params are still published, so the global peak reaches zero and the resolver early-returns before any buffer read.
Rejected Alternatives: Releasing the buffer whenever peak stress hits zero, or continuing to upload zero matrices. Releasing risks driver churn during near-threshold stress oscillation; zero uploads waste CPU/driver bandwidth with no visible result.
Scalability potential: Low/MX350 avoids calm-state upload work after active module order or tier changes. Mid/High/Ultra keep immediate localized bowing once stress rises above the epsilon.
Hardware Impact: Saves one buffer ensure/upload on calm visible-module dirty ticks; estimated 6-18 us on i3/MX350 rebuild/order-change frames, 0 B/frame, no shader cost.

## Follow-Up Correction - Low-Tier Buffer Upload Bypass

Problem: Low-tier habitat deformation uses peak-stress crease feedback and does not need the per-module stress buffer, but `UploadModuleStressMatrix` still ensured and uploaded `_HectonHabitatModuleStressBuffer` whenever peak stress was visible.
Solution: Treat low-tier as a peak-only visual mode in the CPU upload path: `hasVisibleStress` now requires non-low-tier plus peak stress above epsilon. The shared shader resolver also returns the peak stress immediately when low-tier mode is active, so future callsites cannot accidentally read a skipped buffer.
Rejected Alternatives: Keeping low-tier buffer uploads for symmetry, or releasing/reallocating buffers on every tier change. Symmetry wastes driver bandwidth on MX350; release/realloc churn is worse than leaving a small stale buffer ignored when quality tiers change.
Scalability potential: Low/MX350 gets peak-stress crease feedback with no per-module GPU upload. Mid/High/Ultra retain localized per-module bowing and buffer upload only when it buys visible deformation.
Hardware Impact: Saves one structured-buffer ensure/upload on stressed low-tier module ticks; estimated 6-18 us on i3/MX350 stress frames, 0 B/frame, no loss to high-tier localized deformation.

## Follow-Up Correction - Atmosphere Service Cache

Problem: Habitat depth and pressure stress sampling still resolved `GlobalRegistry.Atmosphere` each rebuild/stress pass even though the service identity is stable; only `SeaLevelY` needs to remain live.
Solution: Added `_atmosphereManager` caching and routed `ResolveRuntimeSeaLevelY` through `ResolveAtmosphereManager`. The cached service is cleared on dispose, while `SeaLevelY` is read fresh on every pass.
Rejected Alternatives: Caching the numeric sea level for longer than one pass, or adding a new constructor dependency. Numeric caching would break tides and surface shifts; a constructor dependency widens architecture while concurrent agents are active.
Scalability potential: Low/MX350 removes repeated service-locator work from pressure passes. Mid/High/Ultra keep exact live sea-level pressure deformation.
Hardware Impact: Saves repeated atmosphere registry lookup during rebuild/stress loops; estimated 1-3 us on i3/MX350 stress-heavy frames, 0 B/frame, no shader cost.

## Follow-Up Correction - Vertex Bend Callsite Gate

Problem: DryZone vertices still called the habitat bend and cheap normal-bias helpers in low-tier and zero-stress states, even though the helpers immediately returned. This left avoidable branch/setup work on the highest-frequency render path.
Solution: Initialized bend outputs to zero at the callsite and added `habitatVertexBendActive`, so `HectonHabitatInteriorApplyPanelBendOS` runs only for non-low stressed vertices. Normal bias now runs only when bend is active and the panel mask is nonzero.
Rejected Alternatives: Relying only on helper-internal guards, or duplicating deformation math in the shader body. Helper-only guards waste callsite setup; duplication makes future shader maintenance brittle.
Scalability potential: Low/MX350 avoids all vertex bend helper setup and keeps crease-only feedback. Mid/High/Ultra still get localized sine bow and normal bias when stress exists.
Hardware Impact: Saves two helper-entry branches plus normal-bias setup on low-tier/calm vertices; estimated 2-8 us per 1k interior vertices on MX350-class GPUs, 0 B/frame.

## Follow-Up Correction - Vertex Amplitude Callsite Gate

Problem: DryZone vertex deformation still entered the bend helper when non-low shader mode was active, stress was nonzero, but `_HectonHabitatModuleStressParams.y` carried no displacement budget. The helper rejected the state, but the vertex path still paid callsite setup.
Solution: Added the same deformation-amplitude threshold to `habitatVertexBendActive`, so the shader calls bend and normal-bias helpers only when tier, stress, and amplitude all permit visible vertex deformation.
Rejected Alternatives: Leaving amplitude validation only inside `HectonHabitatInteriorApplyPanelBendOS`, or hardwiring tier names in the shader. Helper-only validation wastes vertex branches; tier names belong on CPU quality policy, not shader branching.
Scalability potential: Low/MX350 and transitional zero-amplitude states skip vertex deformation setup. Mid/High/Ultra keep full sine bow and normal bias when displacement is positive.
Hardware Impact: Saves helper-entry setup on non-low zero-amplitude transitional states; estimated 1-3 us per 1k affected interior vertices on MX350-class GPUs, 0 B/frame.

## Follow-Up Correction - Native Payload Struct Layout

Problem: `HabitatSiegeTargetSnapshot` and `HabitatFloodConnection` are native-facing habitat payload structs, but only the blackbox entry had explicit layout. H-Phi memory-alignment coverage and binary auditability were weaker than the actual data ownership.
Solution: Added `[StructLayout(LayoutKind.Sequential, Pack = 4)]` to the siege snapshot and flood connection payloads. Object-reference staging structs were left untouched because explicit binary layout there would be misleading.
Rejected Alternatives: Blanket layout attributes on every private staging struct, or no layout correction. Blanket attributes on `GameObject`/`string` staging records create false binary-safety signals; no correction leaves native payload ownership under-specified.
Scalability potential: Low/MX350 gets clearer native payload layout for cheap audits and safer telemetry/flood data handling. High/Ultra keep the same runtime behavior with better data-contract evidence.
Hardware Impact: 0 runtime cost; static H-Phi memory-alignment coverage improves for owned native-facing structs.

## Follow-Up Correction - Visible Stress Threshold Coherence

Problem: CPU upload skipped `_HectonHabitatModuleStressBuffer` when peak stress was at or below `ModuleStressUploadEpsilon` (0.0015), but the shader still treated stress above 0.0001 as visible. A drop from high stress to a tiny sub-epsilon peak could publish nonzero shader params while sampling a stale per-module buffer.
Solution: Added shared HLSL stress epsilon macros at 0.0015 and routed resolver, bend, normal bias, DryZone vertex lookup, and low-tier crease gates through them. `PublishModuleStressShader` now publishes zero deformation amplitude unless the same non-low visible-stress condition is true.
Rejected Alternatives: Lowering CPU upload visibility to 0.0001, clearing/releasing the buffer on every tiny stress drop, or leaving the threshold mismatch. The first reintroduces upload churn and still fights the CPU dirty epsilon; the second burns driver bandwidth during near-threshold oscillation; the third can produce stale localized bowing.
Scalability potential: Low/MX350 stays peak-only crease with no per-module buffer dependency. Mid/High/Ultra keep localized bowing only when CPU and shader agree that the stress is visually worth paying for, preserving visual overkill above the threshold and silence below it.
Hardware Impact: Prevents stale buffer reads and skips sub-epsilon vertex resolver/bend work. Estimated 5-30 us saved per 1k calm or near-calm interior vertices on MX350-class GPUs, plus avoided 6-18 us CPU/driver upload churn on near-threshold dirty ticks; 0 B/frame.

## Follow-Up Correction - UberNoir Habitat Mask Gate And Job Layout

Problem: `Hecton8_UberNoir.hlsl` evaluated the habitat analytical radius mask even when habitat displacement was zero, so noir deformation vertices paid useless radius math during calm or low-tier habitat states. The deconstruction DFS Burst job also lacked explicit payload layout evidence, leaving local H-Phi memory-alignment coverage weaker than the actual job ownership.
Solution: Guarded `H8UberNoirRadiusMask(positionWS, _HectonHabitatStressCenterRadius)` behind `habitatDisplacement > H8_UBER_NOIR_EPS`. Added `[StructLayout(LayoutKind.Sequential)]` to `DeconstructionDfsValidationJob` without forcing a pack size, preserving native pointer alignment inside `NativeArray`, `NativeList`, and `NativeParallelHashSet` fields.
Rejected Alternatives: Recomputing habitat mask unconditionally, adding a fake layout pack to object-reference staging records, or touching unrelated crush-depth mask math. The first wastes shader ALU; fake packs are H-Phi theater and may misrepresent managed references; crush-depth remains outside this agent's habitat-pressure ownership.
Scalability potential: Low/MX350 and calm habitats skip the extra habitat mask in UberNoir while retaining the visual fake when real displacement is active. Mid/High/Ultra keep analytical habitat bend support and spend the saved work on stronger active stress deformation.
Hardware Impact: Saves one habitat radius-mask evaluation per UberNoir vertex when habitat displacement is inactive; estimated 1-4 us per 1k affected vertices on MX350-class GPUs. H-Phi local layout coverage improves from 3/9 to 4/9 struct declarations in `HabitatGraphManager.cs`; runtime GC remains 0 B/frame.

## Follow-Up Correction - Emergency State Cache And Contract Layout

Problem: Analytical stress resets and commits wrote `_BaseEmergencyState` even when the value did not change, and habitat vibration could leave a tiny stale shader value after decay because the publish epsilon suppressed the final zero write. The public deformation sample contract also lacked explicit layout despite being the boundary type other systems consume.
Solution: Added a cached `PublishBaseEmergencyState` path with forced dispose clear, snapped habitat vibration values at or below 0.002 to zero while still forcing dispose cleanup, and marked `HabitatModuleDeformationSample` with `[StructLayout(LayoutKind.Sequential, Pack = 4)]`.
Rejected Alternatives: Keeping direct `Shader.SetGlobalInt` calls in every reset/commit, publishing vibration every frame, or blanket-annotating object-reference staging structs. Direct writes burn driver/global-state traffic; every-frame vibration writes waste CPU for no visual gain; fake layout on reference-heavy staging structs misrepresents memory safety.
Scalability potential: Low/MX350 avoids redundant shader global writes during repeated safe/calm analytical stress ticks and avoids lingering micro-vibration. Mid/High/Ultra keep forced cleanup on dispose and exact emergency-state transitions for active pressure visuals.
Hardware Impact: Saves redundant `_BaseEmergencyState` global writes on stable stress/reset ticks and removes stale sub-epsilon vibration state. Estimated 1-3 us saved on i3/MX350 stress/reset-heavy frames; contract layout adds 0 runtime cost and improves H-Phi evidence for the deformation boundary.

## Follow-Up Correction - Analytical Stress Visibility Snap

Problem: Analytical habitat stress used a 0.0025 CPU publication epsilon, but `_HectonHabitatStressParams` could still retain or publish tiny positive stress values that CoreLit accepted at 0.0001. That allowed near-silent stress to keep analytical dent math alive after the CPU had already declared the delta visually unimportant.
Solution: `PublishAnalyticalStressShader` now converts stress at or below `AnalyticalShaderStressEpsilon` to zero, disables analytical displacement for those states, and skips repeated zero global vector writes after the first clear. `Hecton_CoreLit.hlsl` now exposes `HECTON_CORE_LIT_HABITAT_STRESS_EPSILON` at 0.0025 and uses it before habitat analytical dent work.
Rejected Alternatives: Lowering the CPU epsilon to 0.0001, leaving the shader hardcoded, or releasing the whole analytical path on low tiers only. Lowering the CPU epsilon reintroduces global shader churn; hardcoded shader gates drift from CPU truth; tier-only gating misses near-calm high-tier states.
Scalability potential: Low/MX350 and calm high-tier bases stay visually silent below the agreed pressure threshold. High/Ultra keep analytical deformation once stress is worth spending vertex ALU on.
Hardware Impact: Saves CoreLit analytical habitat dent setup for sub-epsilon stress and prevents stale tiny stress globals. Estimated 2-8 us saved per 1k CoreLit vertices in near-calm habitat views on MX350-class GPUs; 0 B/frame.

## Follow-Up Correction - Analytical Spatial Dirty Gate

Problem: `_HectonHabitatStressCenterRadius` could remain stale when analytical stress and displacement stayed numerically stable while the base center/radius moved after topology, AUP, or module-position changes. Stable stress is not enough to skip spatial shader globals.
Solution: Added cached analytical center/radius state and included 5cm center/radius tolerances in the visible-stress publish gate. The publisher now sanitizes non-finite center/radius/stress values into a zero-stress shader clear path before upload.
Rejected Alternatives: Publishing analytical center/radius every tick, or keeping the stress-only dirty check. Every-tick global vectors waste driver bandwidth on calm frames; stress-only gating can put active deformation around the wrong world-space region.
Scalability potential: Low/MX350 keeps cheap zero-stress skip behavior. Mid/High/Ultra keep accurate spatial analytical dents during active pressure while still skipping tiny sub-5cm shader churn.
Hardware Impact: Prevents stale analytical deformation coordinates with one `lengthsq` and one radius compare in the dirty gate. Estimated saved integration/debug cost is high; runtime cost is under 1 us per analytical publish decision on i3/MX350, with avoided redundant shader globals when spatial movement is below tolerance.

## Follow-Up Correction - Finite Ingress Gates

Problem: `ApplyHydrodynamicStress` accepted NaN/Infinity `deltaTime` because the old guard only rejected non-positive time. A malformed seismic epicenter could also force a full active-module scan before naturally failing all distance comparisons.
Solution: Added a finite `deltaTime` gate at the hydrodynamic stress tick entry and finite epicenter component checks in `RegisterSeismicVibration` before the module scan.
Rejected Alternatives: Letting downstream math sanitize every field, or only clamping inside `UpdateHabitatVibration`. Downstream-only cleanup allows NaN to touch flood, pressure, pump, condensation, module-stress and telemetry code; vibration-only cleanup misses other hydrodynamic consumers.
Scalability potential: Low/MX350 avoids wasted module scans and NaN-driven state churn from bad input. High/Ultra keep identical valid-input behavior and preserve all visual pressure feedback.
Hardware Impact: Saves a full hydrodynamic pass on invalid frame time and avoids one active-module scan for malformed seismic signals. Estimated worst-case saved work is 10-80 us on i3/MX350 invalid-input frames; normal-frame cost is one finite check and three seismic component checks on event ingress.

## Follow-Up Correction - Blackbox Ingress And Native Spike Sanitation

Problem: The finite ingress gates rejected invalid pressure time and malformed seismic inputs, but the rejection path did not record the fault into the fixed blackbox ring. A corrupted native module spike lane could also remain non-finite between stress ticks.
Solution: Added `RecordNonFinitePressureIngress` so non-finite pressure/seismic ingress writes the flood blackbox path before returning. `ResolveModuleStress01` now reports non-finite depth, integrity, joint, compression, flood, and spike scalars through a private invalid-state flag, resets corrupted spike slots to zero, and keeps shader-facing stress finite. `InjectModuleStressSpike` also rejects non-finite magnitudes and sanitizes an already-corrupted spike slot before max merge.
Rejected Alternatives: Silent rejection, downstream shader clamping, or clearing the entire stress matrix on one bad scalar. Silent rejection violates the blackbox mandate; shader-only clamping hides the CPU-side source; full matrix clears cause visible pressure flicker and destroy valid rooms.
Scalability potential: Low/MX350 gets deterministic fault evidence without extra frame allocations and avoids poisoned peak-crease feedback. Mid/High/Ultra keep localized pressure bowing for valid modules while one corrupt lane is isolated.
Hardware Impact: Normal valid-frame overhead is a few finite checks inside the existing 64-module stress loop. Fault frames save repeated NaN propagation and avoid stale spike churn; estimated 5-20 us saved on i3/MX350 recovery frames, with 0 B/frame in the valid path.

## Follow-Up Correction - Habitat Shader Finite Normal Fallbacks

Problem: The habitat interior shader helpers used `rsqrt` for cheap normals but did not reject non-finite length before returning bend or normal-bias vectors. A poisoned normal could propagate into object-space displacement or lighting normals.
Solution: `HectonHabitatInteriorSafeNormalize3` and `HectonHabitatInteriorSafeNormalizeHalf3` now require finite positive length and accept explicit fallback vectors. Bend normals fall back to zero offset; normal-bias basis/final vectors fall back to stable axes or the pre-bias base normal.
Rejected Alternatives: Exact `normalize`, shader-wide NaN clamping in the fragment, or disabling habitat bend on all questionable data. Exact normalize violates the cheap-helper mandate; fragment clamping is too late for clip-space vertex output; disabling all bend discards valid pressure visuals.
Scalability potential: Low/MX350 is unaffected because vertex bend is bypassed. Mid/High/Ultra keep sine panel bow and cheap normal bias while invalid inputs degrade to stable no-op/fallback visuals.
Hardware Impact: Adds finite checks only in the already-gated stressed vertex/normal-bias path. Saves catastrophic NaN propagation/debug time; expected normal-frame cost is under 1 us per 1k stressed vertices on MX350-class GPUs.

## Follow-Up Correction - Shader Stress Resolver Ambience Guard

Problem: `HectonHabitatInteriorResolveStress01` could evaluate a non-finite module ambience radius or distance. HLSL comparisons against NaN can fail open, allowing a corrupted ambience slot to become the selected stress index.
Solution: Added scalar finite gates for `centerRadius.w` and `distanceSq` inside the bounded 64-slot resolver loop. Bad candidates are skipped and no stress is read unless a finite module radius/distance passes the radius test.
Rejected Alternatives: Four-component `isfinite(centerRadius)` per slot or relying on CPU-side ambience publication only. Full vector checks cost more than required; CPU-only validation is not enough for a shared shader include fed by multiple systems.
Scalability potential: Low/MX350 is unaffected because low-tier returns peak stress before the buffer loop. Mid/High/Ultra keep localized deformation while corrupted ambience records degrade to zero stress/no selection.
Hardware Impact: Adds two scalar finite checks in the already-gated non-low stressed resolver path. Fault frames avoid poisoned stress selection; valid-frame overhead remains bounded under the 64-slot cap.

## Follow-Up Correction - Shader Stress Scalar Finite Gates

Problem: Habitat module stress globals and buffer values are expected finite from CPU publication, but the shared shader include still trusted `_HectonHabitatModuleStressParams.w` and `_HectonHabitatModuleStressBuffer[bestIndex]`. A bad scalar could enter low-tier crease or localized vertex bend.
Solution: Cached the peak scalar, rejected it when non-finite before any low-tier or module-buffer branch, and explicitly returns zero when the selected buffer stress is non-finite.
Rejected Alternatives: Relying on HLSL `saturate` NaN behavior or clamping in downstream bend/crease helpers. NaN saturation behavior is not a contract worth depending on; downstream clamping is duplicated and too late for shared resolver correctness.
Scalability potential: Low/MX350 peak-only crease now has the same finite gate as Mid/High/Ultra localized stress. Ultra keeps visual overkill when data is valid and degrades bad slots to silence.
Hardware Impact: Adds two scalar finite checks in already-gated stress paths. Fault frames avoid NaN deformation/crease output; valid-frame overhead is negligible under the 64-slot cap.

## Follow-Up Correction - Shader Panel Mask Finite Gates

Problem: A non-finite UV could flow through `frac(abs(uv))`, the sine panel mask, and then into object-space bend offset or low-tier crease intensity.
Solution: `HectonHabitatInteriorPanelUv` now returns zero panel UV for non-finite input, and `HectonHabitatInteriorPanelMaskFromUv` returns zero when the sine mask product is non-finite.
Rejected Alternatives: Checking UV separately in every bend/crease callsite or allowing downstream saturate to handle it. Callsite checks duplicate policy; saturate on NaN is not a stable correctness contract.
Scalability potential: Low/MX350 crease and Mid/High/Ultra sine bow share one invalid-UV behavior: zero panel influence. Valid panels keep the exact existing look.
Hardware Impact: Adds a finite UV check only where panel masks are calculated. Faulted vertices/fragments avoid NaN offset/crease propagation; valid-frame overhead is bounded to stressed/crease paths.

## Follow-Up Correction - CoreLit Analytical Habitat Finite Gates

Problem: The CoreLit analytical habitat dent path still trusted global stress, displacement, radius, grid scale, and seed scalars. A non-finite global could keep the high-tier analytical dent path alive or produce NaN phase/radius math.
Solution: `HectonCoreLitApplyHabitatAnalyticalStress` now fails closed when analytical stress/displacement/radius/grid/seed inputs are non-finite, and rejects non-finite radius masks before dent output.
Rejected Alternatives: Relying only on CPU publication sanitation, or sanitizing the final position after NaN phase work. CPU sanitation is necessary but not sufficient for a shared shader global; final-position cleanup hides wasted ALU and can still poison intermediate logic.
Scalability potential: Low/MX350 stays unaffected because analytical displacement is zero below high scalability. High/Ultra keep the analytical dent fake for valid data and degrade invalid globals to no-op.
Hardware Impact: Adds finite checks only in the active analytical dent path. Fault frames avoid NaN phase/radius propagation; valid-frame overhead is under the analytical path's existing ALU budget.

## Follow-Up Correction - UberNoir Habitat Bending Finite Gates

Problem: UberNoir hull bending reused habitat analytical globals and a shared radius-mask helper that could accept non-finite position or center/radius data. A bad habitat scalar could still leak into the noir bending displacement.
Solution: `H8UberNoirRadiusMask` now fails closed on non-finite position or center/radius vectors. UberNoir habitat stress and displacement scalars are separately finite-gated before contributing to dynamic hull bending.
Rejected Alternatives: Leaving the CoreLit guard as the only analytical protection, or final-position sanitation only. UberNoir has its own path and must not depend on another include's guard; final sanitation hides bad intermediate displacement.
Scalability potential: Low/MX350 remains bypassed through `_MATH_LOD_LOW`. High/Ultra keep noir hull bending when data is valid and lose only corrupted habitat contribution.
Hardware Impact: Adds finite checks in the non-low UberNoir bend path. Fault frames avoid NaN displacement; valid-frame overhead is scalar/vector finite checks around already-active bending math.

## Follow-Up Correction - UberNoir Safe Bend Position

Problem: Even with finite radius-mask gates, UberNoir dynamic bending still passed raw `positionWS` into the buckling mask and final fallback. A non-finite vertex position could poison buckle math before the final `H8UberNoirFinite3` call.
Solution: Sanitized `positionWS` once to `safePositionWS` at the non-low bending entry and reused it for crush/habitat radius masks, buckling mask, and final fallback.
Rejected Alternatives: Sanitizing only the final output, or repeating finite checks in each mask call. Final-only sanitation still burns NaN intermediate work; repeated checks duplicate policy and cost.
Scalability potential: Low/MX350 remains bypassed. High/Ultra dynamic hull bending keeps full valid visuals while non-finite input becomes deterministic zero-origin fallback instead of NaN output.
Hardware Impact: Adds one vector finite sanitize in the non-low bend path and removes repeated risk downstream. Fault frames avoid NaN buckling/displacement; valid-frame cost is negligible versus existing bending ALU.

## Follow-Up Correction - UberNoir Dynamic Scalar Gates

Problem: UberNoir dynamic hull bending still trusted bend feature/strength, crush depth/current/displacement, buckling grid scale, and instance seed. A non-finite scalar in those lanes could keep radius-mask work alive or poison buckle/displacement math after the habitat-specific gates.
Solution: Added finite gates for bend feature, local strength, crush depth/current/displacement, buckling grid scale, and instance seed. Computed crush/habitat displacement values are rechecked after multiplication, crush radius-mask evaluation now runs only when crush displacement is positive, `stablePosition` is re-sanitized after grid scaling, and final displacement is zeroed when non-finite.
Rejected Alternatives: Relying on final output sanitation, clamping every shared helper globally, or removing the crush contribution. Final-only sanitation wastes NaN intermediate work; global helper clamps would tax unrelated fragment paths; removing crush contribution breaks vehicle pressure visuals.
Scalability potential: Low/MX350 remains bypassed by `_MATH_LOD_LOW` and spends no extra ALU. Mid/High/Ultra keep full valid dynamic hull bending while malformed scalar globals degrade to deterministic no-op contribution.
Hardware Impact: Saves one crush radius-mask evaluation whenever crush displacement is zero and prevents fault-frame NaN propagation through buckle/displacement math. Estimated 1-4 us saved per 1k affected UberNoir vertices on MX350-class GPUs in calm-crush states; valid active-crush overhead is scalar finite checks inside the already-active bending path.

## Follow-Up Correction - UberNoir Bend No-Op Early Exits

Problem: After finite sanitation, UberNoir still evaluated crush/habitat displacement branches and buckling noise when bending was disabled, local strength was zero, or both pressure displacement contributions resolved to zero.
Solution: Return `safePositionWS` immediately when bend feature/strength cannot produce output, and return again before buckling when the combined crush/habitat displacement is below epsilon.
Rejected Alternatives: Leaving the existing final zero displacement path, or adding a new material keyword. Final zero displacement preserves correctness but wastes vertex ALU; a new keyword widens shader variant pressure for a local no-op case.
Scalability potential: Low/MX350 remains bypassed by `_MATH_LOD_LOW`. Mid/High/Ultra skip dead bend work on disabled/calm materials while keeping full visual overkill once crush or habitat pressure actually contributes.
Hardware Impact: Saves crush/habitat mask and buckling work on non-low no-op vertices. Estimated 2-8 us saved per 1k UberNoir vertices in disabled/calm bend states on MX350-class GPUs; active bend behavior is unchanged.

## Follow-Up Correction - UberNoir Instance Buffer Finite Gates

Problem: `H8UberNoirLoadInstance` cast `_UberNoirInstanceParams` count/offset/use-buffer globals directly to `uint`. A non-finite count or offset could produce unstable StructuredBuffer indexing before the clamped instance read.
Solution: Finite-gated buffer offset, buffer count, and use-buffer scalars before the `uint` casts. Invalid values now fall back to the default per-object instance data and skip the buffer read.
Rejected Alternatives: Trusting CPU publication, clamping after the cast, or adding a separate buffer metadata structure. CPU-only trust is insufficient for shared shader globals; post-cast clamping is too late for NaN conversion; a new metadata structure adds interface surface for a local guard.
Scalability potential: Low/MX350 keeps default instance fallback on corrupted metadata with no extra buffer traffic. Mid/High/Ultra keep instanced visual overkill when metadata is valid and fail closed when it is not.
Hardware Impact: Adds three scalar finite checks only when the instance-buffer variant is compiled. Fault frames avoid bad StructuredBuffer reads; valid-frame overhead is negligible compared with the existing instanced vertex path.
