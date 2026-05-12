# HECTON8_MOTION_IK Log

Status: PENDING VERIFICATION

## 2026-05-11 Adaptive Motion / Anti-Bloat Pass

What was wrong:
- Main fauna/predator material path lacked the exact `_HitFlash` scalar contract; only boid VAT shader had GPU hit-flash semantics.
- Adaptive leviathan IK cadence used `QualityTier` rather than the explicit `ScalabilityTier` alias requested by the prompt.
- Footstep speed normalization still had one scalar division after the LCG/approx-magnitude work.
- One active gameplay file had a transliterated comment that matched Archivarius content hygiene debt.

What was done:
- Added `_HitFlash` to `Hecton_LeviathanOrganic.shader` with shader-side bloat and emission `lerp`.
- Extended `FaunaBrain` fauna presentation material masks to `ushort`, cached `_HitFlash`, triggered it on damage, and decayed it in the existing presentation path.
- Changed `ProceduralLeviathanSpineIK` adaptive cadence to read `GlobalRegistry.ScalabilityTier`.
- Changed `PlayerFootstepAudio` speed normalization to reciprocal multiply.
- Replaced one `DirectorMissionBridge` transliterated comment with ASCII English.
- Created `Docs/Tasks/Status_HECTON8_MOTION_IK.md` and this rationale/log trail for the current assignment.

Cinematic cheats used:
- Stateless added-mass scalar instead of velocity history.
- Triangle-wave tail surge instead of sine.
- Hash-seeded triangle corpse corkscrew instead of Unity random.
- VAT two-frame shader blending instead of CPU animation frames.
- Shader breathing chest offset instead of Animator breathing.
- 10/30Hz adaptive IK cadence instead of every-frame distant FABRIK.
- `rsqrt` FABRIK pole projection instead of exact sqrt/FromToRotation.
- Four-joint ragdoll handoff instead of full ragdoll stack.
- Shader `_HitFlash` bloat/emission instead of Animator hit reaction.
- LCG footsteps and approximate planar magnitude instead of `UnityEngine.Random` and sqrt.

Estimated microseconds saved:
- Stateless added-mass history purge: ~0.15-0.35 us/player fixed tick.
- Triangle tail surge: ~0.03-0.08 us/fauna tick.
- Adaptive IK low/distant cadence: ~20-80 us per skipped leviathan solve window.
- FABRIK `rsqrt` pole projection: ~0.05-0.20 us/chain.
- Death corkscrew deterministic hash: ~0.3 us/death event plus deterministic stability.
- VAT shader blend: ~5-20 us/swarm batch CPU work avoided.
- Shader breathing fake: ~5-15 us/frame Animator overhead avoided.
- Four-joint ragdoll: ~100-300 us/death event avoided versus full ragdoll.
- Hit-flash shader fake: ~5-20 us/damage event avoided.
- LCG + approximate footstep speed: ~0.08-0.20 us/footstep event.
- Reciprocal footstep scalar: ~0.02-0.05 us/footstep event.
- Quantized breathing global publish: ~1-5 us unchanged frame.

Scalability Matrix behavior:
- High/Ultra: leviathan IK cadence is 2 frames, approximately 30Hz at a 60Hz render cadence.
- Low/Mx350/Unknown or viewer distance >20m: cadence is 6 frames, approximately 10Hz, with visual continuity delegated to shader/VAT presentation.
- Constrained tentacles stay Burst/NativeArray/32-byte structs; no managed collection or Transform hierarchy reads inside the tentacle job.

Verification:
- `dotnet build Hecton8.Core.csproj -nologo -clp:WarningsOnly -maxcpucount:1 /nodeReuse:false /p:UseSharedCompilation=false` completed with 0 warnings / 0 errors.
- Static target scans found no `HydrodynamicAddedMassVelocity`, `math.sqrt`, `math.distance`, `FromToRotation`, `UnityEngine.Random`, `Random.insideUnitSphere`, `math.sin/cos`, or `AnimationEvent` in the target Fauna/Gameplay/IK path.
- Unity Console, import, PlayMode, GCMonitor, Profiler, and visual validation are PENDING.
