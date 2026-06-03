# Rationale 1715 - Interactive Equipment & Prop Baker

## Decision 000 - Authority And Mandates

Problem: The task creates editor-only equipment geometry and runtime-readable IK socket metadata. Wrong route would put mesh math or collider cooking into player runtime.
Solution: Use offline EditorWindow baker only; serialize static mesh, primitive collider children, and unmanaged anchor struct data into prefab metadata. Follow 3dmodel, equipment props, authoring, data, math, systems, telemetry, performance, tools, physics, animation, and UI route bibles.
Rejected Alternatives: Runtime mesh generation, artist-empty anchor search, visual mesh MeshCollider, and hot GlobalRegistry polling. Standard Unity approach is too slow because it shifts topology, collider, and scene search cost into runtime.
Scalability potential: Low uses one-chamfer edges, lower cylinder sides, same anchors/colliders. Middle adds denser bevels and cables. High adds smoother handles and richer wear masks. Ultra bakes more rounded bevel segments and higher radial density without changing runtime truth.
Hardware Impact: i3/MX350 gains from static mesh loading, one shared material route, primitive colliders, and O(1) anchor reads. Runtime steady-state target remains 0 B GC; frame impact is static-source pending, profiler not run.

## Decision 001 - Missing Domain Boundary File

Problem: Retired standalone domain-map references must not drive active ownership.
Solution: Treat `AGENT_PROMPT id="1715"` domain and AGENTS.md folder contract as active boundary: editor generator under `Assets/_Project/Editor/Generators/Interiors/`, runtime metadata inspection under `Assets/_Project/Scripts/Interaction/`.
Rejected Alternatives: Reading archived batch domain files or guessing from unrelated lore domain docs. Archived logs are forbidden by batch hygiene.
Scalability potential: No runtime scalability effect.
Hardware Impact: No hardware effect; risk is architectural scope drift. Logged for integrator.

## Decision 002 - Socket Metadata Instead Of Transform Children

Problem: FABRIK/socket consumers need fixed anchor coordinates without managed hierarchy scans or runtime Transform marker lookup.
Solution: Added `EquipmentMetadata` with serialized `InteractionAnchorData[]` using explicit 64-byte layout. Read accessors return count/array/reference or one struct by index only; no allocation, no scene search, no registry call.
Rejected Alternatives: Empty child GameObjects named `SOCKET_*`, `GetComponentsInChildren`, ScriptableObject lookup tables, or DataVault bootstrap from prefab load. Standard Unity marker hierarchy is too slow because it pushes string/name lookup and transform traversal into runtime setup.
Scalability potential: Low/Middle/High/Ultra keep identical gameplay anchor truth. Only visual mesh density changes through `GlobalQualityWeight`.
Hardware Impact: i3/MX350 avoids hierarchy scanning and object marker cache allocation. Runtime steady-state socket read remains 0 B GC; cold prefab deserialization cost is Unity-owned.

## Decision 003 - Visual CSG Recesses And Primitive Collider Proxies

Problem: Cockpit props need convincing cut-ins, bevels, levers, screens, and cable bundles without importing meshes or using runtime boolean/collider cooking.
Solution: Implemented editor-only CSG-style recessed pockets and bevel lips as baked mesh topology, plus box/capsule/sphere collider children. The visual mesh is never used as a collider.
Rejected Alternatives: MeshCollider, ProBuilder/CSG package dependency, runtime Boolean mesh modification, and imported static FBX. Standard Unity MeshCollider is too slow and too opaque for hand interaction surfaces.
Scalability potential: Low uses fewer radial/cable/torus segments. Middle increases cable and wheel smoothness. High increases bevel/wear detail. Ultra spends saved runtime cost on denser baked surface detail, not authority changes.
Hardware Impact: i3/MX350 collision cost is primitive-only. Saved runtime collision cooking and mesh physics overhead are expected to dominate; exact microseconds require Unity profiler after host build guard clears.

## Decision 004 - Catenary Cheat Route

