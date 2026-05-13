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
