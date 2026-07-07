# HECTON-8 AI Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: AI director, creature cognition, navigation, flocking, stimulus memory, encounter pacing, pathfinding, and AI proof gates.

## First-20 Route Hook

- First-20 moment: first shallow exit and hazard beat where creature pressure, sonar uncertainty, and Director pacing make the route feel alive without unavoidable failure.
- Route blocker removed: prevents AI work from drifting into random patrols or late-game-only brains before the opening swim, resource, tool, and return-path decisions are legible.
- Proof class: STATIC_DOC hook only; acceptance still requires encounter capture, Director/cognition telemetry, profiler/GC evidence, and first-route fairness proof.

## Prime Law

AI exists to create pressure, doubt, pursuit, avoidance, ecology, and evidence. It does not exist to run expensive brains everywhere. HECTON-8 rejects random patrol monsters, omniscient enemies, fake intelligence hidden behind jump scares, and AI that spends frame time without changing player decisions.

The player should infer AI through sound, light response, route pressure, disturbed ecology, sonar uncertainty, and remembered stimulus, not through creatures sprinting directly at the camera because a trigger fired.

## Ownership And Tick Cadence

AI has explicit lanes:

- Director pacing: cold cadence, usually 1 Hz or lower, owns stress, tokens, phase, and spawn permission.
- Cognition: batched Burst jobs over active entities, owns drives, memory, state choice, and intent.
- Navigation: path requests, threat-grid snapshots, flow fields, funnel smoothing, and path validity.
- Flocking/swarm: GPU or data-oriented spatial hash, owns group motion only when swarm behavior matters.
- Presentation: animation, audio, VFX, and UI consume AI intent; they do not mutate cognition truth.

No AI system may own a private `Update` loop for gameplay truth. No AI system may search scene objects in hot paths.

## Director Law

The AI Director controls pressure curve, not cheap surprise. It uses tokens, phase timing, route context, player stress, oxygen/power/clarity state, depth, noise, light, and recent encounter memory.

Required phases:

- buildup;
- peak;
- decay;
- relax.

The Director must never spawn threats just because the game is quiet. Quiet is a resource. The correct question is what physical pressure the player is already under and whether another stimulus improves or flattens that pressure.

## Creature Cognition

Creature cognition must use bounded drives and state flags:

- hunger;
- fear;
- fatigue;
- threat;
- territory;
- curiosity;
- injury;
- stimulus memory.

Stimulus types must be explicit: acoustic, visual, chemical, electrical, pressure, blood, light, tool heat, hull vibration, and sonar ping. Memory is a ring buffer, not an unbounded list.

Creatures must have readable intent:

- ignore;
- investigate;
- stalk;
- flee;
- feed;
- defend;
- lure;
- attack;
- retreat.

If a creature has only idle, chase, and attack, it is not a HECTON-8 creature.

## Navigation

Navigation must respect the medium. Underwater AI is 3D, occluded, current-influenced, and sonar-limited.

Required:

- non-negative path costs;
- threat-grid or voxel snapshot reads instead of live physics raycasts in jobs;
- **3D Sparse Voxel Octree (SVO) A* Pathfinding**: Aquatic macro-navigation must use a multi-threaded A* algorithm traversing a разреженный воксельный октодерево (SVO) on Burst (checking `SDF > 0` for navigable void water), instead of a 2.5D surface NavMesh.
- **Steering Boids Micro-Movement**: Micro-movements and local obstacle avoidance must use data-oriented steering behaviors (Boids) and direct local SDF density checks.
- A* only where needed;
- weighted A* for distance LOD;
- flow-field fallback for distant entities;
- funnel/string-pulling smoothing for corridor paths;
- route invalidation when doors, floods, voxel edits, or pressure states change.

Forbidden:

- **NavMeshAgent Ban**: Using Unity `NavMeshAgent` or standard 2D NavMesh components is strictly banned (they cannot support 3D vertical movement and dynamic voxel caverns).
- Bezier smoothing that cuts through geometry;
- live `Physics.Raycast` inside path jobs;
- direct path recalculation every frame;
- AI that ignores doors, flood, light, sound, or pressure state.

## Flocking And Swarms

Swarms are atmosphere only until they affect visibility, route, threat, or resource decisions. Background swarm motion belongs on GPU or low-cadence data jobs.

Rules:

- fixed-capacity spatial hash;
- overflow discard or load-shed, never stall;
- behavior state packed into bitfields;
- group response to light, sound, flow, and predator proximity;
- no per-entity GameObject logic for swarm members;
- no collision truth for decorative swarm particles.

## 2026-06-05 Static Source Anchors

Evidence class: STATIC_SOURCE only. Compile, Unity import, Play Mode, profiler, GC, encounter capture, and player-build proof remain PENDING VERIFICATION.

| Runtime | Owner / boundary | Static route | GlobalQualityWeight consequence | Missing proof |
|---|---|---|---|---|
| `Assets/_Project/Scripts/HectonBoidController.cs` | `Hecton8.AI.GPU`, DataVault owner `SystemID.AIEcology`; GPU swarm/flocking presentation owner. It does not own creature cognition, collision truth, save identity, or Director authority. | Implements `ITickable`, `IUpdatable`, `ILateFrameTickable`, and hot-swap listener; registers late-frame environment route. Uses ping-pong boid `GraphicsBuffer`s, spatial grid buffers, visible index/indirect args buffers, `Graphics.RenderMeshIndirect`, GPU frustum culling, and owner-local material state. Owns a 300-frame DataVault black-box ring at `BufferID` 71979 with dump target `Docs/AgentLogs/Dump_1301_Boids.bin`. Consumes `SignalBus<AcousticPingSignal>` frame snapshots and caches `IFoveatedSimulationDirector`/flow read models through `GlobalRegistry` hot-swap. | Reads `MathLodRuntimeConfig.GlobalQualityWeight` or `HomeostasisBrain.GlobalQualityWeight`; derives social LOD weight and foveated VAT time scale. Foveated tier may freeze/peripheral-scale presentation, but creature truth, player fairness cues, and acoustic signal meaning must not change by quality tier. | No GPU profiler, GCMonitor, RenderDoc/Frame Debugger, swarm capture, signal overflow proof, black-box dump artifact, or gameplay fairness proof was provided by this static audit. |

