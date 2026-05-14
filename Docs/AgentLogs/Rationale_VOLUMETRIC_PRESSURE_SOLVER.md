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
