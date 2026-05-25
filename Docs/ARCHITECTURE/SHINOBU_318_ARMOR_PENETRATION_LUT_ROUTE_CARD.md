PROMPT IDENTIFIED: SHINOBU_318
DOMAIN: Echelon 5 Combat & Survival Physiology / Armor Penetration LUT

# Armor Penetration LUT Route Card

Owner: `CombatDamageRuntime` / `SystemID.GameplayCombat`.

Hot route:
`CombatDamageSignal` -> `CombatDamageRuntime.TryQueueDamage` -> preserved `ImpactAup` Vault lane -> `ProcessDamageQueueJob` -> `ArmorProfileDTO.ArmorGridLUT[8x6]` -> existing native health CAS -> `CombatDamageResult`.

- Registered tool-hit route: `ToolHitUtility.TryQueueCentralDamage` now passes finite hit-point AUP through the public `CombatDamageRuntime.TryQueueDamage(..., double3 impactAup)` ingress.
- It resolves player-pose AUP first and falls back to `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(hitPoint)` for finite runtime hits.
- It does not derive weakspot/limb metadata from hit collider, use `Transform.InverseTransformPoint` for registered targets, or queue `double3.zero` as plausible impact AUP.

Vault lanes:
- `73580` `double3[1024]` signal impact AUPs.
- `73581` `double3[2048]` target root AUPs.
- `73582` `quaternion[2048]` target rotations.
- `73583` `float3[2048]` target half extents.
- `73584` `ArmorProfileDTO[2048]`.
- `73585` `ArmorPenetrationTelemetryEntry[300]`.
- `73586` `ArmorPenetrationDebugHitDTO[1024]`.
- `73587` `ArmorPenetrationTuningDTO[1]`.
- `73588..73590` cold editor/QA mock request/detail/AUP buffers.

Resolver discipline:

- Runtime `ensure:true` resolves only core armor lanes `73580..73587`.
- Cold QA mock lanes `73588..73590` require explicit `includeMock:true`.
- During `IDataVault.IsAllocationLocked`, SHINOBU_318 may recover existing owner-local descriptors.
- It cannot call allocation-capable `EnsureGenerationHandle`.

- Hot-path discipline: damage enqueue and frame scheduling use `ensure:false`.
- If core armor Vault lanes are missing, route fails closed or falls back to local hit detail.
- No allocation/growth in damage path.
- Core allocation is confined to cold init/register/editor/parser routes.
- Runtime job, mock job, tuning, and CSV writer locks are released through `finally`.

DataVault recovery:
`EnsureArmorPenetrationNativeState` registers a cold `GlobalRegistry` hot-swap bridge.

- DataVault replacement releases old SHINOBU-owned generation descriptors.
- It reopens core armor lanes from the registry event.
- If a damage job owns the lanes, rebind waits for owner completion.
- `FrameTick` does not poll `GlobalRegistry`.

Metadata safety:

- `ProcessDamageQueueJob` bounds-checks packed detail index before `SignalDetails`.
- It clamps packed damage/armor classes before 8x8 LUT index.
- Corrupted ingress fails closed into dropped-result telemetry, not native array overread.

Pointer aliasing:
Production `ProcessDamageQueueJob` and cold `GenerateMockArmorImpactSignalsJob` annotate non-overlapping NativeArray fields with `[NoAlias]`. The mock generator has six disjoint lanes: `InstanceIds`, `TargetRootAups`, `TargetHalfExtents`, `Requests`, `Details`, and `ImpactAups`.

Collision note:
The earlier local candidate `71620..71630` is rejected. Static scan found it already owned by SHINOBU_158 buoyancy/SIMD lanes.

Feedback route:
Armor deflects use existing unmanaged `DeflectSignal`; material/AUP impact feedback uses existing `ImpactSignal`. `VisualFlareSignal` is not used because it has no AUP or material payload.

- Forbidden route: No armor `Physics.Raycast`, no body-part `MeshCollider`, no child-collider weakspot/limb metadata, no registered-route `Transform.InverseTransformPoint(hitPoint)` armor coordinate path, no managed `SendMessage` damage lane.
- Fauna prefab sanitation remains editor-only through `FaunaColliderValidator` and `OOP_Hitbox_Scanner`.
- Residual `RaycastCommand` hits in the Combat/Fauna scan are deferred sensory/leg-IK systems, not armor penetration.
- One total `ToolHitUtility` `InverseTransformPoint(hitPoint)` remains in the unregistered legacy `IDamageReceiver` fallback and is outside the registered armor authority route.

Scalability:
`GlobalQualityWeight` is continuous. Low quality biases cheap base armor; higher quality trusts the 8x6 LUT and raises VFX without changing authority/layout/save/rollback truth.

Black box:

`ArmorPenetrationTelemetryEntry[300]` records impact count, weak hits, deflect count, average mitigated damage, quality, hashes, and solve time.

NaN/over-budget completion sets dump-request flag. Editor/development writes `Docs/AgentLogs/Dump_SHINOBU_318.bin`; release runtime avoids managed disk I/O on fault branch.
