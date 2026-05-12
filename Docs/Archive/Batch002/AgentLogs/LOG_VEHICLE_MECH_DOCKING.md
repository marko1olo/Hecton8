# VEHICLE_MECH_DOCKING Log

## Session 2026-05-11

What was wrong: Batch prompt reports Seaglide/Submarine docking uses or risks Unity joint-style locking and origin-shift tearing.
What was done: Prompt extracted, domain confirmed, mandates loaded, status/rationale/log files initialized.
Cinematic Cheats used: Selected kinematic snap/S-curve presentation instead of physical joint simulation.
Exact Microseconds saved: PENDING VERIFICATION; no Unity profiler numbers yet.

## Session 2026-05-11 Completion Pass

What was wrong: Docking capture had no strict distance/alignment gate, old PD fields implied force-based capture, S-curve target state was not stored as habitat-relative AUP, undock did not eject out of colliders, Seaglide thrust only affected speed/force and local battery float, and there was no docking black-box dump.

What was done: `VehicleDockingModule` now gates by AUP distancesq `< 2.0` and dot `> 0.8`, disables Rigidbody forces by setting kinematic at capture, interpolates to the dock in exactly 1.5s using S-curve + FastNlerp, stores dock target relative to habitat AUP, snaps on origin shift, restores/ejects on undock, queues numeric docking impact, exposes hatch/HUD state, pushes attached drone mass into submarine external mass, skips interpolation on Low/MX350, and records a 300-entry NativeArray telemetry ring that dumps to `Docs/AgentLogs/Dump_VEHICLE_MECH_DOCKING.bin` on invalid pose.

What was done: `MantaScooter` remains handheld; it now contributes KCC thrust plus drag coefficient multiplier and drains `PlayerInventory` SOA condition via `_qualityMilli`/`_durabilities` while thrusting. `IPlayerTransportSource`, `PlayerTransportCoordinator`, `HectonPlayerMovement`, `MountablePlayerTransport`, `PlayerInventory`, and `SubmarineFluidDynamics` received the minimal hooks needed.

Cinematic Cheats used: Kinematic matrix sync instead of physical dock simulation; 1.5s S-curve as visual fake for magnetic capture; low-tier instant snap; synthetic impact signal for heavy clunk; scalar drag multiplier instead of fluid scooter simulation.

Exact Microseconds saved: Profiler measurement blocked by external compile/runtime dependencies. Engineering estimates recorded: 20-80 us saved per dock event by avoiding solver joints; 4 us saved per Low/MX350 dock by skipping S-curve; 3 us/player tick for Seaglide scalar thrust/drag; 1-2 us for hatch/HUD/drone scalar queries; 10-40 us event savings by avoiding transform hierarchy repair across AUP shifts.

Verification: `VehicleDockingModule`, `IPlayerTransportSource`, and `PlayerInventory` validated cleanly through Unity MCP. `git diff --check` produced only CRLF warnings. `rg` confirmed no `FixedJoint`, `CharacterJoint`, or `SetParent` references in new docking module. Full compile remains blocked by external `DiegeticVisorHudMesh` `DamageSignal` ambiguity, `SaveBinaryStorage` Burst BC1007, and MSBuild child-node failure/timeouts.

## Session 2026-05-12 Honest R&D / OMEGA Polish

What was wrong: The first Seaglide condition drain was correct but still paid a hash scan on every accumulated drain tick. Docking telemetry also wrote every `Tick` even when the dock was idle, the serialized `dockingDurationSeconds` field triggered a local compile warning because the sanitizer forced the default while the resolver ignored the field, and OMEGA audit found avoidable normalize/division/modulo work in the active docking path.

What was done: Added `PlayerInventory.TryDrainItemConditionAtAnchor` and made `MantaScooter` cache the validated inventory anchor index plus item hash, with hash-scan fallback when the item moves or reservation/hash checks fail. `VehicleDockingModule` now allocates telemetry on enable, skips telemetry writes when not docking/docked, uses the serialized safe duration, removes redundant forward-vector normalization, uses `math.rcp(duration)` for S-curve normalization, and branch-wraps the 300-frame telemetry ring cursor instead of modulo.

