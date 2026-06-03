# Rationale 1736 - Hazard, Flare, and Decal Anchor Assembler

## Decision 001 - Missing Domain File
Problem: The directive names `Docs/Actual Domains of Project.txt`, but the active tree does not contain it.
Solution: Use the XML prompt as the current hard boundary: editor factory under `Assets/_Project/Editor/Assembly/`; runtime support only under `Assets/_Project/Scripts/Gameplay/` or `Assets/_Project/Scripts/Environment/`.
Rejected Alternatives: Editing broad World/Lighting/VFX owners to satisfy prompt language would exceed the granted domain.
Scalability potential: Low/Middle/High/Ultra unaffected; this is ownership containment.
Hardware Impact: 0 us runtime.

## Decision 002 - Offline Factory Route
Problem: Manual hazard prefab assembly allows stray `ParticleSystem`, runtime `AddComponent`, bad layers, and local light shadows.
Solution: Implement an editor-only `HazardPrefabFactory` matching existing factory patterns: discover meshes/metadata, create temporary root, assemble, validate, save, and destroy root in `finally`.
Rejected Alternatives: Runtime prefab assembly or scene-time fix-up scripts; both create active executable overhead and hidden dependencies.
Scalability potential: Low keeps static mesh plus pooled VFX anchors; Middle adds decals/lights when quality allows; High/Ultra can spend saved runtime cost on denser pooled effects and richer practical lighting.
Hardware Impact: Estimated MX350 gain is avoiding per-hazard runtime setup and stray particle simulation; steady-state saved cost depends on count, target 20-80 us per dense hazard cluster plus reduced GPU shadow cost.

## Decision 003 - Damage Router Metadata
Problem: No existing `DamageRouter`, `HazardMetadata`, `LightCullingProxy`, or `ThermalVentRuntime` type exists in the target slices.
Solution: Add minimal runtime components. `DamageRouter` serializes a blittable packet and exposes pure getters; `HazardMetadata` stores anchor references and FNV-1a effect hashes; `LightCullingProxy` performs presentation-only culling in `LateFrameTick`.
Rejected Alternatives: New global damage DataVault lane, new EventBus traffic, or hot scene searches from damage code.
Scalability potential: Low uses trigger truth and emissive-only visuals; Middle enables culled light/decal presentation; High/Ultra allow higher light intensity/flicker cadence without changing damage payload.
Hardware Impact: On i3/MX350, direct serialized references avoid `GetComponentInChildren`/`Find` at runtime and local light shadow suppression avoids shadow-map submissions.

## Decision 004 - Trigger Radius Formula
Problem: Hazard damage must be readable and fair before visual contact while staying cheap.
Solution: Center damage trigger at first emission anchor when present, otherwise combined bounds center. Radius = max(bounds extents magnitude, metadata radius) + metadata padding or 1.5 m fallback, clamped finite.
Rejected Alternatives: MeshCollider damage volume, per-frame distance casts, or exact fluid/steam simulation.
Scalability potential: Low/Middle/High/Ultra use identical damage truth; only presentation density changes.
Hardware Impact: One primitive trigger per hazard is O(1) broadphase-friendly; no runtime mesh cooking.

## Decision 005 - Fail Closed On Missing Layers
Problem: `Hazard_Trigger` and `World_Static` are mandated by the batch prompt, but the project layer file currently does not define those exact names.
Solution: The factory refuses trigger/collision assembly and records a fatal violation when either layer is absent.
Rejected Alternatives: Editing `ProjectSettings/TagManager.asset` from this agent or silently falling back to `Default`; both would hide ownership and physics-route defects.
Scalability potential: Low/Middle/High/Ultra all require identical collision truth; quality scaling cannot change physics layers.
Hardware Impact: Prevents wrong-layer broadphase checks and prevents hidden runtime fix-up. Estimated gain is correctness, not frame time.

