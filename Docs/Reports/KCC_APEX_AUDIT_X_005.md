# KCC APEX Audit X_005

## Scoped PhysX Result
- X_005 scoped forbidden call count: 0
- Whole non-Editor runtime forbidden call count: 0
- Whole non-Editor sync Physics cast count: 0
- Broad residual split: {}
- Whole-runtime residuals outside X_005 are listed in JSON; they are not claimed clean by this agent.

## Rigidbody Authority Result
- External non-Editor Rigidbody velocity assignment count: 0
- External non-Editor Rigidbody force call count: 0
- External player/rider Rigidbody pose fallback count: 0
- Remaining velocity writes are central `PhysicsApplySystem` packet application or DTO/state assignments listed in JSON.

## Legacy Player Sweep Bridge Result
- Player motor capsule sweep bridge removed: True
- Player motor capsule sweep bridge symbol count: 0
- Repair-target bridge disabled: True
- Player motor native state removed: True
- Player motor native state symbol count: 0
- Player motor `RaycastHit` symbol count: 0
- Player hand probe uses explicit KCC probe DTO: True
- Player hand probe `RaycastHit` lane count: 0
- Player kinematics runtime `RaycastHit` symbol count: 0
- Player kinematics sync contract `RaycastHit` symbol count: 0
- Player kinematics sync contract uses vector ladder contact: True
- Player movement surface `RaycastHit` symbol count: 0
- Player movement legacy collision symbol count: 0
- Player movement Unity collision DTO count: 0
- Player movement legacy collision route removed: True
- Player movement raycast-named surface symbol count: 0
- Player movement surface language is typed: True
- Player motor repair PhysX wording count: 0
- Player motor repair language is typed: True
- Player kinematics default Physics layer count: 0
- Player kinematics uses strict interaction probe mask: True
- Player footstep audio `RaycastHit` symbol count: 0
- Player movement surface uses explicit hit DTO: True
- Player footstep audio uses surface hit DTO: True
- Player spawner `RaycastHit` symbol count: 0
- Player spawner `TryRaycastGround` symbol count: 0
- Player spawner uses spawn ground DTO: True
- Player motor `RaycastHit` native allocations: 0
- Player motor PhysX command native allocations: 0
- Legacy batch helper `QueryResult[]` mirrors: 0
- Legacy batch helper Unity Physics calls: 0
- Vehicle motor capsule sweep bridge removed: True
- Vehicle motor capsule sweep bridge symbol count: 0
- Vehicle motor `RaycastHit` symbol count: 0

## Player Split Authority Velocity Reads
- Player noise/action Rigidbody velocity read count: 0
- Player Rigidbody motion/mass/pose state read count: 0
- Player-body alias motion/mass/pose state read count: 0
- Player noise uses KCC velocity signal: True
- Player action interrupt uses KCC velocity signal: True
- Player swim has no Rigidbody velocity fallback: True
- Survival movement/save velocity uses KCC signal: True
- Player spawner teleport velocity uses KCC signal: True
- Player tool recoil uses deterministic equivalent mass: True
- Player inventory impact uses deterministic equivalent mass: True
- Camera juice player speed uses KCC velocity signal: True
- Airlock docking snap start uses Transform pose, not Rigidbody pose: True
- Airlock Hydro teleport/snap routes through player motor: True
- Save-load Hydro teleport routes through player motor: True
- Spawner Hydro teleport routes through player motor: True
- Maelstrom player damage position uses player pose snapshot: True

## Player Trigger Callback Authority
- Player trigger callback count: 0
- Sargassum uses dispatcher polling: True
- Sargassum cut response uses KCC velocity signal: True
- Sargassum has no Rigidbody velocity read: True
- Environmental hazard uses slow-tick cached trigger volume: True
- Toxin hazard uses slow-tick cached trigger volume: True
- Oxygen bubble uses runtime-position polling: True
- Base module uses runtime occupancy polling: True
- Base module hot occupancy uses cached player only: True
- Acoustic reverb uses runtime volume polling: True
- Acoustic reverb TryResolve uses cached player only: True
- Demo door uses runtime volume polling: True
- Demo door TryResolve uses cached player only: True
- Sargassum hot-swap disables registry fallback: True
- Transport charging uses lifecycle-registry volume polling: True
- Vehicle docking uses lifecycle-registry volume polling: True
- Vehicle docking has no legacy collider resolver: True
- Transport registry TryGetAt is pure read: True
- Manta scooter registers on pool spawn: True

## Owner Internal Authority Reads
- Player movement `_rb.linearVelocity` read count: 0
- Player movement hot `_rb.mass` read count: 0
- Player movement uses cached authoritative body mass: True
- Player movement velocity reads centralized: True
- Player movement has no Rigidbody velocity read: True
- Player movement has no hot Rigidbody mass read: True
- Player movement hot Rigidbody pose read count: 0
- Player movement has no hot Rigidbody pose read: True
- Player movement body position is snapshot-first: True
- Player movement uses KCC velocity signal: True
- Player motor `_body.linearVelocity` read count: 0
- Player motor has no body velocity read: True
- Player kinematics `_body.linearVelocity` read count: 0
- Player kinematics has no body velocity read: True
- Player kinematics hot Rigidbody pose read count: 0
- Player kinematics has no hot Rigidbody pose read: True
- Player kinematics body position is snapshot-first: True
- Motor Hydro force uses KCC velocity: True
- Motor Hydro force uses authority mass: True
- Motor Hydro impulse uses authority mass: True
- Motor Hydro torque suppressed: True
- Motor Hydro off-center force demotes to linear KCC force: True
- Motor has no scheduled sweep bridge symbols: True
- Motor runtime position uses cached player context: True

