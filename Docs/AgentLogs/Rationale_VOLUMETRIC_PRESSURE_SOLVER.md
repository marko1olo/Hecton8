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

Problem: Some signal producers only carry a 16-bit `TargetId` from runtime or graph identity low bits. The previous resolver accepted graph node low bits immediately, missed direct runtime-id low-bit targets, and could pick the wrong module on a low-bit collision.
Solution: `TryResolveModuleStressIndex` now counts `TargetId` candidates across stable module hash, direct runtime `EntityId` hash, and graph node id low bits. It accepts the id path only when exactly one active module matches; ambiguous ids fall through to bounded world-point fallback or no match.
Rejected Alternatives: Blind immediate low-bit matching, requiring every producer to emit a full graph hash, or using nearest-only. Immediate matching creates collision artifacts; producer rewrites violate decoupled signal ownership; nearest-only is less deterministic in dense habitat interiors.
Scalability potential: Low/MX350 receives reliable crease feedback from truncated target ids without false-room spikes. Mid/High/Ultra keep localized bowing tied to the correct module and spend no extra shader cost.
Hardware Impact: Adds a bounded 64-module signal-path candidate counter only while resolving impact/deformation signals, with no idle-frame or vertex cost. Estimated under 2 us on i3/MX350-class hardware.

## Follow-Up Correction - Empty Stress Buffer Release

Problem: Zero-module stress states stopped allocating a new GPU stress buffer, but a previously allocated `_HectonHabitatModuleStressBuffer` could remain resident after all modules disappeared.
Solution: `ClearModuleStressState(true)` now releases the existing stress `GraphicsBuffer` during real shader-clear states, then publishes zero stress params and records the uploaded count as zero. `ClearModuleStressState(false)` still preserves the buffer for active-order rebuilds that upload replacement data in the same tick.
Rejected Alternatives: Always keeping the buffer resident, or releasing it on every active-order clear. The first wastes VRAM in empty/teardown states; the second creates release/reallocate churn during module pooling and graph rebuilds.
Scalability potential: Low/MX350 gets lower idle VRAM pressure when habitats are absent or torn down. Mid/High/Ultra keep the buffer hot during real active-module reorder events, preserving deformation responsiveness.
Hardware Impact: Saves one structured GPU buffer after empty habitat teardown or startup clear; no hot-frame allocation and no shader cost. Estimated 5-20 us plus transient VRAM saved on low-end rebuild/teardown paths, with 0 us idle-frame tax.

## Follow-Up Correction - Buffer Growth Clear Suppression

Problem: When module stress capacity grows beyond the current `GraphicsBuffer`, `EnsureModuleStressBuffer` released the old buffer with a shader-param clear even though the upload path immediately creates, uploads, binds, and republishes the replacement buffer.
Solution: Buffer growth now calls `ReleaseModuleStressBuffer(false)`, preserving shader params during same-path replacement. Dispose and real empty clears still use the clearing path.
Rejected Alternatives: Leaving the transient global zero, or never clearing on release. The transient zero burns driver traffic and can produce one-frame visual flicker during growth; never clearing leaves stale globals after teardown.
Scalability potential: Low/MX350 avoids unnecessary driver work during base expansion. Mid/High/Ultra keep continuous pressure deformation while the buffer grows.
Hardware Impact: Saves one redundant `Shader.SetGlobalVector` on stress-buffer growth/reallocation events; estimated 1-4 us on driver-bound MX350-class frames, with no idle-frame cost.

## Follow-Up Correction - Combined Shader Stress Resolver

Problem: The DryZone vertex path resolved a habitat module index, then called a second helper that recomputed the module count before reading the stress buffer.
Solution: Replaced the split index/read path with `HectonHabitatInteriorResolveStress01`, which clamps module count once, scans once, returns zero on no match, and reads `_HectonHabitatModuleStressBuffer` only for a valid module.
Rejected Alternatives: Keeping the split helpers for readability, or adding another metadata buffer. Split helpers duplicate scalar work on every stressed vertex; another buffer changes the renderer contract for no visual gain.
Scalability potential: Low/MX350 is unchanged because low tier does not run per-module vertex lookup. Mid/High/Ultra keep identical localized bowing with less vertex shader scalar work.
Hardware Impact: Removes one duplicate module-count clamp/read helper call per Mid/High/Ultra DryZone vertex. Estimated 1-3 us per 1k affected interior vertices on MX350-class GPUs, with no idle-frame cost.

## Follow-Up Correction - Panel UV Reuse

Problem: The high-tier vertex path computed panel UV for sine bow, then recomputed centered panel UV inside normal bias.
Solution: `HectonHabitatInteriorApplyPanelBendOS` now outputs `panelCenteredUv` together with the shared panel mask. `HectonHabitatInteriorApplyCheapNormalBiasWS` consumes that value instead of recalculating `frac(abs(uv))`.
Rejected Alternatives: Leaving the duplicate panel-space math, or replacing high-tier sine panels with low-tier triangle masks. The first wastes vertex ALU; the second violates the instructed sine bow look.
Scalability potential: Low/MX350 remains on the cheaper triangle crease path. Mid/High/Ultra keep premium sine deformation and normal bias with less repeated vertex work.
Hardware Impact: Removes one panel UV normalize/frac/center calculation from the stressed normal-bias path. Estimated 1-2 us per 1k affected interior vertices on MX350-class GPUs, with no idle-frame cost.

## Follow-Up Correction - Shader Helper Anti-Bloat

Problem: Panel-UV reuse left `HectonHabitatInteriorPanelMask` as an unused wrapper around the live panel UV and sine-mask helpers.
Solution: Removed the unused wrapper and kept only `HectonHabitatInteriorPanelUv`, `HectonHabitatInteriorPanelMaskFromUv`, and `HectonHabitatInteriorCheapPanelMask`.
Rejected Alternatives: Keeping the compatibility wrapper for possible future use. Future-proof dead code increases audit noise and does not buy runtime value.
Scalability potential: Low/MX350 and Mid/High/Ultra behavior is unchanged; shader source stays smaller and easier to audit.
Hardware Impact: Compile-time/source-size cleanup only; no direct runtime microseconds claimed.

## Follow-Up Correction - Index-Hinted Graph Lookup

Problem: Active-module stress loops repeatedly resolved `BaseModule` back into `_moduleBuffer` by scanning from slot 0, even when active module order matched graph order.
Solution: Added an index-hinted overload for `TryResolveGraphModuleRecord`. Stress update, stress-order hash, direct signal targeting, and nearest fallback now check the active slot first, then fall back to the existing graph scan when orders differ.
Rejected Alternatives: Building a managed dictionary/cache or requiring active and graph order to be identical. A dictionary adds managed memory/churn risk; requiring identical order would break the renderer-order alignment correction.
Scalability potential: Low/MX350 reduces CPU work during signal-heavy base stress frames. Mid/High/Ultra keep exact localized deformation while paying less lookup overhead when orders align.
Hardware Impact: Best case removes up to one 64-record scan per active module lookup in stress tick/signal paths; estimated 4-12 us saved on i3/MX350-class signal-heavy frames, with no allocation and no shader cost.
