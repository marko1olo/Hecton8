# NAVGRID LEAK PURGE SURGERY LOG

Date: 2026-05-07
Status: PENDING VERIFICATION

## Mandates Followed

- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `AI_DYNAMIC_NAVGRID_SDF_INTEGRATION.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `STRM_Persistent_Object_Registry.txt`

## NavGrid Native Disposal

Target: `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` and `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntimeLifecycle.cs`

Finding: `VolumeRecord.Dispose()` released persistent `NativeArray<byte>` and `NativeArray<ushort>` without a lifecycle owner and without gating disposal behind the pending dynamic obstacle `JobHandle`.

Surgery:

- Added `VoxelDynamicNavGridRuntimeLifecycle` with `OnDisable()` and `OnDestroy()` teardown hooks in a matching Unity script file.
- Added static lifecycle owner creation on first runtime initialization.
- Replaced direct record disposal with `VolumeRecord.TryDisposeCompleted()`.
- `TryDisposeCompleted()` returns `false` when `HasPendingDynamicUpdate` is true and `PendingDynamicUpdateHandle.IsCompleted` is false.
- `UnregisterVolume()` now marks records as pending removal and removes them only when disposal is safe.
- `DisposeAll()` marks all records for teardown, defers queue/container disposal if any job is still incomplete, and clears containers after the pending job completes.
- `TryPrepareBuild()` now drains completed dynamic obstacle jobs before resizing persistent native buffers.

Hot-path impact: no new per-frame managed allocation. New scratch state is static cold allocation: `List<int>(16)`.

Failure modes: if Unity destroys the lifecycle owner while a dynamic nav job is still incomplete, native arrays are intentionally retained until a later completed teardown pass. This avoids unsafe disposal but requires Unity runtime readback to prove no retained pending job remains at shutdown.

## LayerMask Serialization

Target: `Assets/_Project/Scripts/Scavenging/ResourceNodeTemplate.cs` and `Assets/_Project/Data/Scavenging/ResourceNodes/*.asset`

Findings:

- `ResourceNodeTemplate` already uses explicit `DefaultValidLayerMask = (1 << 8) | (1 << 9) | (1 << 10)`.
- `ResourceNodeTemplate.OnValidate()` already sanitizes negative masks through `SanitizeValidLayerMask()`.
- Static scan found no serialized `m_Bits: -1` under `Assets`.
- Existing resource-node assets serialize explicit masks such as `m_Bits: 1792`.

Editor repair attempt: MCP `execute_code` was attempted to scan ScriptableObject `.asset` files and rewrite `m_Bits: -1` to `m_Bits: 4294967295`; Unity session was unavailable, so no editor-executed repair result exists.

## Contact Pair GC

Target: `PhysicsApplySystem.cs` and collision handlers.

Findings:

- `PhysicsApplySystem` already uses `UnityEngine.Physics.ContactModifyEvent`, `ContactModifyEventCCD`, and `NativeArray<ModifiableContactPair>`.
- Removed the direct `collision.contacts` array access in `FloraProjectile`; it now uses `collision.contactCount` + `collision.GetContact(0)`.
- Static scan found no remaining `.contacts` or `GetContacts()` calls.

Remaining managed collision callbacks requiring a separate architecture migration:

- `GlobalPhysicsStateManager.PhysicsStateReporter.OnCollisionEnter`
- `HectonPlayerMovement.OnCollisionEnter`
- `Gameplay.MantaEmergencyWreck.OnCollisionEnter`
- `Gameplay.FloraProjectile.OnCollisionEnter`
- `World.SargassumCollapseChunk.OnCollisionEnter`

Verdict: full "ModifiableContactPair-only" compliance is NOT complete. Current patch removes the known contact-array allocation but does not rewrite all gameplay collision ownership into the contact-modification pipeline.

## Obsolete Warning Sweep

Findings:

- Static scan found no `#pragma warning disable CS0618` in `Assets/_Project/Scripts`.
- Updated `FindObjectsByType` call sites to pass `FindObjectsSortMode.None`.
- Static regex scan found no remaining `FindObjectsByType` invocation without `FindObjectsSortMode`.

## Delegate Caching

Targets: `SystemDispatcher.cs`, event buses.

Findings:

- `SystemDispatcher` hot paths do not create anonymous delegates or lambdas.
- Event bus scan found static event invocations and expression-bodied accessors, not hot-path anonymous delegate allocation.
- `PhysicsApplySystem` subscribes contact callbacks by method group in `OnEnable` and unsubscribes in `OnDisable`.

## Verification State

Static checks completed:

- No serialized `m_Bits: -1` under `Assets`.
- No `.contacts` or `GetContacts()` calls under `Assets/_Project/Scripts`.
- No `#pragma warning disable CS0618` under `Assets/_Project/Scripts`.
- No `FindObjectsByType` call block without `FindObjectsSortMode`.

Unity checks:

- `refresh_unity` timed out after 60 seconds waiting for editor readiness.
- `read_console` failed: `no_unity_session`.
- `execute_code` layer-mask repair failed: `no_unity_session`.
- `dotnet build Assembly-CSharp.csproj` is not authoritative for this Unity project and failed on missing Unity-generated `Temp/bin/Debug/*.dll` metadata references before project code could be validated.

STATUS: PENDING VERIFICATION - NOT MCP VERIFIED.

Diff artifact: `Docs/Reports/NAVGRID_LEAK_PURGE_DIFF.patch`