Problem: Cable bundles need believable sag, bundling, and tie pinch detail without simulating cable physics.
Solution: Used a Burst offline `IJob` with cosh catenary sag, deterministic strand offsets, and geometric pinch rings. This is a cinematic cheat, not a runtime cable solver.
Rejected Alternatives: Rigidbody cable joints, LineRenderer spline per cable, Verlet chain, or same-frame schedule/readback loop. Physics cable simulation is too slow and unpredictable for static cockpit decoration.
Scalability potential: Low: 8 segments/6 ring sides. Middle: moderate cable subdivisions. High: denser catenary. Ultra: 22 segments/8 ring sides with same static bake route.
Hardware Impact: i3/MX350 pays zero runtime solver cost. Bake-time cost is editor-only; runtime sees one mesh and primitive colliders.

## Decision 005 - Single Material SRP Route

Problem: Equipment detail needs base metal, grime, screen emission, warning lights, cable rubber, and grip variance without material slot fragmentation.
Solution: Encoded wear/cavity/emissive/material identity in vertex color and UV channels; prefab renderer uses one shared material. Report records material slot count as one when bake executes.
Rejected Alternatives: material-per-part, cloned generated material, extra texture generation, or renderer-per-subpart. Standard material fragmentation damages SRP batching and memory residency.
Scalability potential: Low/Medium/High/Ultra can all share one shader route; richer devices can use the same masks for stronger shader effects.
Hardware Impact: i3/MX350 avoids renderer/material churn. Expected gain is fewer draw/material state changes; exact value pending Unity profiler.

## Decision 006 - Validation And Black Box

Problem: Offline generated topology can silently produce nonfinite vertices, tiny triangles, bad indices, or broken bounds; without proof the runtime solver could inherit bad sockets.
Solution: Added Burst topology validator, `mesh.RecalculateBounds()` finite/nonzero gate, and source-native `UnsafeUtility.SizeOf<T>()` layout checks. Removed obsolete JSON report and binary dump I/O after APEX protocol update.
Rejected Alternatives: trusting `Mesh.RecalculateNormals`, relying on visual inspection, only logging to chat, or proving correctness through disk report files. Standard Unity import validation does not know the HECTON area threshold or socket proof.
Scalability potential: Validation is independent of quality level; low-to-ultra must pass the same finite/index/area gate.
Hardware Impact: No runtime cost. Editor failure cost is acceptable because it prevents bad static assets from reaching cheap devices.

## Decision 007 - Build Guard Block

Problem: The project requires compile verification, but host check reported CPU load at 100% and an existing `dotnet` process.
Solution: Did not launch `dotnet build` or another compile. Ran static source scans only: balanced braces, no `MeshCollider`, no scene find, no `GlobalRegistry`, no hidden `.Complete()` in new files.
Rejected Alternatives: Forcing a build during high CPU or parallel dotnet work. That violates the explicit guard and risks conflicting with other agents.
Scalability potential: No content effect.
Hardware Impact: Avoids saturating the shared machine. Compile and Unity bake must be rerun once CPU is below guard and no other build is active.

## Decision 008 - APEX Source-Only Proof And Fence Fast-Fail

Problem: The previous implementation still had proof-by-I/O remnants and adjacent read/resolve helpers relied on the vault implementation to reject compaction-fence access.
Solution: Removed the baker JSON/binary proof route, removed the unreferenced VR bridge disk dump method plus its physical-hand caller, added source-native static layout validation, and added explicit `IsCompactionFenceActive` fast-fail checks before interaction/VR bridge buffer open or read helper routes.
Rejected Alternatives: Keeping dead report writers, retaining disk dump coupling in the touched bridge path, adding new validator utility classes, or rewriting GlobalDataVault ownership. Those would add topology noise without improving the 1715 steady-state path.
Scalability potential: Low/Middle/High/Ultra retain identical gameplay anchors and vault ownership; only visual mesh density scales through continuous `GlobalQualityWeight`.
Hardware Impact: i3/MX350 avoids redundant disk I/O from the baker and avoids unnecessary vault handle attempts while compaction is active. Exact frame impact remains unmeasured because compile/profiler execution is blocked by host CPU/dotnet guard.

## Decision 009 - APEX Lock Flattening And MeshData Type Correction