## Player Fairness

AI may be hostile, not dishonest.

Required fairness cues:

- acoustic cue before close threat;
- partial silhouette or sonar read before lethal contact where possible;
- readable reason for escalation;
- safe explanation after failure through evidence or black-box trail;
- route pressure that can be answered by player tools.

The Director must not chain unavoidable failures. If oxygen, power, clarity, route, and enemy pressure all collapse together, the system must create a readable recovery path or explicitly enter catastrophic mode.

## Scalability

`GlobalQualityWeight` scales AI cadence, active presentation density, optional memory richness, flock sample density, and Director staging breadth. It never changes gameplay truth ownership, perception fairness, save identity, command authority, or the meaning of a creature state.

Compact uses fewer active cognition entities, wider cold tick cadence, flow-field steering, lower swarm density, and stronger audio/UI telegraphs. Middle uses full local cognition with conservative Director tokens. High adds richer memory and ecology. Ultra adds denser encounter staging and more secondary swarms, not omniscient or unbounded brains.

## Proof Artifacts

AI work must provide:

- Director phase, token budget, and cadence;
- active cognition count and maximum admitted count;
- path request count, path failure count, and load-shed behavior;
- stimulus memory schema and capacity;
- black-box fields routed through `telemetry.md`;
- Compact and High encounter capture when player-facing behavior changed;
- profiler/GC proof for runtime AI changes;
- fairness cues: what the player heard, saw, or inferred before danger escalated.

If these artifacts are absent, the AI claim remains `PENDING VERIFICATION`.

## Rejection Gates

Reject:

- random patrol routes as final behavior;
- omniscient player tracking;
- AI spawns with no token budget;
- pathfinding with live scene searches;
- per-creature managed allocations;
- swarms built from individual GameObjects;
- AI reports without active counts, tick cadence, token budget, path cost, and load-shed behavior;
- horror that comes only from a scripted scream.

## Vertex Animation Textures (VAT) Doctrine — Fauna Animation

Agents assigned fauna or swarm animation must NOT use `Animator` components or `SkinnedMeshRenderer`.

**Why:** 10,000 SkinnedMeshRenderers destroy the CPU on bone matrix calculation before the GPU even starts rendering. Animator runs on the main thread. This kills Zero-GC and 60 FPS targets in one move.

**Required: Vertex Animation Textures (VAT)**

Small fauna (fish schools, rays, microorganisms, jellyfish): animation is baked into a texture (position offsets per vertex per frame) in Houdini or Blender. The creature shader reads that texture and displaces vertices on the GPU. The CPU knows only a `float3` position passed via `BatchRendererGroup`. Main thread cost: zero.

**Required: Procedural IK for Leviathans**

Large creatures (tentacles, spider crabs, multi-limb horrors) use procedural IK computed in a Burst job — no skeleton, no `Animator`. Algorithms: FABRIK (Forward And Backward Reaching Inverse Kinematics) or CCD (Cyclic Coordinate Descent). Joints are `float3` positions in a `NativeArray`. The job produces final joint positions that conform to the voxel terrain surface each frame.

**Swarm agent exam:** Any external agent assigned scatter or fauna animation must demonstrate understanding of BRG and VAT before receiving the task. An agent proposing `Animator` or `SkinnedMeshRenderer` for mass fauna is immediately reassigned.

## Zero-GC UI: The Babel Protocol

`SuitHUDV4CanvasOverlay.cs` (369 KB) was caught doing `text = "Depth: " + depth.ToString()` 60 times per second. Each concatenation allocates a new `string` on the managed heap. At 60 Hz, Garbage Collector runs every ~60 seconds with 20 ms spikes. In a VR headset this induces nausea.

**Banned in all hot UI paths:**

- `string.Format(...)` with numeric args.
- `text = "Label: " + value.ToString()`.
- `TextMeshPro.text = ...` with any string concatenation.

**Required: Babel Protocol**

```csharp
// REQUIRED pattern for hot numeric HUD elements:
Span<char> buf = stackalloc char[32];
depth.TryFormat(buf, out int len, "F1");
_tmpText.SetCharArray(buf.Slice(0, len)); // zero allocations
```

Use `ReadOnlySpan<char>`, `stackalloc`, and `CharBufferPool` for all hot HUD text. Numbers are formatted via `int.TryFormat` / `float.TryFormat` directly into the buffer, then fed to `TextMeshPro.SetCharArray()`. Zero allocations, 60 Hz safe.

**Diegetic Terminal Click Mapping:** Standard `Screen Space - Overlay` Canvas is banned. Terminal screens render as `RenderTexture` on in-world 3D objects. User interaction: `Physics.Raycast` from camera center (VR: from finger tip), convert hit UV to screen coordinates, emit synthetic click. Full immersion, zero overlay UI.

## Acceptance Sentence

AI is accepted only when it is bounded, readable, data-owned, fair under pressure, scalable across hardware, and capable of creating dread through systems instead of cheap triggers.

