# SHINOBU_36 Rationale

Date: 2026-05-17
Domain: INPUT_DETERMINISM_AND_HAPTICS
Status: IMPLEMENTED - CORE COMPILE BLOCKED BY EXTERNAL UI/WAKE CONTRACTS

## Decision Log

### R0 - Working Memory Recovery

Problem: Agent state files for SHINOBU_36 did not exist, so the batch had no durable progress trail.

Solution: Created `Docs/Tasks/Status_SHINOBU_36.md` and `Docs/AgentLogs/Rationale_SHINOBU_36.md` before code changes. Disk files are now the durable state.

Rejected Alternatives: Chat-only tracking was rejected because context compression would destroy task continuity.

Scalability potential: Documentation has no runtime frame cost. It protects parallel-agent coordination across weak, middle, high, and ultra hardware targets.

Hardware Impact: 0 us runtime gain; prevents integration churn that would waste developer iteration time on i3/MX350-class machines.

### R1 - Contract and Buffer Repair Path

Problem: Existing deterministic input path owns local persistent NativeArrays and uses Pack=1 structs, violating H-Phi data sovereignty and ARM64 alignment rules.

Solution: Keep the existing InputDispatcher as the integration point because SystemDispatcher already calls it in PRE_SIMULATION. Move SHINOBU_36 deterministic history/current/mask/profile/telemetry buffers to GlobalDataVault handles and add aligned DTOs instead of rewriting sibling domains.

Rejected Alternatives: A new parallel input service was rejected because it would fight GlobalRegistry.Input and create compile-order churn. Full InputManager deletion was rejected for the first loop because UI/rebind code still owns generated InputSystem assets.

Scalability potential: Weak devices get one cached poll, a 24-byte DTO, and fixed rings. Middle/high/ultra tiers can consume the same journal for replay, oscilloscope, and haptic overkill without touching gameplay truth.

Hardware Impact: Estimated 4-10 us saved on i3/MX350 during replay/history writes by eliminating owner-local NativeArray churn and using 512 fixed entries in Vault; ARM64 avoids misaligned reads that can become multi-dozen-cycle stalls.

### R2 - Polling Authority Over InputManager Callbacks

Problem: `InputManager` used `InputAction.performed/canceled` subscriptions, making button edge timing dependent on managed callback order instead of the PRE_SIMULATION poll.

Solution: Leave `InputManager` as the generated asset/rebind/UI module owner, but remove callback subscriptions and make `InputDispatcher` poll cached `InputAction` references directly. Edges are emitted by mask XOR after the deterministic sample is written.

Rejected Alternatives: Deleting `InputManager` was rejected because generated action ownership, rebinding, and UI module references are already coupled to it. Continuing to trust callback-latched state was rejected because it keeps the multiplayer desync vector.

Scalability potential: Low = one poll pass and fixed bitmask. Middle = same DTO plus 10-frame buffer. High/Ultra = replay/journal/oscilloscope reads without changing gameplay truth.

Hardware Impact: Estimated 6-18 us saved on weak CPU frames by removing callback fan-out from input authority; more importantly, deterministic edge order eliminates rollback divergence cost.

### R3 - Haptic DTO and Dear Lie Bridge

Problem: Existing tool haptics used a larger command struct and platform-specific dispatch logic. Quest/OpenXR amplitude pulses and gamepad low/high motors were not unified under a 16-byte DTO.

Solution: Added `HapticCommandDTO` and a 16-slot Vault command buffer. `HapticRequest` and mock collision impulses insert DTOs, a fixed evaluator applies Pade-style exponential decay, and XR receives a unified amplitude with a 0.02s pulse duration.

Rejected Alternatives: Per-device physical rumble models and direct VR SDK probing were rejected. They create device divergence and waste frame time for no gameplay truth.

Scalability potential: Low = throttled 15 Hz haptic dispatch with half amplitude under Steam Deck critical pressure. Middle = standard 60 Hz input with decayed haptics. High/Ultra = overlapping command overkill while the same DTO remains deterministic.

Hardware Impact: Estimated 20-80 us and motor battery load saved during thermal pressure by reducing haptic writes to 15 Hz; DTO evaluation remains bounded at 16 slots.

### R4 - Human Control Without Recompile

Problem: Deadzone, mouse acceleration, and haptic power were hardcoded, forcing C# recompilation for tuning drift-prone test kits.

Solution: Added a Play Mode editor window and a root `input_profiles.csv` watcher. Both write `InputProfileDTO` directly into GlobalDataVault memory; the poller consumes that profile in PRE_SIMULATION.

Rejected Alternatives: ScriptableObject-only tuning was rejected because it still creates managed object indirection and asset serialization churn. Per-frame file polling was rejected because it would create avoidable I/O pressure.

Scalability potential: Low = larger inner deadzone and low haptic scale. Middle = standard exponential curve. High = sharper aim curve. Ultra = haptic overkill via the same bounded command buffer.

Hardware Impact: Estimated 0 us steady-state cost after profile load; avoids repeated editor recompiles and preserves Steam Deck MicroSD by using event-driven file monitoring.

### R5 - Compile Wall Boundary

Problem: `Hecton8.Core.csproj` cannot complete because unrelated files reference missing `TerminalOS.ISignal` and `GlobalPhysicsStateManager.WakeRequestSignal`, outside SHINOBU_36 ownership.

Solution: Verified `Hecton8.Input.csproj` passes and reran `Hecton8.Core.csproj`; after SHINOBU_36 fixes, only external TerminalOS and wake signal errors remain. Marked this as dependency-blocked instead of editing UI/physics contracts.

Rejected Alternatives: Adding dummy `ISignal` or `WakeRequestSignal` shims was rejected because it would mask other agents' missing contracts and pollute the global signal corridor.

Scalability potential: No runtime effect; preserves parallel-agent isolation.

Hardware Impact: 0 us runtime gain; avoids integration churn and false green builds.
