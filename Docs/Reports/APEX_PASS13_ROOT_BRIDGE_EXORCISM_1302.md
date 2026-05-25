# APEX PASS 13 - ROOT BRIDGE EXORCISM 1302

## Scope
- Prompt: `Docs/Reports/PROMPT_1302_REEXTRACTED_PASS13.txt`
- Task count: 20
- Source domain: `Assets/_Project/Scripts/Physics`, excluding Tether/Cable ownership, plus root `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs` because Pass 12 moved the culling writer there.
- Build: not launched. CPU LoadPercentage probe was 59 (>50), and user explicitly ordered rare dotnet/build usage.

## Source Patch
- `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:540-542` adds fixed uint reason hashes for physics culling faults.
- `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:2614`, `:2919`, `:2933` now call `DumpPhysicsCullingBlackBox(uint reasonHash, float scalarValue)` instead of string reason paths.
- `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:3300-3306` now fail-closes on `reasonHash == 0` or `GlobalTelemetryBus.BlackboxActiveFrameCount <= 0`, sanitizes scalar NaN to `0f`, and emits `GlobalTelemetryBus.PushEvent` only.
- Removed local `System.IO`, `Path`, `Directory`, `FileStream`, `BinaryWriter`, string reason write, `catch (Exception)`, and string concatenation from the physics-culling dump route in root manager.

## Byte Offset Map - Culling DTOs
Full machine-readable map: `Docs/Reports/DTO_OFFSET_MAP_1302_PASS13_CULLING_TARGETS.json`.

- `PhysicsImpactEventData` `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:493`, size 112, violations 0.
  - `0 AbsoluteUniversePosition PointAup`; `48 ulong PrimaryBodyId`; `56 ulong SecondaryBodyId`; `64 float3 Point`; `76 float3 Normal`; `88 float Force`; `92 float Intensity`; `96 float MassVelocity`; `100 PhysicsImpactWeightClass WeightClass`; `101 byte PrimaryAudioMaterialId`; `102 byte SecondaryAudioMaterialId`; `103-111 private byte padding`.
- `PhysicsCullingTelemetryEntry` `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:518`, size 32, violations 0.
  - `0 int FrameIndex`; `4 int TrackedBodyCount`; `8 int CulledBodyCount`; `12 uint BodyId`; `16 float DistanceSq`; `20 private uint padding`; `24 ushort CcdInterventions`; `26 byte Command`; `27 byte AwakeResult`; `28 byte Flags`; `29 byte Reserved`; `30-31 private byte padding`.
- `PhysicsCullingDTO` `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:18`, size 40, violations 0.
  - `0 double3 AUP`; `24 int InstanceId`; `28 float ActivationRadiusSq`; `32 uint CullingFlags`; `36 byte IsAsleep`; `37-39 private byte padding`.
- `FrozenVelocityDTO` `...Shinobu37PhysicsCulling.cs:31`, size 32, violations 0.
  - `0 float3 LinearVelocity`; `12 float3 AngularVelocity`; `24 private uint padding`; `28 byte HasVelocity`; `29-31 private byte padding`.
- `PhysicsCullingTargetWakeRequestSignal` `...Shinobu37PhysicsCulling.cs:43`, size 16, violations 0.
  - `0 float3 ImpulseVector`; `12 uint TargetInstanceId`.
- `MockSeismicShockwaveSignal` `...Shinobu37PhysicsCulling.cs:50`, size 48, violations 0.
  - `0 double3 EpicenterAup`; `24 float RadiusMeters`; `28 uint Seed`; `32 uint Frame`; `36 byte Fire`; `37-47 private byte padding`.
- `PhysicsCullingFrameTelemetry` `...Shinobu37PhysicsCulling.cs:71`, size 32, violations 0.
- `PhysicsCullingCounter64` `...Shinobu37PhysicsCulling.cs:84`, size 64, violations 0.
- `PhysicsCullingTuningDTO` `...Shinobu37PhysicsCulling.cs:98`, size 32, violations 0.
- `PhysicsCullingDebugBody` `...Shinobu37PhysicsCulling.cs:111`, size 40, violations 0.

## Static Scan Results
- `Docs/Reports/PASS13_PLAYER_STATIC_SCAN_1302.json`: 41 changed `.cs`, 36 in-scope after Tether/Cable exclusion, root bridge forbidden player hits 0, player managed-risk hits 0, added forbidden token hits 0.
- Same scan reports 16 full-file player managed allocation `new` hits and 700 raw textual `new` hits. These are pre-existing cold field storage/value-type construction in changed files, not Pass 13 additions. They are not reported as erased.
- `Docs/Reports/AUP_CAST_SCAN_1302_PASS13.json`: 27 AUP-context float-cast candidates, 0 possible absolute AUP direct-float violations. Formula remains double delta first, e.g. `GlobalPhysicsStateManager.cs:2768` uses `AbsoluteUniversePosition.DeltaMetersClamped(in bodyAup, in cameraAup)` before float consumers.
- `Docs/Reports/DEPENDENCY_USING_AUDIT_1302_PASS13.json`: forbidden using hits 0, modified asmdefs 0.
- `Docs/Reports/OVERENGINEERING_FAILCLOSED_SCAN_1302_PASS13.json`: 3 schedule hits reviewed. They are existing variable-count batch schedules (`candidateCount`, `count`, `jobCount`) with no same-frame `.Complete()` hit in the culling scan.

## Residuals
- Core `GlobalTelemetryBus` still owns managed disk IO in Core diagnostics. 1302 removed the local physics-culling `FileStream`/`BinaryWriter`; it did not fake a native Core dump bridge.
- Full changed-file player source is not literal zero-managed-source because of 16 pre-existing cold managed field allocations in root/listener scratch storage.