## Decision 006 - Pooled VFX Anchors Only
Problem: Nested `ParticleSystem` components break pooled VFX ownership and create per-prefab simulation cost.
Solution: Prefabs receive only `VFX_Anchor` transforms plus `HazardMetadata` bindings with effect IDs/hashes; validator rejects any `ParticleSystem`.
Rejected Alternatives: Authoring particle emitters in each prefab or spawning particle systems from hazard runtime scripts.
Scalability potential: Low uses sparse pooled steam/fire; Middle raises cadence; High/Ultra can raise particle density in the pool without changing prefab topology.
Hardware Impact: On i3/MX350 this avoids per-prefab particle component overhead; expected saving is 20-200 us in dense hazard clusters depending on effect count.

## Decision 007 - Continuous Presentation Culling
Problem: Local hazard lights and decals are expensive on MX350 when the player is far away, but binary quality tiers are forbidden.
Solution: `LightCullingProxy` and `ThermalVentRuntime` consume continuous `HomeostasisBrain.GlobalQualityWeight`; they scale intensity/range/decal fade and cull by distance without changing damage truth.
Rejected Alternatives: Always-on lights, low/ultra bool switches, or gameplay damage scaling through quality.
Scalability potential: Low disables/de-emphasizes light/decal presentation; Middle keeps nearby low-range presentation; High expands range/intensity; Ultra spends saved cycles on stronger flicker and decal visibility.
Hardware Impact: Avoids unnecessary local-light and decal updates beyond cull range; target saving 8-18 us per hidden hazard presentation update plus GPU shadow-map elimination.

## Decision 008 - Shadow Suppression
Problem: Hazard flares/vents need local light feel without realtime shadow cost or accidental renderer shadow submissions.
Solution: The factory sets `Light.shadows = LightShadows.None`, `MeshRenderer.shadowCastingMode = Off`, `receiveShadows = false`, and validator rejects violations.
Rejected Alternatives: Keeping LOD0 shadow casting for visual richness; this contradicts the hazard/flaring performance budget.
Scalability potential: Low/Middle retain emissive/decal/light cues; High/Ultra can spend budget on pooled volumetric-looking VFX instead of shadow maps.
Hardware Impact: On MX350 this prevents local-light shadow submissions and shadow caster traversal for these hazard props.

## Decision 009 - Scale-Aware Damage Packet
Problem: Designers can scale prefab roots; a serialized local radius alone would under-report or over-report damage when scale changes.
Solution: `DamageRouter.ReadPacket()` multiplies stored radius by max absolute lossy scale while keeping anchor coordinates local.
Rejected Alternatives: Baking world-space anchors or forbidding prefab scale; both are brittle for procedural placement.
Scalability potential: Low/Middle/High/Ultra use the same damage truth and same router packet layout.
Hardware Impact: Three finite checks plus max-axis multiply; expected under 1 us per read, no allocation.

## Decision 010 - Compile And Tooling Gate
Problem: The Unity MCP bridge reports `ping not answered`; host CPU sampled above 50% earlier and Unity's `VBCSCompiler` `dotnet` process PID 49204 remained active.
Solution: Do not launch manual `dotnet build`; use the last editor compile log as partial proof and mark menu dry-run/build execution as tooling-blocked.
Rejected Alternatives: Starting another build under load or claiming a menu run that did not happen.
Scalability potential: Not runtime-related.
Hardware Impact: Prevents workstation contention and duplicate compiler load.

## Decision 011 - Dry Run Layer Failure
Problem: Once MCP recovered, the actual factory dry run discovered 50 hazard mesh groups but failed all of them because `Hazard_Trigger` does not exist.
Solution: Preserve fail-closed behavior. The factory report is now authoritative evidence that the tool refuses unsafe prefab creation until the physics layer is owned and defined.
Rejected Alternatives: Auto-creating project layers, using `Default`, or saving prefabs with unresolved trigger routing.
Scalability potential: Low/Middle/High/Ultra all need the same physics route. Visual quality scaling cannot alter trigger ownership.
Hardware Impact: Avoids wrong-layer broadphase and runtime correction passes. Runtime cost remains 0 because no invalid prefabs were emitted.

