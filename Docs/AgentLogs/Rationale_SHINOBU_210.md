# SHINOBU_210 Rationale

Date: 2026-05-20
Status: POLISH LOOP ACTIVE - PENDING VERIFICATION

## Decision 001 - Offline Visual Fake Instead Of Runtime Deformation

Problem: Real-time base-module wall buckling under abyss pressure would mutate mesh geometry or spawn physics debris during gameplay, violating frame-time and GC constraints.
Solution: Use an Editor-only Burst pipeline that bakes Stressed, Ruptured, and Collapsed static mesh states plus primitive collision hull data. Runtime state becomes an integer mesh-state selection, not deformation math.
Rejected Alternatives: Runtime procedural vertex deformation was rejected because it touches mesh buffers in the hot path. Rigidbody fragment bursts were rejected because they expand PhysX broadphase and active body budgets. Skinned blend shapes were rejected because base architecture is static geometry and would keep deformation work live.
Scalability potential: Low uses static meshes with conservative primitive hulls. Middle uses the same meshes with richer stress vertex colors. High keeps more detailed cracked/torn baked geometry. Ultra can bake denser visual states and stronger shader stress response without changing runtime truth.
Hardware Impact: On i3/MX350, expected runtime impact shifts from suspicious per-frame deformation or debris spikes to a mesh reference/index swap. Measured proof absent until compile and Unity profiler artifacts exist.

## Decision 002 - Editor Local Scratch, No New Global Data Route

Problem: The batch requires NativeArray and NativeList scratch buffers for mesh processing, but GlobalDataVault must not become a dumping ground for local editor-only temporary data.
Solution: Keep bake scratch inside Editor-only methods with TempJob lifetime, complete jobs at editor command boundaries, and dispose in finally blocks. Only immutable generated assets and aligned mapping DTOs cross into runtime.
Rejected Alternatives: Creating new GlobalDataVault buffers was rejected because the bake pipeline is an owner-local editor tool, not a cross-domain runtime native state owner. Persistent local NativeArrays were rejected because the tool does not require steady-state runtime buffers.
Scalability potential: Low through Ultra share the same runtime immutable assets. Higher tiers can consume denser baked meshes and vertex-color detail without increasing runtime data ownership complexity.
Hardware Impact: Avoids persistent native memory footprint on weak devices. Editor bake cost is cold-path only; runtime memory remains bounded by generated mesh assets.

## Decision 003 - Interleaved 32-Byte Baked Vertex Layout

Problem: Baked habitat damage meshes need predictable GPU fetch layout without bloated per-vertex bandwidth.
Solution: The editor baker writes `HabitatDamageBakedVertex` as 32 bytes: position float3 at 0, normal float16x4 at 12, tangent snorm8x4 at 20, uv0 float16x2 at 24, color unorm8x4 at 28. `Mesh.SetVertexBufferParams` declares that exact stream.
Rejected Alternatives: Float32 normal/tangent/UV layout was rejected because it inflates static damage meshes. Runtime MaterialPropertyBlock deformation was rejected because it keeps pressure response live and risks SRP batcher breakage.
Scalability potential: Low uses compact geometry and vertex stress color only. Middle/High/Ultra can bake denser rupture topology while keeping the same vertex layout and shader contract.
Hardware Impact: 32-byte vertices reduce bandwidth on MX350 compared with 48-56 byte float-heavy layouts. Exact frame/VRAM gain pending Unity import and profiler proof.

## Decision 004 - Duplicate-Vertex Tear With Degenerate Breach Holes

Problem: Structural rupture needs visible separation without runtime topology edits.
Solution: The baker duplicates vertex streams for Ruptured/Collapsed states, offsets opposite sides of deterministic weak seams, and degenerates triangles at breach centers to create holes offline.
Rejected Alternatives: Runtime mesh cutting was rejected because it mutates topology in gameplay. Authoring separate broken prefabs manually was rejected because it scales badly across module variants and invites physics fragment spawns.
Scalability potential: Low can bake fewer live triangles through breach degeneration. Middle/High/Ultra preserve richer torn rims and stress colors with identical runtime state selection.
Hardware Impact: Runtime cost remains a mesh swap. Static mesh memory increases for damaged states; that is an asset budget issue, not a per-frame CPU issue.

## Decision 005 - Static Scanner Report Preserves Prior Agent Evidence