Problem: Static review found one Unity API risk and two lock-window risks: `IndexFormat.UInt32` was uploaded through `NativeArray<int>` instead of the project-standard `NativeArray<uint>`, scheduled surface completion resolved SDF/terrain hits while holding an interaction mutation guard, and kinematic hand solving held the bridge mutation guard across SDF lease acquisition and `ResolveHand`.
Solution: Converted MeshData upload to `NativeArray<uint>` with a tight conversion loop, changed byte clamp logic inside Burst to branch assignments, added explicit Burst compile parameters, split scheduled surface completion into snapshot/resolve/writeback phases, and split kinematic hand solving into DataVault snapshot, off-lock SDF/ResolveHand/somatic/telemetry precompute, and direct-assignment writeback. Added a prewarmed fixed `VRInteractionSocketDTO[]` socket snapshot, cleared by bounded copy.
Rejected Alternatives: Leaving Unity to reinterpret signed int indices, resolving SDF under mutation guard, or copying sockets into per-frame managed arrays/lists. Those are slower or less deterministic under compaction pressure and violate the direct-copy lock rule.
Scalability potential: Weak devices get shorter mutation windows and no extra steady-state GC. Middle/High/Ultra keep identical authority while spending quality weight only on offline visual density and existing kinematic fidelity hints.
Hardware Impact: i3/MX350 benefits from reduced lock hold time around SDF/hand solve and no extra managed allocations. Exact microseconds remain unmeasured because build/profiler execution is blocked by active `dotnet` guard.

## Decision 010 - MeshData Failure Disposal And Dump Path Removal

Problem: The mesh upload route allocated writable MeshData before asset creation and had no local cleanup if vertex/index upload or `ApplyAndDisposeWritableMeshData` failed. A later source scan also found residual VR bridge fault-dump symbols in touched interaction files.
Solution: Wrapped mesh creation/upload/save in a `try/catch`, disposing the MeshDataArray only when Unity has not consumed it and destroying the transient Mesh on failure. Removed the touched VR bridge dump constant/method and physical-hand dump calls/flag, leaving only in-memory per-frame fault throttling.
Rejected Alternatives: Trusting Unity asset creation to clean failed MeshData allocations, or retaining source-level binary dump proof after the current APEX directive rejected disk proof. Both create avoidable editor/resource and I/O surface risk.
Scalability potential: Low/Middle/High/Ultra unchanged. This is failure-path hygiene and source-route cleanup; runtime visual quality still scales only through baked static mesh density.
Hardware Impact: i3/MX350 avoids failed-bake native resource leakage and any touched-path disk dump attempt. Runtime measured savings unavailable because compile/profiler execution is blocked by active Unity `dotnet` guard.

## Decision 011 - Socket Snapshot Without Persistent Native Alias

Problem: The first lock-flattening pass used a persistent `NativeArray<VRInteractionSocketDTO>` field in `PhysicalHandController`, which shortens lock windows but conflicts with the project ban on persistent native aliases in MonoBehaviours.
Solution: Replaced it with one fixed cold `VRInteractionSocketDTO[]` and added a `ReadOnlySpan<VRInteractionSocketDTO>` overload for kinematic snap math. DataVault native ownership remains inside the vault; the controller only holds a bounded value-copy snapshot.
Rejected Alternatives: Keeping `ResolveHand` under the vault mutation guard, retaining a MonoBehaviour NativeArray field, or allocating a managed array per solve. The first keeps heavy SDF/socket math under lock, the second violates memory sovereignty, the third creates steady-state GC.
Scalability potential: Low/Middle/High/Ultra unchanged. Socket count and authority layout remain fixed; quality weight still affects solver iteration and static visual density only.
Hardware Impact: i3/MX350 keeps the reduced lock hold time without native alias residency in the controller. Runtime allocation remains 0 B after cold setup; measured profiler proof is blocked by host guard.

## Decision 012 - Capacity Estimates And Concurrent Dump Conflict