## Decision 012 - Repeat Request Boundary
Problem: The repeated `1736` request still requires exact `Hazard_Trigger` and `World_Static` layers, but `AGENTS.md` forbids changing project settings/tags/layers from this task.
Solution: Do not mutate `ProjectSettings/TagManager.asset`. Keep the factory strict and report the route blocker.
Rejected Alternatives: Substituting existing `TriggerZone ` or `Terrain ` layers, adding layers via MCP, or weakening factory validation.
Scalability potential: Low/Middle/High/Ultra remain deterministic because invalid prefab output is blocked before runtime.
Hardware Impact: 0 us runtime; prevents future broadphase and damage-route ambiguity.

## Decision 013 - Packet Layout And Channel Contract
Problem: The damage packet is the hazard/router contract; a layout drift or ambiguous damage channel blocks deterministic consumers and ARM64 alignment proof.
Solution: Validate `DamageRouterPacket` with `UnsafeUtility.SizeOf<DamageRouterPacket>()`, force the 64-byte / 8-byte contract, and bind the channel through one `IntegrityDamageChannel` constant.
Rejected Alternatives: Depending only on `[StructLayout]` or scattered enum casts.
Scalability potential: Low/Middle/High/Ultra share identical damage truth; presentation quality does not touch packet layout.
Hardware Impact: 0 B steady-state allocation; editor validation cost is sub-microsecond compared with prefab assembly.

## Decision 014 - Shared Editor Compile Blocker
Problem: Unity 6000 rejected `Renderer.receiveGI` in a shared editor factory, preventing clean verification of the hazard factory menu path.
Solution: Use editor-only serialized `m_ReceiveGI` inspection in `WreckagePrefabFactory` validation while preserving the `ReceiveGI.Lightmaps` requirement.
Rejected Alternatives: Ignoring the compile blocker, weakening GI validation, or launching a manual `dotnet build` while Unity compiler processes were active.
Scalability potential: Runtime unaffected; editor validation remains deterministic across renderer API surface differences.
Hardware Impact: 0 us runtime; removes compile stall without adding player code.

## Decision 015 - Canonical Damage Packet Bridge
Problem: Hazard triggers had a local serialized `DamageRouterPacket`, but first-party consumers terminate at canonical `Hecton8.Core.DamagePacket`.
Solution: Add pure `TryBuildDamagePacket` projection on `DamageRouter`, keep radius scale-aware, and validate both packet layouts through `UnsafeUtility.SizeOf<T>()`.
Rejected Alternatives: Creating a new DataVault damage route or attaching a second damage behaviour to prefabs. Both duplicate authority.
Scalability potential: Low/Middle/High/Ultra share identical damage truth; quality only changes light/decal/VFX presentation.
Hardware Impact: Direct struct projection, no allocations, no registry lookup, no scene search.

## Decision 016 - Culled Presentation Writes
Problem: Culled or low-quality hazard lights were disabled but still received intensity/range/fade assignments every visual tick.
Solution: Gate light intensity/range and decal fade writes behind active presentation checks in `LateFrameTick`; keep `LightShadows.None` as a cheap drift guard.
Rejected Alternatives: Always writing all presentation properties or moving visual work into simulation phases.
Scalability potential: Low skips practical light/decal writes when invisible; Middle/High/Ultra spend presentation budget only near the player.
Hardware Impact: Avoids 3-6 Unity native property writes per hidden hazard presentation tick on low-end GPUs/CPUs.

## Decision 017 - Drone Attachment Compile Wall
Problem: Shared `Hecton8.Project.Editor` compilation was blocked by missing drone attachment DTOs used by `DronePrefabFactory`, preventing hazard verification from reaching a stable editor state.
Solution: Restore `DroneAttachmentMetadata`, `DroneAttachmentAnchorDescriptor`, `DroneAttachmentRuntimeData`, `DroneAttachmentKind`, and `DroneAttachmentFlags` in the Construction namespace with a 96-byte / 8-byte runtime layout gate.
Rejected Alternatives: Removing drone attachment assembly code, stubbing factory calls, or leaving the whole editor assembly red.
Scalability potential: Runtime attachment data is direct arrays and immutable descriptors; high-tier VFX can consume richer anchors without changing prefab topology.
Hardware Impact: 0 us hazard runtime; resolves a compile blocker without adding tick/update loops.

