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

## 2026-05-16 - Loop 7 Stricter Prompt Compliance
What was wrong:
- `CURRENT_BATCH.md` contains a stricter duplicate `LADDER_CLIMB_IK` prompt that was not fully reflected in status: PC camera slide had to be explicit ladder-vector interpolation, STP stabilization had to use FastNlerp head smoothing, climbing fast had to drive stress/O2 pressure, and slip had to include look-down grip release.
- DataVault resolution still had a lazy registry read path reachable from tick-called helpers.
- `PhysiologyStateSignal` and `PlayerStressSignal` publish paths used legacy queue fields directly instead of explicit sanitized `SignalBus<T>.Push`.

What was done:
- Added cold-only `CacheVaultDependency()` and removed `GlobalRegistry.DataVault` polling from `EnsureVaultBuffers()`.
- Added low-tier absolute camera slide using `Vector3.Lerp(entry, exit, progress01)` and non-VR head stabilization using `CinematicMath.FastNlerp`.
- Added climb-speed stress publishing through existing `PhysiologyStateSignal` and `PlayerStressSignal` with `Cause = PlayerStateSignal.StateClimbing` and O2 multiplier.
- Added VR look-down grip-release slip using cached `IPlayerRuntimeContext` and a dot-product threshold against ladder-down.
- Converted `PhysiologyStateSignal` and `PlayerStressSignal` to explicit `Pack = 1` and routed their publish methods through sanitized typed `SignalBus<T>.Push`.
- Removed the remaining misleading `Update()`/teleport comments from the touched ladder adapter header.

Cinematic cheats used:
- Toaster mode: absolute camera interpolation plus one FastNlerp, no extra physics, raycast, or Animator state.
- Slip detection: dot-product gaze fake instead of a camera physics query.
- High/VR: grip pull remains physical; HMD rotation is not forced.

Exact microseconds saved:
- Cold-only DataVault dependency cache: 0 to 1 us avoided per helper call versus repeated registry property reads.
- Dot-product look-down slip versus raycast/camera search: estimated 5 us saved per VR tick and 0 GC.
- Low-tier absolute slide avoids cumulative correction drift with effectively 0 additional hot cost; FastNlerp cost estimated 2 us/frame.
- Reusing existing physiology/stress lanes avoids a new signal lane and duplicate consumers; estimated 3 us/event avoided versus new lane fan-out.

Validation:
- Fixed self-owned compile error from the first Loop 7 build attempt (`SanitizeFinite` overload).
- Latest `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` reports no `LadderClimb`, `ProceduralLadder`, `ClimbableLadder`, `PhysiologyStateSignal`, or `PlayerStressSignal` errors.
- Build remains blocked by unrelated `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs(1166,18): EnsureCoreCognitionVaultBuffers` missing.
- Static scan found no ladder-domain `private NativeArray`, `H8Memory.Allocate`, `new NativeArray`, `Allocator.Persistent`, `StartCoroutine`, runtime `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, `Debug.Log`, `EventBus`, `Animator`, `TeleportPlayer`, `PerformTeleport`, `player.position =`, or `Player.transform.position +=`.
- Pack-layout scan found no non-pack-1 `StructLayout` in `Assets/_Project/Scripts/Animation/Locomotion`.
- Status remains PENDING VERIFICATION because Unity/Profiler/GCMonitor evidence is absent and Core build is blocked outside this domain.
## Loop 8 Registry Hygiene and Delegate Purge

What was wrong:
- `ProceduralLadderClimbRuntime` still created a hidden persistent root with `DontDestroyOnLoad` and self-registered through `Awake()`.
- `ClimbableLadder` still exposed `UnityEvent` climb hooks and invoked `OnClimbStart`, creating a duplicate managed delegate path beside the typed climb signal lanes.

What was done:
- Removed `DontDestroyOnLoad`; the generated ladder runtime is now scene-local.
- Deleted `Awake()` self-registration and moved registry ownership to `OnEnable`/`OnDisable`.
- Added a cold-order justification comment for the runtime `DefaultExecutionOrder`.
- Removed ladder adapter UnityEvent fields/import/invocation plus obsolete transition/player-tag serialized fields.
- Re-ran static scans for teleport markers, DDOL, UnityEvent, string.Format, Debug.Log, Animator, private/native allocations, EventBus, and H8Memory allocation in the ladder-owned path.

Cinematic cheats used:
- No new simulation. The low-tier Dear Lie remains midpoint elbow placement plus absolute camera lerp; high tier keeps the exact `math.acos` two-bone solve.
- Visual overkill remains delegated to typed-lane consumers; ladder IK owns embodiment, not visor salt/silt/hull shaders.

Exact microseconds saved:
- Runtime steady-state: 0 us. This pass removes lifecycle and delegate risk, not per-frame math.
- Interaction start: estimated 0-2 us saved by deleting `UnityEvent` invocation; profiler proof absent.
- Build validation: `dotnet restore Hecton8.Core.csproj` succeeded. Latest Core build fails on unrelated `RepairTool.cs(1036,52): CS0165` and `World/SargassumMicroFaunaBoids.cs` CS0103 vault/native-field errors; no ladder symbols reported. Assembly restore/build attempt timed out after 306 seconds.

## Loop 9 Teleport API Name Purge

What was wrong:
- `ClimbableLadder` still exposed public `TeleportToExit` and `TeleportToEntry` methods even though the implementation had become procedural.
- That preserved the old teleport contract in the source surface and kept a false-positive debt marker in the ladder-owned path.

What was done:
- Replaced `TeleportToExit` with `RequestClimbToExit`.
- Replaced `TeleportToEntry` with `RequestClimbToEntry`.
- Confirmed there are no live source references to the old method names outside a deprecated external description bundle.
- Re-ran source scans for teleport, UnityEvent, DDOL, string formatting, Debug logging, coroutine, Animator, EventBus, private/native allocations, and H8Memory allocation markers in the ladder-owned path.

Cinematic cheats used:
- No new simulation. This pass only removes an API lie; low-tier still uses the Dear Lie midpoint elbow and camera slide, high tier keeps exact rung hand lock and `math.acos`.

Exact microseconds saved:
- Runtime: 0 us. Method rename does not change the execution path.
- Maintenance/debug savings only: prevents future agents from binding against a teleport-named climb API.
- Build validation: latest Core build fails on unrelated `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs(1,18): CS0234` missing `Hecton8.AI.Ecosystem`; no ladder symbols reported.