Problem: `EquipmentPropBaker1715` used fixed `NativeList` capacities that were safe today but not formally tied to continuous quality settings; parallel Agent 1704 repeatedly reintroduced a VR bridge/hand binary dump route into files touched by 1715 after the source-only proof directive.
Solution: Added deterministic `EstimateVertexCapacity` and `EstimateIndexCapacity` derived from the same continuous `ResolveGeometryProfile` used by geometry emission; raised radial minimum to 12 and kept cable/ring density continuous through `math.lerp`. Removed the reintroduced dump symbols again and verified the touched-source scan stayed clean after a 3-second delay.
Rejected Alternatives: Letting `NativeList` grow implicitly at high quality, keeping binary dump calls because another agent wanted them, or changing unrelated black-box systems outside the 1715 domain. Implicit growth risks editor allocation churn; cross-agent dump I/O violates the current APEX source-only proof directive for this touched path.
Scalability potential: Low keeps conservative geometry with bounded capacity; Middle/High/Ultra spend only editor bake time on denser baked mesh detail. Gameplay anchors, collider proxies, DTO layout, and authority route remain fixed.
Hardware Impact: i3/MX350 runtime unchanged and stays static-mesh/primitive-collider only. Editor bake avoids surprise NativeList growth. Compile/profiler values remain unavailable because Unity Roslyn dotnet processes are active and the build guard blocks another compile.

## Decision 013 - Import Log Hardening And Integer Overload Removal

Problem: Editor.log retained stale 1715 errors for missing capacity/profile helpers, while current source already had those methods; the same project also showed Unity.Mathematics ambiguity on integer `math.max` overloads. Concurrent edits again restored disk dump source symbols into 1715-touched interaction files.
Solution: Verified current method placement by source scan, removed every integer `math.max` clamp from `EquipmentPropBaker1715`, and removed the reintroduced VR bridge/hand dump route again. Kept float/vector `math.max` uses only. Re-ran dump-symbol, hot-method, braces/parens, trailing-whitespace, orphan-meta, diff-check, Editor.log, and build-throttle gates.
Rejected Alternatives: Trusting stale Editor.log faults without checking current source, leaving integer `math.max` because it is editor-only, or keeping the parallel agent dump route. Standard Unity.Mathematics overload resolution already produced a fault in this project; disk fault writers violate the current source-only proof directive for this path.
Scalability potential: Low/Middle/High/Ultra unchanged. Visual density still scales through continuous `GlobalQualityWeight`; gameplay anchor DTO layout and authority route stay fixed.
Hardware Impact: i3/MX350 runtime unchanged. Editor bake avoids integer overload compile risk and touched runtime path avoids disk I/O. Measured profiler values remain unavailable because CPU is 100% and Unity dotnet compiler processes are active.

## Decision 014 - Baker Validator And Metadata API Polish

Problem: The offline topology threshold was set to `1e-4 m2`, which rejects legitimate small bevel, spoke, and cap triangles on cockpit controls. The baker also used unnecessary native safety suppression attributes and material fallback was arbitrary project-first search. Runtime consumers had index/span anchor reads but no direct copy into a preallocated native buffer.
Solution: Changed the topology gate to `1e-6 m2`, removed `NativeDisableParallelForRestriction`, marked pure output job buffers `[WriteOnly]`, removed unused catenary seed state, added deterministic material priority paths, and added `TryGetAnchorById` plus `CopyAnchorsTo(NativeArray<InteractionAnchorData>)`.
Rejected Alternatives: Increasing geometry size to satisfy a bad validator, leaving safety suppression comments to satisfy no code change, or forcing 1704 to copy through managed arrays. Those options either damage art scale, weaken job safety, or add integration friction.
Scalability potential: Low/Middle/High/Ultra keep the same anchor truth and collider route. Visual density still scales continuously; validator now protects against degenerates without rejecting small authored detail.
Hardware Impact: i3/MX350 runtime path gains a direct native-copy bootstrap route and no added steady-state cost. Editor bake avoids false negative validation failures.

## Decision 015 - Restored Dump Route Rejection

Problem: Concurrent edits restored binary fault-dump calls into the 1715-touched VR interaction bridge after the current APEX source-only directive rejected proof-by-I/O.
Solution: Removed the restored dump constant, vault dump method, controller dump retry fields, and lifecycle/LateFrame dump calls again. Retained in-memory fault frame throttling only.
Rejected Alternatives: Keeping disk dump writes in the touched path or editing unrelated owners outside the 1715 interaction boundary. Disk writes add I/O coupling and conflict with the latest source-only completion rule.
Scalability potential: Low/Middle/High/Ultra unchanged; anchor metadata and kinematic authority remain source/struct driven.
Hardware Impact: Cheap devices avoid fault-path disk work in this touched route. Steady-state remains unchanged and zero-GC by static scan.

