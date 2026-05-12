# LOG_VEHICLE_DRONE_FLEET

## 2026-05-11 - Headless Drone Fleet Pass

What was wrong:
- Fleet slot cap was 8, below the 50-drone batch requirement.
- Explicit SoA streams for `NativeArray<float3>` drone positions and `NativeArray<byte>` drone states were missing.
- Repair work was applied from the late-frame scan instead of a Burst-emitted command queue.
- Weld visuals had no `DebrisSpawnSignal` spark event.
- GPU culling handled frustum only; no 50m Low / 150m High render-distance LOD.
- Mining laser and ore transport have no safe drone task contract in current construction code.
- Full compile is blocked outside this domain by `HectonSurvivalSystem.cs(298,29): SurvivalPhysiologyScalarResult` missing.

What was done:
- Raised `HeadlessDroneCapacity` to 64.
- Added persistent SoA mirrors: `s_DronePositionsSoA` and `s_DroneStateBytes`.
- Added `DroneServiceCommand` and prewarmed `NativeQueue<DroneServiceCommand>` drained by the drone fleet owner thread.
- Added repair spark debris signal from drone weld AUP.
- Added compute shader distance culling via `_CameraPositionWS` and `_DroneRenderDistanceSq`.
- Added 300-frame fixed native black-box ring and NaN-triggered dump to `Docs/AgentLogs/Dump_VEHICLE_DRONE_FLEET.bin`.
- Wrote recon report to `Docs/AgentLogs/RECON_VEHICLE_DRONE_FLEET.md`.

Cinematic cheats used:
- Kinematic fake movement, no NavMeshAgent and no Rigidbody.
- Spatial-hash repulsion using squared distances, no physics colliders.
- `math.rsqrt`/`math.rcp` instead of sqrt/division on hot paths.
- Compute append-buffer compaction for frustum/distance culling.
- Event-driven spark debris instead of per-drone ParticleSystem.
- Render-only LOD: Low/MX350 culls drones past 50m; High/Ultra culls past 150m while logic continues.

Exact microseconds saved:
- Verified by profiler: 0 us. Profiler/runtime proof blocked by external compile failure.
- Engineering estimate, pending verification: 100-500 us saved at 50 drones by avoiding managed per-drone update/render components.
- Engineering estimate, pending verification: 80-400 us saved at 50 drones by avoiding collider avoidance.
- Engineering estimate, pending verification: 30-150 us presentation cost saved in far-drone scenes by compute distance culling.

Verification:
- `mcp validate_script Assets/_Project/Scripts/Construction/DroneCognitionJob.cs`: 0 diagnostics.
- Earlier `mcp validate_script` pass for `DroneFleetManager.cs`: 0 diagnostics before final black-box additions; later validation attempts disconnected.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: fails only on external survival dependency at final run.

Burst job evidence:
- Kinematic movement: `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs:263` and `:353-366` resolve destination, rsqrt-normalize direction, blend velocity, and write `drone.Position += drone.Velocity * DeltaTime`.
- Anti-collision fake: `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs:520` spatial-hash neighbor scan applies squared-distance repulsion with `math.rcp`.
- Service queue: `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs:390` enqueues `DroneServiceCommand`; `DroneFleetManager.cs:1491` drains it on the owner thread.
- Compute culling: `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2895` dispatches culling and `DroneCulling.compute` rejects invisible/distant drones before indirect count copy.

Status:
- PENDING VERIFICATION.

## 2026-05-12 - Honest R&D Phantom Swarm Scalability Pass

What was wrong:
- Mining drones still do not have a real drone-domain contract. `ResourceNode` accepts mining interaction signals, and `AutonomousExtractorSystem` owns fixed extractor production, but there is no `DroneFleetTaskKind.Mining`, ore carry bit, or storage return handoff.
- The phantom swarm visual cheat was fixed at 500 GPU instances for every tier. That is acceptable visual overkill on Ultra, but wasteful on i3/MX350.

What was done:
- Left mining laser and ore transport blocked instead of faking completion.
- Added tier-resolved phantom draw counts: Unknown/Low/MX350=0, Mid=192, High=384, Ultra=500.
- Changed phantom indirect args to update only when the draw count changes.
- Kept the Ultra buffer capacity at 500 so high-end visuals do not allocate or resize during play.

Cinematic cheats used:
- GPU-authored phantom drones remain presentation-only; real drone logic is unchanged.
- Low tier deletes the decorative swarm entirely rather than simulating or rendering a weaker fake.
- High/Ultra spend saved CPU/GPU budget on dense indirect visual noise.

Exact microseconds saved:
- Verified by profiler: 0 us. Profiler proof is still blocked by external project compile errors.
- Engineering estimate, pending verification: 30-250 us saved on Low/MX350 by skipping the 500-instance phantom compute dispatch and indirect draw.

Verification:
- `mcp validate_script Assets/_Project/Scripts/Construction/DroneFleetManager.cs`: 0 diagnostics.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: still fails outside drone domain. Current reported blockers include missing `HectonPersistentPathPolicy`, `HectonThreadPriorityPolicy`, `HectonNativeBridge`, `SteamDeckInputPal`, and other non-drone symbols.

Status:
- PENDING VERIFICATION.
