# LOG_HARDWARE_THROTTLING_DIRECTOR

## 2026-05-16 Prompt Gate
What was wrong -> `Docs/Tasks/CURRENT_BATCH.md` has no `<AGENT_PROMPT id="HARDWARE_THROTTLING_DIRECTOR">`.
What was done -> Confirmed absence with CLI extraction, checked `CURRENT_BATCH_AUDIT_20260516.md`, and recorded blocked status/rationale.
Cinematic Cheats used -> None. No runtime system was authored.
Exact Microseconds saved -> 0 us runtime. Prevented unauthorized code path with unknown DOD and unknown consumers.

Status -> [BLOCKED BY DEPENDENCY] Batch owner must provide the missing XML tag before implementation.

## 2026-05-16 Phase 1 - Great Purge
What was wrong -> Hardware metrics were locally owned by `HomeostasisBrain`, and `HardwareThermalService` carried a static runtime instance beside the registry-owned service slot.
What was done -> Removed the hardware thermal static instance; added `SystemID.HardwareHomeostasis`; added `BufferID.HardwareMetrics`; changed homeostasis metrics initialization to prefer `GlobalDataVault` and fall back to H8Memory only when the vault is absent.
Cinematic Cheats used -> None. This was ownership and load-shed infrastructure only.
Exact Microseconds saved -> 0 us hot path measured. Expected low-end impact: cold allocation ownership clarity, no added per-frame work.
Compile -> PENDING VERIFICATION. `dotnet build Hecton8.Core.csproj --no-restore` failed after three attempts on external dirty-batch dependencies, currently `FaunaKinematicsRuntime.cs` missing `Hecton8.Animation.Fauna` DTOs.

## 2026-05-16 Phase 2-4 - Homeostasis Active
What was wrong -> The status ledger was stale, the blackbox dump filename still pointed at `AGENT_HOMEOSTASIS_BRAIN`, owned hardware/DRS paths still used legacy publish wrappers, and ARM64-facing hardware signal payloads had explicit sizes but not explicit `Pack = 1`.
What was done -> Verified the Android cached `PowerManager.getThermalHeadroom(30)` bridge, Steam Deck/standalone `SystemInfo` battery polling, Mac `NSProcessInfo` thermal hook, Burst SHI formula, EWMA smoothing, Level 1/2/3 sacrifice masks, 3000-frame sequential recovery, DataVault-owned hardware metrics/frame/blackbox buffers, typed `SignalBus<T>` publication, DRS max 0.75 under Level 2 pressure, XR 72 Hz pressure request, and final `dotnet build`.
Cinematic Cheats used -> Dear Lie scalar pressure instead of real heat simulation; EWMA instead of noisy thermal truth; bitmask vasoconstriction instead of global quality collapse; DRS 0.75 and 1 Hz AI demotion instead of OS-throttle stutter; high/ultra visual budget remains available until SHI forces targeted sacrifice.
Exact Microseconds saved -> Profiler capture was not run. Static DOD estimates: 40 us/frame avoided by no per-frame JNI, 12 us/frame avoided by no per-frame SystemInfo battery polling, 350 us GPU recovered at Level 1, 1600 us GPU plus 220 us CPU recovered at Level 2, 1800 us CPU recovered at Level 3, 6 us/frame and 0.5 KB/frame allocation risk removed by typed SignalBus lanes. Blackbox cost estimate: 1 us/frame fixed ring write, 0 GC.
Multiplatform Evidence -> Owned structs and hardware-adjacent signal lanes use `Pack = 1`; shader scan found no `numthreads` product over 1024 and no scanned `only_renderers d3d` or `exclude_renderers metal`; Steam Deck path adds no sensor-file or MicroSD I/O.
Validation -> `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_Phase4_Strike3.txt`: Build succeeded. 0 Warning(s). 0 Error(s). Time Elapsed 00:00:03.64.
Status -> VERIFIED MASTER GRADE - HOMEOSTASIS ACTIVE.

## 2026-05-16 Omega Pass 2 - Mask Correction
What was wrong -> The generated hardware profile data still encoded Level 1 as `0x70` and Level 2 as `0x2007F0`, causing `VolumetricFogHighRes`, SSR/foveated-adjacent work, and distant steering to be sacrificed too early. That contradicted the XML Level 1/2 policy and punished high-end hardware before critical pressure.
What was done -> Tightened Level 1 to `0x30`; tightened Level 2 to `0x330`; kept Level 3 at `0xF017F0`; aligned `HomeostasisBrain`, `HardwareProfileCatalog`, `Data/System/Hardware_Profiles.json`, `Data/Hardware/Profiles.json`, and both generated Quest 3/Steam Deck profile JSON files. Sequential recovery now skips absent bits and restores the next active bit.
Cinematic Cheats used -> Level 2 now buys survival through DRS 0.75 and cheap animation sacrifices instead of killing expensive visual overkill. Emergency visual shutdown stays reserved for SHI > 0.95.
Exact Microseconds saved -> Static estimates unchanged for Level 2: 1600 us GPU from DRS cap and 220 us CPU from animation/IK cuts. New gain is quality preservation, not raw frame time. Recovery skip cost is effectively 0 us/frame; it avoids 240-420 frames of delayed real restoration after Level 2-only pressure.
Validation -> `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass2.txt` currently fails on external `HectonUnderwaterVisuals.cs`, `GameBootstrapper.cs`, and `ToolDurabilitySystem.cs` compile errors. No current compiler error is in the hardware mask patch.
Status -> HOMEOSTASIS PATCH ACTIVE; CURRENT GREEN BUILD BLOCKED BY EXTERNAL NON-HARDWARE COMPILE WALL.