Problem: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` already contained SHINOBU_209 output, but SHINOBU_210 is required to write the same path.
Solution: The SHINOBU_210 scanner preserves the previous report payload inside `previousReport` while writing the new Habitat/Environment verdict.
Rejected Alternatives: Blind overwrite was rejected because 20+ agents are operating concurrently. Creating a different report path was rejected because the prompt names the required file.
Scalability potential: Report preservation is tooling hygiene and has no runtime tier impact.
Hardware Impact: None. Static evidence only.

## Decision 006 - Compile Guard Honored

Problem: AGENTS forbids `dotnet build` when CPU is under work over 50% or another dotnet/csc is running.
Solution: Process scan found no `dotnet`/`csc` output, but `Get-Counter` reported 100% CPU during guard checks. No build was launched.
Rejected Alternatives: Running build anyway was rejected because it violates the batch CPU rule and risks disrupting parallel agents.
Scalability potential: None. Verification remains pending until machine load drops.
Hardware Impact: Prevented additional local CPU load during concurrent agent work.

## Decision 007 - Runtime Unity Object Purge

Problem: The previous pass placed a `MonoBehaviour` mesh-swap bridge and a managed `ScriptableObject` bake manifest in the Runtime assembly, which made the offline baker look like it owned presentation objects.
Solution: Delete the runtime bridge from SHINOBU_210 ownership and move baked damage truth to `Hecton8.Habitat.Deformation.Contracts` as blittable DTOs plus a math-only hash resolver. The managed bake manifest now lives in the Editor assembly only.
Rejected Alternatives: Keeping `MeshFilter.sharedMesh` logic in this lane was rejected because structural/rendering owners should consume integer state and mesh hashes, not a prefab-attached controller from the offline baker.
Scalability potential: Low through Ultra all use the same runtime integer state. Visual richness scales through the baked asset set and shader vertex color response.
Hardware Impact: Removes a managed Unity-object path from this lane's runtime surface. Exact profiler delta remains absent.

## Decision 008 - Continuous Quality Inside Offline Geometry Math

Problem: Tear and collision-lie stages had weak quality continuity; they risked acting as fixed middle-ground output.
Solution: Feed `GlobalQualityWeight` into tear seam sharpness, tear gap amplitude, breach-hole threshold, and primitive hull count. Low quality bakes fewer hulls and smaller gaps; middle/high densify rupture; ultra keeps richer visual damage without changing runtime state.
Rejected Alternatives: Binary low/high bake switches were rejected because HECTON-8 requires smooth scalability.
Scalability potential: Weak devices can consume conservative baked meshes and one primitive hull; high devices can use richer rupture topology and three primitive hulls.
Hardware Impact: Runtime unchanged at integer/hash selection. Asset memory and authoring bake time scale with selected quality.

## Decision 009 - Reserved Vault IDs Without Core Enum Mutation

Problem: The ultra mandate requires H-Phi/Vault status, but editing central Core memory enums during a parallel batch would widen the compile wall.
Solution: Reserve `73320..73323` in the SHINOBU_210 architecture route card and Binary Payload Ledger. The offline baker itself requests no gameplay Vault handles.
Rejected Alternatives: Editing `H8Memory.cs` was rejected because it is a massive Core header and not required for the Editor bake pipeline to function.
Scalability potential: Future runtime consumers can import the mapping/hull/telemetry buffers through an owner route without changing baked asset math.
Hardware Impact: No runtime memory allocated by SHINOBU_210 in this pass.

## Decision 010 - CSV Byte Ingest Without Managed File Blob

Problem: The first pass used `File.ReadAllBytes` for `habitat_crush_profiles.csv`, which is a lazy managed file blob even if the path is Editor-only.
Solution: Read the file through `FileStream` into a Temp `NativeArray<byte>` and parse tokens through `ReadOnlySpan<byte>`. The UI still creates profile-name strings because UI Toolkit dropdowns are managed Editor facade controls, not bake-loop data.
Rejected Alternatives: `string.Split`, LINQ, and managed token arrays were rejected because they do not match the CSV binary bridge mandate.
Scalability potential: CSV parsing remains cold and bounded to 1 MB; output profiles drive continuous low/middle/high/ultra bake math.
Hardware Impact: No runtime impact. Editor GC from the file blob route is removed; UI label strings remain Editor-only.

## Decision 011 - Unity Import And CSV Label Compatibility

Problem: New SHINOBU_210 scripts had no committed `.meta` files, leaving Unity to invent GUIDs during import. The CSV profile display path also depended on `Encoding.ASCII.GetString(ReadOnlySpan<byte>)`, which is more fragile across Unity profile/API compatibility levels than the parser itself.
Solution: Add deterministic `.meta` files for the new contracts and editor pipeline scripts. Replace the profile-name conversion with a bounded unsafe stackalloc ASCII copier that creates only the final Editor UI string and no intermediate token array.
Rejected Alternatives: Auto-generated GUIDs were rejected because they create cross-workstation import churn. `char[]` intermediate conversion was rejected because it adds another managed allocation. Keeping the Encoding span overload was rejected because compile verification is still blocked by the CPU guard.
Scalability potential: Low through Ultra runtime is unchanged. Editor profile labels remain bounded to 64 bytes; all quality-tier math still comes from the parsed unmanaged profile DTO.
Hardware Impact: Runtime impact is 0 us. Editor import identity becomes stable, and CSV label materialization stays bounded and cold-path.

## Decision 012 - Code-Context Runtime Destruction Scanner

Problem: Task 19 called for static analysis proof, but the previous scanner was a `ReadAllLines` substring pass that could count comments or string literals as runtime destruction code.
Solution: Replace it with deterministic byte-token scanning over non-Editor Habitat and Environment `.cs` files. The scanner walks source bytes, tracks comments, char literals, normal strings, verbatim strings, and raw triple-quote strings, and only matches forbidden deformation/physics tokens in code context.
Rejected Alternatives: Roslyn was rejected because adding a compiler package reference would widen the Editor assembly surface during a parallel batch. Keeping grep-style line matching was rejected because it is weak evidence for architectural enforcement.
Scalability potential: Runtime unchanged. The scanner is cold Editor tooling and now produces more reliable proof across low/middle/high/ultra targets without affecting gameplay.
Hardware Impact: Runtime impact is 0 us. Editor scan uses bounded per-file byte buffers and deterministic file ordering; compile/profiler proof remains pending behind CPU guard.

## Decision 013 - Editor Blackbox Ring Artifact

Problem: The contracts reserved a 300-entry telemetry ring, but the Editor bake queue only produced aggregate JSON totals. That was insufficient for post-failure forensics.
Solution: Add a bounded `NativeArray<HabitatDamageBakeTelemetryEntry>` inside the Editor bake report lifecycle. Each baked state writes module hash, state hash, triangle counts, hull count, quality weight, Burst milliseconds, fault flags, and output mesh hash. The queue writes `Docs/AgentLogs/Dump_SHINOBU_210.bin` and disposes the NativeArray on completion or no-batch exit.
Rejected Alternatives: A ledger-only claim was rejected because it creates no crash artifact. A gameplay Vault allocation was rejected because SHINOBU_210 is an offline Editor baker and must not claim runtime memory ownership.
Scalability potential: Runtime remains unchanged for low through ultra. Higher quality bakes produce richer telemetry rows without changing gameplay state.
Hardware Impact: Runtime impact is 0 us. Editor memory is fixed at 300 * 64 bytes plus a small binary header, then released.

## Decision 014 - Explicit Little-Endian Blackbox Dump

Problem: The blackbox dump initially used `BinaryWriter`, which hides byte ordering behind framework behavior and does not satisfy the binary serialization mandate explicitly enough.
Solution: Serialize the dump header and telemetry entries through explicit little-endian primitive writers. Floats are converted with `math.asuint`; integer words use `math.reversebytes` defensively on non-little-endian hosts before byte emission. The header now stores agent hash, dump version, ring capacity, recorded count, cursor, and entry size.
Rejected Alternatives: Keeping `BinaryWriter` was rejected because it obscures endianness. Writing JSON-only telemetry was rejected because it bloats forensic data and loses exact binary DTO layout proof.
Scalability potential: Runtime unchanged. The binary format remains compact for low-end CI machines and exact enough for ultra-tier bake forensics.
Hardware Impact: Runtime impact is 0 us. Dump header is 24 bytes; each telemetry row remains exactly 64 bytes.

## Decision 015 - Dedicated DamageBake Editor Assembly

Problem: The SHINOBU_210 bake tool originally lived inside the broad Habitat Deformation Editor assembly, which references the runtime deformation assembly for legacy tuner windows. That widened the compile wall for a tool that only needs contracts plus Unity bake packages.
Solution: Move the bake pipeline under `Editor/DamageBake/` and add `Hecton8.Habitat.Deformation.DamageBake.Editor.asmdef`. The new assembly references `Hecton8.Habitat.Deformation.Contracts`, Burst, Collections, Jobs, and Mathematics only. The parent Editor asmdef no longer needs SHINOBU_210 unsafe/Burst references.
Rejected Alternatives: Leaving the tool under the broad Editor asmdef was rejected because any bake-tool edit would share a dependency lane with older runtime-backed tuner tooling. Moving code into Runtime was rejected because the assignment is explicitly Editor-only.
Scalability potential: Runtime remains unchanged. Designer tooling can iterate independently while low/middle/high/ultra bake recipes remain data-driven.
Hardware Impact: Runtime impact is 0 us. Editor compile invalidation scope is narrowed to the DamageBake assembly.

## Decision 016 - Explicit Cache-Line Scratch Vertex Layouts

Problem: `HabitatDamageSourceVertex` and `HabitatDamageWorkingVertex` were sequential structs used inside parallel Burst jobs. Sequential layout leaves stride and padding to compiler/runtime rules and makes false sharing easier during dense vertex writes.
Solution: Pin `HabitatDamageSourceVertex` to 64 bytes and `HabitatDamageWorkingVertex` to 128 bytes with explicit field offsets and padding. Add validator checks for both sizes and critical offsets.
Rejected Alternatives: Keeping compact sequential structs was rejected because the bake loop mutates adjacent `NativeArray` elements in parallel. A smaller 96-byte working vertex was rejected because it does not keep every element on a 64-byte boundary.
Scalability potential: Runtime unchanged. Editor memory cost rises in exchange for cleaner parallel write isolation; low quality can still bake fewer/cheaper variants, high and ultra get richer topology without runtime cost.
Hardware Impact: Runtime impact is 0 us. Editor bake scratch bandwidth increases, but false-sharing risk drops on ARM64 and desktop multi-core CPUs.

## Decision 017 - Explicit AUP Settings Layout

Problem: `HabitatDamageBakeSettings` carries `double3` AUP inputs but previously had no explicit layout. That left 8-byte alignment proof implicit.
Solution: Pin settings to 80 bytes with `ModuleAup` at offset 0, `SeaLevelAup` at offset 24, then float tuning fields and explicit padding. Add validator checks for settings size and key offsets.
Rejected Alternatives: Relying on default layout was rejected because the task explicitly cares about AUP precision and ARM64 alignment proof. Moving AUP data into runtime state was rejected because the bake tool is Editor-only.
Scalability potential: Runtime unchanged. The same aligned settings DTO drives low/middle/high/ultra bake variants through `GlobalQualityWeight`.
Hardware Impact: Runtime impact is 0 us. Editor bake settings now have explicit 8-byte-aligned AUP fields.

## Decision 018 - Rsqrt Lengths And Native Scanner Source Buffer

Problem: The bake jobs still used direct `math.length` in crush/stress paths, and the runtime-destruction scanner used a managed per-file `byte[]`. Both were Editor-side, but they weakened the math-vaccination and zero-GC evidence trail.
Solution: Add `HabitatDamageBakeMath.SafeLength` using `dot(v, v) * math.rsqrt(math.max(dot(v, v), 1e-8f))`, then route radial length, displacement stress, and vertex stress color through it. Move scanner file reads into a Temp `NativeArray<byte>` with unsafe `Span<byte>` reads and dispose it in `finally`.
Rejected Alternatives: Keeping `math.length` was rejected because the i3/SIMD mandate prefers guarded reciprocal-square-root forms. Keeping managed scanner `byte[]` was rejected because Task 19 is an architectural evidence path and should not rely on a managed file blob when the existing CSV parser already proved the NativeArray+Span pattern.
Scalability potential: Low uses the same finite math on sparse/cheap bakes. Middle/High/Ultra can bake richer rupture states without adding runtime CPU work; scanner evidence remains cold tooling.
Hardware Impact: Runtime impact is 0 us. Editor bake math avoids sqrt-backed helper ambiguity and scanner managed heap pressure is reduced; compile/profiler proof remains pending.

## Decision 019 - Corridor Axis Alignment And Branch-Light Tear Gate

Problem: The mock pressure corridor builds radial vertices in local `X/Y` and length along local `Z`, but the buckling job used local `X/Z` as the radial plane. That could shorten or fold the corridor axis instead of crushing the cross-section. The tear job also used a zero-tear early return and manual quality polynomial, weakening the continuous quality proof.
Solution: Change `ApplyHydrostaticBucklingJob` to crush local `X/Y` and reserve local `Z` for axial ripple. Fix mock axial sampling to use `max(1, lengthSegments - 1)` so the synthetic cylinder spans the full requested length. Replace manual quality curves with `math.smoothstep(0, 1, quality)` and gate tear displacement through `math.step` instead of returning for normal vertices.
Rejected Alternatives: Keeping `X/Z` radial math was rejected because it contradicts the synthetic corridor generator and can damage axial topology. Keeping the early return was rejected because branch-light scalar gating is cheap and closer to the mandate's SIMD posture.
Scalability potential: Low bakes still use conservative gaps and hull counts. Middle/High/Ultra now build richer damage on a corrected cross-section and explicit smooth quality curve, without runtime deformation.
Hardware Impact: Runtime impact is 0 us. Editor bake correctness and branch predictability improve; compile and profiler proof remain pending.

## Decision 020 - Chronological Evidence Repair And Build Guard

Problem: The Loop 11 report was initially inserted near the top of `LOG_SHINOBU_210.md`, which breaks the Top=Old, Bottom=New evidence contract even though the code patch itself was valid.
Solution: Move the Loop 11 report to the chronological bottom and rerun static gates for forbidden source patterns, expected smoothstep/axis markers, diff whitespace, process state, and CPU load.
Rejected Alternatives: Leaving the report out of order was rejected because the CTO reads disk logs, not chat history. Launching `dotnet build` was rejected because the CPU counter reported 96.53%, above the AGENTS 50% ceiling.
Scalability potential: None. This is evidence integrity and build-discipline enforcement.
Hardware Impact: Prevented extra local CPU pressure during a high-load period; no runtime impact.

## Decision 021 - Hull Output Sanitization Before Bounds Scan

Problem: `GenerateSimplifiedHullsJob` used an uninitialized `NativeArray<HabitatDamageHullDTO>` and cleared it only after scanning vertex bounds. Missing vertices or all-non-finite vertices could leave random hull hashes that later entered a manifest.
Solution: Clear every hull row before vertex validation and return empty rows if no finite bounds can be proven. Keep the hull DTO fixed at 64 bytes and preserve the primitive collision lie.
Rejected Alternatives: Trusting imported art and previous jobs to always emit finite vertices was rejected because NaN vaccination must guard the last write boundary before manifest serialization. Switching to MeshCollider output was rejected because it violates the Dear Lie.
Scalability potential: Low/Middle/High/Ultra all keep the same primitive hull route. Richer quality can still emit more hulls only after finite bounds exist.
Hardware Impact: Runtime impact is 0 us. Editor cost is an 8-row hull clear per baked state, negligible against a bad collision manifest risk.

## Decision 022 - Sentinel-Aware Finite Vertex Guard

Problem: `float.MaxValue` and `float.MinValue` sentinels are finite, so checking only `isfinite(min/max)` does not prove any real vertex contributed to bounds. An all-NaN source could still produce sentinel-derived center/size data.
Solution: Track `finiteVertexCount` in both hull emission and mesh bounds calculation. Hulls emit only when at least one finite vertex contributed; mesh bounds otherwise fall back to a safe one-meter default.
Rejected Alternatives: Leaving the previous finite-only guard was rejected because it confuses sentinel validity with geometric validity. Throwing from the bake job was rejected because the editor queue should contain invalid art by emitting safe empty proxies and telemetry flags, not by crashing the batch.
Scalability potential: No quality-tier behavior changes. Low through Ultra all require finite geometry before collision fake emission.
Hardware Impact: Runtime impact is 0 us. Editor adds one integer increment per finite vertex in two cold validation passes; the cost is negligible compared with preventing malformed baked hulls.

## Decision 023 - External Compile Wall Recorded

Problem: The CPU guard eventually cleared and `dotnet build Hecton8.slnx` was justified, but the build failed with 72 errors before SHINOBU_210 could be proven. Errors are in external domains: missing `Hecton8.Logistics.Grid`, `FaunaKinematicsRuntime`, `HectonFluidEngine`, `SoundEmissionSignal`, `H8BinaryWorldPager`, `SocketDefinitionDTO`, `IDockingAutopilotService`, and other Core/Power/Construction/World contracts.
Solution: Record the compile wall as external and do not edit those domains. SHINOBU_210 verification remains limited to static gates until the integrator repairs the shared build.
Rejected Alternatives: Patching Core/Power/Fauna/Save/Construction/World symbols was rejected because those are outside the assigned Habitat offline damage-bake domain and would violate the parallel-agent boundary. Reverting SHINOBU_210 work was rejected because the failure log contains no SHINOBU_210 file path.
Scalability potential: None. This is build hygiene.
Hardware Impact: Build consumed one single-core pass after CPU fell below 50%; no further build retries will be launched until external blockers change.

## Decision 024 - Four-State Runtime Resolver

Problem: `HabitatDamageMeshStateResolver.ResolveStateIndex` returned only Intact or Collapsed. That made Stressed and Ruptured baked meshes unreachable even though the assignment requires three baked strength states beyond pristine.
Solution: Resolve state with a branch-light sum of `math.step` thresholds: Stressed at 0.33333334, Ruptured at 0.6666667, Collapsed at 0.95. Mesh hash selection remains O(1) and uses the existing `ModuleDamageStateMappingDTO`.
Rejected Alternatives: Keeping pristine-until-collapse was rejected because it wastes the offline Stressed/Ruptured assets and pushes visible deformation back toward runtime shader tricks only. A data-driven threshold table was rejected for this contract layer because it would add another memory owner before the importer/consumer route exists.
Scalability potential: Low devices can still use the same O(1) state selection with cheaper baked meshes; high/ultra devices can bind richer baked mesh variants per state without changing runtime CPU complexity.
Hardware Impact: Runtime cost is three scalar `math.step` evaluations and integer/hash selection, no vertex deformation or Unity object traversal.

## Decision 025 - Pack Job Chained To MeshData Boundary

Problem: `BakeDamageState` completed the deformation job graph, then scheduled `PackBakedVertexJob` as a second independent job and completed again. That created an avoidable synchronization point in the offline bake path.
Solution: Allocate writable `MeshData`, schedule `PackBakedVertexJob` with the current deformation/normal/color/hull `JobHandle` as dependency, and complete once before CPU-visible bounds/index copy and `Mesh.ApplyAndDisposeWritableMeshData`.
Rejected Alternatives: Keeping two completes was rejected because the pack stage is a pure dependency continuation and does not need a detached synchronization barrier. Moving all MeshData work into a runtime importer was rejected because this agent is Editor-only.
Scalability potential: Low quality bakes complete fewer vertices but still benefit from one less sync. High/Ultra bakes with denser topology benefit more because the pack phase overlaps the existing dependency chain until the MeshData API boundary.
Hardware Impact: Runtime impact is 0 us. Editor bake removes one `Complete()` per baked state; measured wall-clock proof remains blocked by external compile/Unity import state.

## Decision 026 - Resolver Source Regression Reapplied

Problem: A static gate after Loop 15 found `ResolveStateIndex` still using collapse-only `math.select(0, 3, p >= 0.95f)` in source, despite the earlier rationale and status entry saying all states were reachable.
Solution: Reapply the actual source change: state index equals the sum of three `math.step` thresholds for Stressed, Ruptured, and Collapsed. Keep `math.select(0, 3` in the forbidden gate for SHINOBU_210 owned files.
Rejected Alternatives: Trusting the earlier documentation was rejected because source is the only objective truth. Leaving Stressed/Ruptured unreachable was rejected because it nullifies the offline bake states.
Scalability potential: Low through Ultra all preserve O(1) selection; high/ultra can use richer state meshes without runtime deformation.
Hardware Impact: Runtime stays constant-time and allocation-free. The gate prevents the collapse-only regression from returning silently.

## Decision 027 - Direct Mesh SetVertexBufferData Upload

Problem: Task 10 explicitly names `SetVertexBufferData` from `NativeArray`s. The previous implementation used writable `MeshData`, which is a direct Unity mesh write path but not the literal API requested by the assignment.
Solution: Pack baked vertices into a TempJob `NativeArray<HabitatDamageBakedVertex>`, chain `PackBakedVertexJob` to the deformation graph, complete once, then call `Mesh.SetVertexBufferData` and `Mesh.SetIndexBufferData` from native arrays.
Rejected Alternatives: Keeping MeshData-only serialization was rejected because the task wording is unambiguous and static evidence should match it. Moving serialization to runtime was rejected because the baker is Editor-only and runtime must only resolve baked state hashes.
Scalability potential: Low bakes pay one compact packed buffer. High/Ultra bakes with denser topology still stay Editor-only and produce the same 32-byte vertex stream for GPU bandwidth.
Hardware Impact: Runtime impact is 0 us. Editor adds one packed TempJob buffer but removes ambiguity in the serialization contract and retains one completion boundary.