## Decision 018 - Presentation Fails Closed Without Player Context
Problem: `LightCullingProxy` could keep hazard practical lights enabled when the player runtime context was unavailable, turning bootstrap/service stalls into full-cluster local-light cost.
Solution: Require both sufficient `GlobalQualityWeight` and a valid cached player pose before distance hysteresis can enable presentation.
Rejected Alternatives: Defaulting to visible on missing player context or polling `GlobalRegistry.Get<T>()` from `LateFrameTick`.
Scalability potential: Low/Middle disable remote or unresolved hazard presentation; High/Ultra still expand cull distance only after player pose exists.
Hardware Impact: On i3/MX350 this prevents accidental activation of every hazard light/decal during service initialization.

## Decision 019 - Culled Thermal Pulse Math
Problem: `ThermalVentRuntime` calculated pulse phase even when quality already disabled practical light output.
Solution: Move triangle-wave pulse calculation behind the light quality cutoff and keep decal cutoff independent.
Rejected Alternatives: Keeping pulse math unconditional or moving visual pulse into a simulation tick.
Scalability potential: Low skips pulse entirely; Middle/High/Ultra spend pulse math only when the visual is actually visible enough to matter.
Hardware Impact: Saves one time read and several scalar operations per culled vent visual tick.

## Decision 020 - Shared Compile Recovery
Problem: Unity compile verification was blocked by a shared editor import regression outside the hazard files: missing `Hecton8.Gameplay` in `PrefabAssemblerEngine` for `BaseModule`.
Solution: Add only the missing `Hecton8.Gameplay` using directive; current `LogisticsPipeTransportScheduler` disk state contains no `NativeArray`/`JobHandle` sites, so no construction edit is retained or required.
Rejected Alternatives: Manual `dotnet build`, broad asmdef edits, or reflection-based component attachment in the assembler.
Scalability potential: No gameplay scaling change; the editor toolchain can compile and run hazard validation again.
Hardware Impact: 0 us runtime; removes compile blockers without adding executable player work.

## Decision 021 - Single Presentation Scalar Owner
Problem: Factory-created hazards attached both `LightCullingProxy` and `ThermalVentRuntime` to the same light/decal pair; dispatcher order could let culling overwrite thermal pulse scalars or thermal re-enable culled presentation.
Solution: Add a scalar-management switch to `LightCullingProxy`, pass `false` from `HazardPrefabFactory`, and pass the proxy reference into `ThermalVentRuntime`. Culling owns enable/disable. Thermal owns intensity/range/decal fade.
Rejected Alternatives: Relying on dispatcher registration order, removing thermal pulse entirely, or merging both scripts into a larger duplicated controller.
Scalability potential: Compact can disable presentation cleanly; Middle/High/Ultra keep pulse richness without losing distance culling.
Hardware Impact: Avoids redundant scalar writes and removes nondeterministic light state churn in dense vent fields.

## Decision 022 - Snapshot-Only Hazard Distance Culling
Problem: Hazard light culling used `PlayerTransform.position` when the player pose snapshot was unavailable, reading scene state from a presentation tick and failing open to transform fallback behavior.
Solution: Require `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot` for hazard culling; missing or non-finite snapshot disables presentation.
Rejected Alternatives: Polling `GameBootstrapper`, using scene transform fallback, or keeping lights visible until a player transform appears.
Scalability potential: Low/Middle avoid accidental cluster activation during bootstrap; High/Ultra still expand visibility only after authoritative pose exists.
Hardware Impact: Removes one fallback Transform position route per hazard culling tick and prevents full-cluster local-light cost on stale context.