## Decision 016 - Anchor Contract De-Duplication And Cable Clearance

Problem: Anchor active/two-hand bits were duplicated in the editor baker and a magic `1u` survived in the socket DTO copy path. Cable sag also needed an explicit static clearance gate so decorative bundles cannot sink into the panel body at low quality.
Solution: Moved anchor flag, hand mask, and surface kind constants into `InteractionAnchorData` without changing FieldOffset layout. Updated baker and metadata bridge to consume the same constants. Added a catenary minimum-center clamp plus post-bake minimum-vertex-y rejection before prefab serialization.
Rejected Alternatives: Leaving magic constants in separate files or relying on visual inspection for cable clearance. Duplicate bits drift when 1704/1715 evolve independently; visual inspection misses automated batch bakes.
Scalability potential: Low keeps sparse catenary segments above the panel. Middle/High/Ultra increase cable/torus density through continuous quality weight while using the same static clearance and anchor truth.
Hardware Impact: i3/MX350 runtime cost unchanged. Editor bake now fails fast on invalid cable clearance and FABRIK/socket bootstrap can copy serialized anchors directly into preallocated socket DTO buffers.

## Decision 017 - Socket Semantic Flag Preservation

Problem: Serialized equipment anchors carried hand mask, two-handed, and surface kind data, but the cold metadata-to-socket bridge only published the active bit. FABRIK could snap to a lever/valve/toggle position but lost authored semantic hints.
Solution: Reserved higher bits in the existing `VRInteractionSocketDTO.Flags` field for two-handed, left-hand, right-hand, and packed surface kind. `EquipmentMetadata.ResolveSocketFlags` maps the serialized anchor data during cold/preallocated socket publication and skips inactive anchors so they cannot occupy reserved runtime socket slots. DTO size, field offsets, socket capacity, and DataVault ownership are unchanged.
Rejected Alternatives: Adding a parallel metadata table, expanding `VRInteractionSocketDTO`, using child transforms, or resolving semantic data through managed dictionaries. Those routes add layout churn, lookup cost, or hierarchy coupling.
Scalability potential: Low/Middle/High/Ultra keep identical socket truth. Quality scaling still affects only offline visual density and existing solver fidelity; semantic bits do not alter gameplay authority.
Hardware Impact: i3/MX350 gains no measured frame time yet, but avoids future managed side-channel lookup for FABRIK hand selection. Steady-state remains fixed DTO read with no allocation by static scan.

## Decision 018 - Anchor Layout And Orientation Validation

Problem: Size-only validation was not enough. A future field reorder could preserve 64-byte size while corrupting FABRIK socket reads, and finite forward/up vectors could still be nearly parallel, producing unstable socket orientation.
Solution: `EquipmentMetadata.ValidateStaticLayout` now verifies exact offsets for every public anchor field. `VRInteractionKinematicBridgeLayout.Validate` now verifies socket DTO offsets. The baker calls `EquipmentMetadata.ValidateAnchorSet` before prefab serialization, rejecting empty sets, duplicate ids, bad hand/surface bits, nonfinite values, invalid active count, and parallel anchor axes. Socket publication orthonormalizes up before `LookRotationSafe`.
Rejected Alternatives: Trusting explicit struct size only, adding a parallel authoring table, or repairing bad anchors at runtime. Size-only checks miss offset drift; runtime repair hides authoring defects and costs solver setup clarity.
Scalability potential: Low/Middle/High/Ultra unchanged. Visual density still scales offline; socket authority remains one validated struct route.
Hardware Impact: i3/MX350 runtime remains fixed DTO reads. The extra validation is editor/cold only and prevents bad prefabs from creating hand snap instability on all hardware tiers.

## Decision 019 - Explicit Fence Fast-Fail Before Mutation Guards

