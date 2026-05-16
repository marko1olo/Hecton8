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