## Decision 023 - Continuous Thermal Cadence
Problem: Visible thermal vents still wrote pulse, range, and decal scalars every visual tick even when quality was low and the player-readable change was minimal.
Solution: Map the continuous quality curve to a 1-5 frame update stride, with dirty caches bypassing the stride after re-enable. Property writes still use epsilon guards.
Rejected Alternatives: Binary low/ultra switches, coroutine throttles, or always-on per-frame scalar writes.
Scalability potential: Compact gets sparse but readable heat flicker; Middle updates more often; High/Ultra keep per-frame pulse when visual budget supports it.
Hardware Impact: Up to 80 percent fewer scalar presentation updates at the lowest visible quality without changing damage, trigger, or VFX anchor truth.

## Decision 024 - Unmanaged VFX Anchor DTO
Problem: Pooled hazard steam/fire VFX needed anchor pose and effect identity without copying managed strings or relying on Transform-heavy metadata as the only runtime route.
Solution: Add explicit 64-byte `VfxAnchorRuntimeData` with local pose, effect hash, hazard hash, anchor index, and flags; validate size/alignment with `UnsafeUtility.SizeOf<T>()` and expose it through `HazardMetadata.TryGetAnchorRuntimeData`.
Rejected Alternatives: A second persistent anchor table, managed `EffectId` copies during runtime handoff, or nested particle emitters under the prefab.
Scalability potential: Low can sample only hash/local pose for cheap pooled steam; Middle/High/Ultra can layer richer pooled effects while keeping the same anchor DTO.
Hardware Impact: 0 B managed allocation in the handoff path; avoids per-anchor string movement on i3/MX350 class hardware.

## Decision 025 - Decal Material Gate
Problem: `DecalProjector` accepted any shared material that passed broad URP/SRP checks, which could produce invisible or invalid hazard scorch/toxin decals.
Solution: Require decal material validation to include decal shader name or shader asset path proof in addition to shared asset and SRP batcher checks; validate both resolved source material and saved projector material.
Rejected Alternatives: Falling back to a generic Lit material, cloning a runtime decal material, or accepting projector setup without shader-route proof.
Scalability potential: Low/Middle/High/Ultra use the same decal material contract; quality can scale projector visibility/fade, not shader identity.
Hardware Impact: Editor-only validation cost; prevents runtime material replacement and broken projector draw attempts.

## Decision 026 - Proxy Authority Hygiene
Problem: Collision proxy prefabs may carry hidden scripts or rigidbodies from source art workflows, creating duplicate hazard authority or static prefab physics drift.
Solution: Strip `MonoBehaviour`s and `Rigidbody`s from copied collision proxies and make final validation reject `Rigidbody`, `HectonHazardSource`, and `EnvironmentalHazard` in generated static hazards.
Rejected Alternatives: Trusting artist proxy prefabs, attaching old hazard manager scripts beside `DamageRouter`, or allowing static hazard rigidbodies for convenience.
Scalability potential: Low/Middle/High/Ultra keep one trigger authority path; visual richness stays in anchors, decals, and light proxies.
Hardware Impact: Avoids hidden `Update`/registration paths and static rigidbody broadphase overhead in dense hazard clusters.

## Decision 027 - Repo Meta Hygiene Boundary
Problem: Full repository orphan scan reported stale `.mat.meta` files under `Assets/Shapes/Shaders/Generated Materials`, outside the hazard factory domain and not created by this pass.
Solution: Do not delete package/generated material metadata from another domain; record the exact hygiene failure and keep touched C# metadata verified.
Rejected Alternatives: Mass-deleting unrelated generated material metadata or claiming the repository-level meta gate is clean.
Scalability potential: Not runtime-related. The hazard factory's own prefab/script metadata remains valid.
Hardware Impact: 0 us runtime; prevents accidental asset identity churn outside the hazard assembler ownership boundary.