Problem: Several touched interaction paths relied on lower-level buffer open helpers or guard acquisition to fail during compaction. That still allowed callers to enter mutation-guard code before observing the fence.
Solution: Added explicit `IsCompactionFenceActive` checks before guard acquisition and immediately after acquisition in scheduled surface query completion/scheduling and physical hand kinematic snapshot/writeback. Updated the shared `TryAcquireInteractionGuard` helper to reject active fence. All touched guard releases remain in `finally`.
Rejected Alternatives: Trusting `TryResolveHandle` to fail later, or adding a new lock abstraction. Late failure lengthens lock windows; a new abstraction would duplicate first-party guard routing.
Scalability potential: All quality tiers keep identical authority. Weak devices benefit most because compaction pressure does not compete with interaction mutation windows.
Hardware Impact: i3/MX350 avoids needless guard entry during compaction. No measured profiler data because build remains blocked by host CPU/compiler guard.

## Decision 020 - Timed Build Cleanup And External Hygiene Boundary

Problem: A single throttled build attempt was allowed after the guard cleared, but `dotnet build Hecton8.slnx --no-restore` timed out after about 304 seconds and left its parent/MSBuild dotnet tree running. The project orphan-meta scan also found two tracked prefab metas outside the 1715 domain.
Solution: Stopped only the timed-out 1715 build process tree. Rechecked active dotnet processes and identified the remaining work as external `DataMonolithBakeCli` and another `Hecton8.slnx` build. Left the tracked orphan prefab metas untouched because they belong to `Assets/_Project/Prefabs`, outside the assigned equipment/interactions domain during parallel agent work.
Rejected Alternatives: Launching a second build under 100% CPU, killing external dotnet work, or deleting another domain's tracked prefab metas. Those choices would violate compile throttling or the domain boundary.
Scalability potential: Low/Middle/High/Ultra content unchanged. This is process and repository hygiene only.
Hardware Impact: i3/MX350 runtime unchanged. Host load was reduced by removing the stale build tree; measured frame/profiler data remains unavailable because external compile/data-monolith work is still active.

## Decision 021 - Socket Guard And Bake Identity Finalization

Problem: The metadata socket publication path checked the compaction fence before acquiring the mutation guard, but not immediately after acquisition. The prefab `bakeHash` also ignored quality and material identity, so two different static mesh/material outputs could share the same metadata hash.
Solution: Added a post-acquire `IsCompactionFenceActive` check in `TryReplaceSocketRange` before resolving DataVault socket buffers. Changed `EquipmentPropBaker1715` to resolve a bake hash from output name, seed, quantized `GlobalQualityWeight`, and shared material path. Added a hard failure if no shared material can be resolved.
Rejected Alternatives: Trusting the earlier fence check, or keeping a name/seed-only hash. The earlier check allows a compaction fence to rise between acquisition and buffer resolution; a name/seed hash hides authored mesh/material drift from prefab metadata.
Scalability potential: Low/Middle/High/Ultra static outputs now carry distinct bake identity while preserving the same runtime authority route and DTO layout.
Hardware Impact: Runtime cost unchanged except shorter failure windows under compaction. Editor bake identity is more accurate; profiler data remains unavailable because an external build is active and CPU is above the compile threshold.

## Decision 022 - CSG Counterbore And Baked ABI Proof

Problem: The generated panel had rectangular recesses and bevels, but lever/valve bases still lacked explicit circular subtractive cut surfaces. Baker struct validation also proved sizes only, so a field reorder could preserve byte size while corrupting MeshData/Burst layout.
Solution: Added deterministic `AppendCsgCircularCounterbore` geometry for lever and valve bases: outer wall, inner wall, and annular bottom surfaces emitted into the single static mesh. Updated NativeList capacity estimates from the same continuous geometry profile. Added exact `UnsafeUtility.GetFieldOffset` checks for `EquipmentPropVertex1715`, `EquipmentBakeMetrics1715`, and `TopologyValidationResult1715`.
Rejected Alternatives: Adding a runtime boolean/CSG dependency, using child mesh rings, or trusting struct size alone. Runtime CSG violates the offline-only mandate; child meshes increase renderer/material churn; size-only validation misses ABI drift.
Scalability potential: Low keeps 12-segment circular cuts; Middle/High/Ultra increase segment count continuously with the existing quality weight. Runtime socket truth, collider proxies, and DTO layout do not change.
Hardware Impact: i3/MX350 runtime remains one shared-material static mesh plus primitive colliders. Editor bake cost rises only with authored quality; no steady-state allocation path is added.
