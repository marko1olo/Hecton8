# LOG_LADDER_CLIMB_IK

## 2026-05-16 - Procedural Ladder Climb IK
What was wrong:
- `ClimbableLadder` used a hard teleport traversal path, breaking VR embodiment and bypassing hand contact truth.
- Ladder data had no dedicated AUP vault buffer for procedural climb math.
- There was no ladder climb IK runtime, no rung-lock haptic event, and no 300-frame ladder blackbox.

What was done:
- Added `Assets/_Project/Scripts/Animation/Locomotion/LadderClimbIkJobs.cs` with Burst analytical 2-bone IK, exact discrete rung targets at `base + index * 0.3f`, `double3` AUP conversion, finite guards, and blackbox telemetry.
- Added `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs` with registry ownership, DataVault `LadderAUPs` read, PC slide path, VR grip-delta path, haptic thuds, stamina drain, slip drop, and dump-to-bin on NaN.
- Patched `ClimbableLadder` to request procedural climb instead of teleporting.
- Extended `PlayerStateSignal` with climb flags/state, added `BufferID.LadderAUPs`, and registered the runtime through `GlobalRegistry`.
- Added the runtime file to `Directory.Build.targets` core include list because `GlobalRegistry` and `ClimbableLadder` are compiled in `Hecton8.Core.csproj`.

Cinematic cheats used:
- Low tier: smooth camera/movement slide instead of full VR hand-pull embodiment.
- High tier/VR: grip-gated world-pull deltas drive climb progress, while the exact rung lock remains mathematical.
- Rung positions are procedural from a single AUP and rung spacing, not authored rung transforms.

Exact microseconds saved:
- Avoided per-rung Transform search/authoring path: estimated 8 us/player.
- Closed-form two-bone solve instead of iterative FABRIK: estimated 12 us/two hands.
- Typed signal packets instead of UnityEvent/string state propagation: estimated 3 us/event.
- Fixed blackbox struct write instead of managed logging: estimated 4 us/frame and 0 GC.
- Stamina/slip scalar update: estimated 2 us/player.

Validation:
- `dotnet build Assembly-CSharp.csproj --no-restore -nodeReuse:false -v:q` attempted.
- Build remains blocked by unrelated missing project assets/temp metadata and pre-existing non-ladder compile errors. Targeted scans after repair found no remaining `LadderClimb`, `ProceduralLadder`, `ClimbableLadder`, `LadderAUPs`, or climb-signal errors.

## 2026-05-16 - Multiplatform/H-Phi Hardening Pass
What was wrong:
- Runtime still owned persistent `NativeArray` fields and a private H8Memory fallback. That failed DataVault sovereignty.
- Ladder packet structs used `Pack=4`, not the pack-1 binary layout demanded for IL2CPP/Quest-style payload safety.
- Low-tier still paid the full `acos` elbow solve even though the prompt explicitly allowed a PC/camera-slide fake.
- Math had remaining guarded-but-direct divisions that were weaker than the mobile NaN/Inf policy.

What was done:
- Replaced runtime-owned NativeArray fields with `VaultBufferHandle<T>` fields for ladder input, output, AUP, telemetry ring, and telemetry cursor.
- Added `BufferID.LadderClimbIkInput`, `LadderClimbIkOutput`, `LadderClimbIkTelemetryRing`, and `LadderClimbIkTelemetryCursor`.
- Removed the private H8Memory fallback; no DataVault now means climb start fails closed.
- Converted ladder input/output/telemetry structs to `[StructLayout(LayoutKind.Sequential, Pack = 1)]`; converted touched `HapticRequest` and `PlayerStateSignal` explicit lanes to `Pack = 1` without changing fixed sizes.
- Added low-tier midpoint-plus-pole elbow fake while preserving exact rung hand targets; high tier still uses clamped `math.acos`.
- Replaced remaining ladder-domain blind divisions with `math.rcp(math.max(...))`, clamped grip accumulation, guarded `rsqrt`, and sanitized presentation deltas.

Cinematic cheats used:
- Toaster mode: camera slide plus midpoint elbow fake, no `acos` elbow solve.
- High/VR mode: exact two-bone hand lock remains, driven by grip hand deltas.
- No shader/compute/VFX ownership was invented from the animation domain; ladder publishes typed state/haptics for existing visual owners to consume.

Exact microseconds saved:
- Low-tier elbow fake versus full two-arm `acos` solve: estimated 7 us saved per player solve.
- Removal of private fallback allocation/mirror: 0 us hot path, lower persistent memory ownership risk.
- No per-frame disk IO: 0 us Steam Deck/MicroSD hot-path cost; blackbox dump remains cold path only.

Validation:
- Static ladder-domain scan found no private NativeArray fields, `H8Memory.Allocate`, `new NativeArray`, `Allocator.Persistent`, `StartCoroutine`, runtime `Update`, `FixedUpdate`, naked `Debug.Log`, `Animator`, `TeleportPlayer`, `PerformTeleport`, or `player.position =`.
- Static shader/compute scan found no ladder-domain `ComputeShader`, shader dispatch, material mutation, or thread-group code.
- `dotnet restore Hecton8.Core.csproj` and `dotnet restore Assembly-CSharp.csproj` succeeded.
- `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` fails on unrelated missing `TetherFiredSignal` and `Hecton8.AI.Sensory.AcousticEchoHuntResult` contract includes.
- `dotnet build Assembly-CSharp.csproj --no-restore -nodeReuse:false -v:q` fails on missing RealtimeCSG source files plus `TetherFiredSignal`.
- Targeted Core build error scan produced no `LadderClimb`, `ProceduralLadder`, or `ClimbableLadder` matches. Status remains PENDING VERIFICATION.