Cinematic Cheats used: Cached SOA anchor condition drain instead of object battery simulation; direct finite dot checks instead of exact normalized alignment math; reciprocal multiply for S-curve time; branch-wrapped black-box ring; low-tier instant snap remains the Math LOD cheat; physical docking remains a deterministic kinematic fake, not a Unity joint.

Exact Microseconds saved: Estimated i3/MX350 savings are 1.5 us per Seaglide drain event after first successful anchor cache, 2 redundant normalizations per dock acquisition/telemetry sample removed, 1 float division removed per active fixed docking tick, 1 modulo removed per active telemetry write, and all idle telemetry writes removed. Profiler proof is still blocked by external compile errors.

Final Git Diff: `VehicleDockingModule.cs`, `IPlayerTransportSource.cs`, `MantaScooter.cs`, `MountablePlayerTransport.cs`, `PlayerTransportCoordinator.cs`, `HectonPlayerMovement.cs`, `PlayerInventory.cs`, and `SubmarineFluidDynamics.cs`; current stat for these files is 1893 insertions and 81 deletions. `Status_VEHICLE_MECH_DOCKING.md`, `Rationale_VEHICLE_MECH_DOCKING.md`, and this log were updated after that stat.

Verification: `VehicleDockingModule` and `PlayerInventory` Unity MCP validators returned zero diagnostics. `MantaScooter` standard validator reported duplicate `ResolveCurrentIntegrityNormalized`, but `rg` found one declaration and the basic validator timed out, so this is recorded as validator instability rather than a proven source duplicate. Touched-file `git diff --check` is clean except CRLF normalization warnings. Full `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /m:1` fails outside vehicle domain at `Assets/_Project/Scripts/Input/UserOptionsPersistence.cs:597` and `:598` because `HectonPersistentPathPolicy` is unresolved; package/third-party warnings also remain. Status cannot honestly be upgraded to VERIFIED MASTER GRADE.

## Session 2026-05-12 Honest R&D / Lifecycle Hardening

What was wrong: Dock finalize and origin-shift snap were optimistic. If the anchor went missing or invalid, the module could still proceed toward docked state or keep the vehicle locked. Undock also depended on trigger exit, which is not a reliable control surface once the body is manually held kinematic at the dock.

What was done: `SnapDockedBodyToAnchor` now returns success/failure. Invalid finalize, fixed tick, and origin-shift paths call a fail-closed abort that dumps `Dump_VEHICLE_MECH_DOCKING.bin` and releases the transport without ejecting into a bad pose. Added public `TryUndock(bool applyEjectVelocity = true)` so UI/input/vehicle systems can request deterministic release. Release now clears attached drone mass and `OnDestroy` disposes telemetry defensively.

Cinematic Cheats used: Fail-closed kinematic release instead of trying to physically recover a broken anchor; explicit eject API instead of waiting for trigger physics; cold-path binary dump remains the crash evidence path.

Exact Microseconds saved: Steady-state cost is 0 us. Failure-path savings are unbounded relative to a stuck kinematic vehicle because the recovery no longer depends on repeated trigger/physics attempts. Release bookkeeping adds only scalar clears and one optional telemetry write.

Verification: `VehicleDockingModule` Unity MCP validator returned zero diagnostics after the lifecycle hardening. Local `git diff --check -- VehicleDockingModule.cs` is clean except CRLF normalization warning. Unity console latest errors are external editor-test Burst symbol failures in `NativeArenaArrayEditTests.cs`. Full build still fails outside vehicle domain; the latest run reaches `Hecton8.Core` and reports 95 external errors, led by missing `HectonPersistentPathPolicy`, `HectonNativeBridge`, `HectonNativeLibrary`, `SteamDeckInputPal`, `VoxelChunkModifiedEvent`, `VoxelChunkModifiedEvents`, `HapticWaveformLibrary`, `HardwareTierDetector`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, and `HectonThreadRole`.