## Decision 028 - Presentation Runtime Self-Disable
Problem: A manually damaged or partially generated hazard could keep `LightCullingProxy` or `ThermalVentRuntime` registered in `LateFrameTick` without valid presentation targets.
Solution: Block late-frame registration when the required light/decal target is absent and make `ThermalVentRuntime` unregister if both presentation targets are lost.
Rejected Alternatives: Letting the tick run a null guard forever or performing scene searches to repair references.
Scalability potential: Low/Middle/High/Ultra all fail cold for broken prefabs; valid prefabs keep the same continuous quality scaling.
Hardware Impact: Removes a wasted late-frame callback per broken/manual hazard and avoids scene lookup repair paths.

## Decision 029 - Prefab Reference Graph Validation
Problem: The validator counted routers, lights, decals, and anchors, but did not prove that the runtime scripts referenced the same ownership graph after save.
Solution: Add public read-only reference accessors and require `HazardMetadata.Router`, `ThermalVentRuntime.Metadata`, `ThermalVentRuntime.CullingProxy`, light, and decal references to align with the single generated graph.
Rejected Alternatives: Count-only validation or merging culling and thermal runtime into a new monolithic script.
Scalability potential: Compact through Ultra share identical prefab graph truth; higher quality only increases scalar update richness, not ownership ambiguity.
Hardware Impact: Editor-only validation cost; prevents duplicate scalar writers and null target callbacks in dense hazard fields.

## Decision 030 - Hidden Vent Math Order
Problem: `ThermalVentRuntime` read quality and computed curve before proving that the presentation targets still existed and were not externally culled.
Solution: Hoist null-target and external-cull checks above quality/curve math.
Rejected Alternatives: Leaving small scalar work in the hidden path or adding a new scheduler lane.
Scalability potential: Low/Middle benefit most because more vents are culled; High/Ultra still pay full pulse math only for visible presentation.
Hardware Impact: Saves one global quality read and smoothstep scalar chain for each externally hidden vent visual tick.

## Decision 031 - Name-Derived Hazard Truth Defaults
Problem: `HazardProfile.FromName` used `toxic && !thermal`, so names like brine/toxic vent could be forced into thermal truth just because they contained a vent token.
Solution: Split name parsing into flare/fire, hydrothermal, coral, and caustic tokens. Flare/fire stays heat. Coral/brine/acid/toxic defaults to toxicity unless explicitly a flare/fire source. Hydrothermal tokens still drive steam/fire presentation and heat truth when no toxic token exists.
Rejected Alternatives: Keeping the broad shortcut, creating new hazard types, or adding a second runtime route for mixed hazards without an owner card.
Scalability potential: Low keeps cheaper short-range toxic bioluminescent cues; Middle/High/Ultra can expand flare/vent presentation via the same light/decal/VFX anchors without changing damage truth.
Hardware Impact: 0 B allocation and 0 us steady-state cost; only serialized editor defaults change.

## Decision 032 - Surface Material Fallback Scoring
Problem: Flare profiles requested emissive material names, but current material inventory includes hazard-pocket and rock-family materials more reliably than dedicated emissive hazard assets.
Solution: Keep emissive as the strongest flare signal, add hazard-pocket fallback scoring for non-toxic heat, and score rock-family materials for vent/geyser/smoker/sulfur profiles.
Rejected Alternatives: Generating new materials, cloning instances, or accepting arbitrary first-valid material selection.
Scalability potential: Compact gets readable silhouettes with existing shared materials; High/Ultra can swap authored metadata to richer emissive materials later without changing factory code.
Hardware Impact: Editor-only scoring; shared material usage preserves SRP batching and avoids runtime material churn.

## Decision 033 - Explicit Child Transform Identity
Problem: The trigger and practical-light children relied on default GameObject transform state, which is technically correct but weak as a prefab authoring invariant.
Solution: Write local rotation identity and local scale one explicitly for `TRIG_DamageZone` and `LIGHT_Hazard_Practical` during factory assembly.
Rejected Alternatives: Trusting default transform construction or adding runtime normalization scripts.
Scalability potential: All tiers get identical collision/light placement. Higher-tier presentation can scale intensity/range, not transform truth.
Hardware Impact: 0 runtime cost; prevents drift and avoids any future runtime repair path.