## Player Force Route Authority
- Direct player-body force/angular route sites: 4
- Ungated player-body force/angular route sites: 0
- Central force router routes player force before Rigidbody kinematic rejection: True
- Central force router routes player point force before Rigidbody kinematic rejection: True
- Central force router suppresses player torque/angular velocity shell mutation: True
- Central force router uses KCC/movement velocity for player velocity set: True
- Central force router uses deterministic player equivalent mass: True
- Tool player impulse uses deterministic equivalent mass: True
- Fauna light target velocity uses KCC signal: True
- Fauna predator bite routes through player force sink: True
- Scooter shafts velocity fallback uses KCC signal: True
- Submarine thermal updraft no longer queues duplicate player Rigidbody force: True
- Survival angular reset is Hydro-gated: True
- Spawner angular reset is Hydro-gated: True
- Save-load angular reset is Hydro-gated: True

## Solver Bound
- ResolveIterationCount max: 8
- Hard local stride clamps 1..8 found: 3
- Capsule axis probe manifold: True
- Contact plane deduplication: True
- Max SDF axis probes per entity: 24 (8 sweep samples * 3 capsule probes)
- Max stored contact planes per entity: 8
- Max resolution plane projections per entity: 64 (8 contact planes * 8 bounded passes)
- 100 m/s at dt 0.016666667 moves 1.666667 m/frame.
- 100 m/s cone fall contract test present: True
- No recursion is used by the KCC collision build or resolution jobs; bounded for-loops terminate after fixed counters.
- Three-plane corner proof: At most 8 unique contact planes are collected and at most 8 Gauss-Seidel passes are executed. A 3-plane corner consumes no recursion and no stack growth: each bounded projection computes v' = v - n * min(dot(v,n),0), then the next fixed-index contact is evaluated. Nearly duplicate same-direction planes above the dot threshold are discarded before they spend projection budget; opposing corridor walls remain independent constraints. The two loop counters are monotonic and capped, so degenerate coplanar/orthogonal contacts terminate after <=64 projections even when velocity becomes zero.
- 100 m/s cone proof: At 60 Hz, 100 m/s is 1.6666667 m per frame. The speculative SDF stage keeps an 8-slot stored contact stride, evaluates up to 24 capsule-axis SDF probes (8 sweep steps * bottom/mid/top), and the headless smoke geometry includes a central voxel cone with profile index 1 falling at exactly -100 m/s. An editor contract now asserts the smoke runner tuning limit is >=100 m/s, so this proof cannot silently degrade by speed clamp. Cone/corner degeneracy can still lose sub-voxel collider fidelity if the SDF cell is coarser than the cone tip radius; the failure mode is bounded conservative stop/slide, not an unbounded loop.

## Lockstep Layout Gate
- Runtime validator checks 64-byte size: True
- Runtime validator checks storage offsets: True
- Rollback edit test uses 64-byte size: True
- Rollback edit test rejects old 96-byte layout: True
- Rollback edit test checks storage field offsets: True
- Rollback edit test has no compatibility-property offsets: True

## Black Box Result
- Telemetry ring capacity is 300: True
- Agent dump file present: True
- Dumps on new fault mask: True
- Fault latch resets after clean frame: True
- Late-frame fault scan requires full entity capacity: True
- Telemetry aggregate requires valid state lane: True
- Telemetry iteration count can record exact zero: True

## LockstepPlayerKinematicState Layout
- Size: 64 bytes
- Covered bytes: 64
- Gaps: []

- 00..24: double3 PositionAup
- 24..36: float3 Velocity
- 36..48: float3 InputVector
- 48..52: uint Frame
- 52..56: uint Flags
- 56..60: uint InputActions
- 60..61: byte _pad0
- 61..62: byte _pad1
- 62..63: byte _pad2
- 63..64: byte _pad3

## KinematicStateDTO Layout
- Size: 64 bytes
- Covered bytes: 64
- Gaps: []

- 00..24: double3 AUP_Position
- 24..36: float3 Velocity
- 36..48: float3 AngularVelocity
- 48..52: float Mass
- 52..56: uint Flags
- 56..60: float DragCoefficient
- 60..61: byte RestingFrameCount
- 61..62: byte DeepSleepTickCount
- 62..63: byte SleepMaterialIndex
- 63..64: byte _pad0

## PlayerKinematicsProbeHit Layout
- Size: 64 bytes
- Covered bytes: 64
- Gaps: []

- 00..12: float3 Point
- 12..24: float3 Normal
- 24..28: float Distance
- 28..32: uint Flags
- 32..36: int ColliderInstanceId
- 36..40: int MaterialId
- 40..52: float3 ReservedVector
- 52..56: uint Frame
- 56..64: ulong RouteHash