## Decision 034 - Non-Colinear VFX Anchor Rotation
Problem: Default VFX anchors point upward. Passing `Vector3.up` as both effective forward and up-vector into `Quaternion.LookRotation` creates a colinear basis and can produce unstable editor-time rotations.
Solution: Keep the authored forward as truth, but select `Vector3.forward` as the up-vector whenever the safe forward is nearly vertical.
Rejected Alternatives: Store identity rotation for all anchors, add runtime rotation repair, or rely on Unity's internal fallback.
Scalability potential: Low/Middle/High/Ultra all receive deterministic pooled VFX emitter orientation; richer tiers can layer more particles without different anchor math.
Hardware Impact: Editor-only scalar dot/abs branch; prevents runtime corrective transform writes.

## Decision 035 - Proxy And Decal Local Transform Closure
Problem: Instantiated collision proxy prefabs could carry source local transform drift, and decal roots did not explicitly prove identity scale.
Solution: Force copied collision proxies to root-local zero/identity/one and force decal local scale to one during assembly.
Rejected Alternatives: Accept source-prefab transform inheritance, normalize at runtime, or add a separate proxy wrapper object.
Scalability potential: Low keeps primitive collision cheap and predictable; High/Ultra can improve visuals through decals/lights/VFX, not collider/decal root drift.
Hardware Impact: 0 runtime cost; avoids future transform normalization in startup or visual sync.

## Decision 036 - Metadata-Bound VFX Anchor Proof
Problem: The prefab validator only counted `VFX_Anchor` transform names. It did not prove that `HazardMetadata` actually points to those anchors with finite DTO data and usable effect hashes for particle pooling.
Solution: Validate every metadata anchor through `TryGetAnchorRuntimeData`, requiring a `VFX_Anchor` reference, finite local pose/forward, non-zero effect hash, and identity local scale.
Rejected Alternatives: Name-only validation, managed effect-id string checks in runtime consumers, or creating a second unmanaged anchor registry in the factory.
Scalability potential: Compact can spawn minimal pooled steam/fire from hash/local pose; Middle/High/Ultra can add denser pooled visuals through the same DTO contract.
Hardware Impact: Editor-only validation cost; preserves 0 B steady-state pooled VFX handoff and avoids per-anchor scene search.

## Decision 037 - Metadata Transform Drift Gate
Problem: `HazardMetadata` could serialize local VFX anchor pose data that no longer matched the actual invisible anchor transform after manual prefab edits or source drift.
Solution: Extend saved-prefab validation to compare each runtime DTO's local position and forward against the referenced transform's local position and local rotation forward vector, plus hazard hash/index consistency.
Rejected Alternatives: Trust serialized metadata alone, search child transforms at runtime, or create a duplicate unmanaged anchor registry.
Scalability potential: Low/Middle/High/Ultra all consume one authoritative anchor fact; higher tiers can spawn denser pooled effects without a second pose route.
Hardware Impact: Editor-only vector checks; prevents runtime repair/search and keeps pooled VFX handoff 0 B.

## Decision 038 - Fail-Closed Culling Hysteresis
Problem: `LightCullingProxy` kept `_visible` true when quality was below threshold or player pose was unavailable. When context returned, the next tick used the wider off-distance hysteresis threshold.
Solution: Reset `_visible` to false whenever culling cannot prove a visible, quality-qualified player snapshot. Re-entry must pass the stricter on-distance gate.
Rejected Alternatives: Leaving stale hysteresis state, using player transform fallback, or polling scene state to guess player position.
Scalability potential: Low/Middle avoid accidental practical-light activation during service stalls; High/Ultra still get larger quality-scaled cull range once the authoritative snapshot exists.
Hardware Impact: 0 B allocation; prevents unnecessary light/decal activation after bootstrap or registry hot-swap gaps.
