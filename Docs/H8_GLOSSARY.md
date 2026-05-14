# HECTON-8 Glossary

Date: 2026-05-14
Status: ENCYCLOPEDIA VERIFIED
Evidence class: STATIC_DOC

Scope: shared terminology for agents and developers. Runtime proof is out of scope.

## Terms

| Term | Definition |
|---|---|
| AUP | Absolute Universe Position. Large-world position expressed as integer sector/grid plus local float offset. Runtime truth uses AUP; `Transform.position` is presentation. |
| AUP hash | Compact hash derived from AUP sector/local data for signals, telemetry, save keys, or spatial ownership checks. |
| Black Box | Fixed-size circular telemetry buffer, normally 300 frames, storing high-level state before crash or NaN dump. |
| Bucketer | Deterministic workload distributor that spreads SlowTick or simulation work across frame buckets to avoid spikes. |
| COLD ALLOC | Required source comment for intentional initialization allocation with capacity, reason, and owner. |
| DataVault | Central owner of native runtime buffers. Systems request handles; the vault owns capacity, generation, relocation, disposal, and memory telemetry. |
| Dirty page | Data page marked changed so VISUAL_SYNC or persistence uploads only what changed. |
| EventBus | Legacy/general queue term. New gameplay broadcasts use typed SignalBus lanes, not string events. |
| FrostTick | Very low-frequency cadence for cold audits, memory checks, and broad policy updates. |
| H-Phi | Static architecture pressure score combining data sovereignty, synaptic density, phase discipline, and evidence multiplier. It is triage, not runtime proof. |
| Math LOD | Tiered math fidelity. Low tier uses cheap approximations; high and ultra spend saved cycles on richer visuals, not uncontrolled simulation. |
| MX350 | Minimum GPU target with 2 GB VRAM. It drives hard frame, memory, shader, post-process, and visual-fake constraints. |
| Sentinel | Runtime guard or monitor that tracks memory, native allocation, health, or fault state and emits telemetry before failure becomes silent. |
| SHI | System Health Index. A scalar health/pressure indicator used for load shedding or warnings; high SHI means system pressure is dangerous, recovery must use hysteresis. |
| Signal lane | Typed bounded broadcast channel with unmanaged payload, owner, phase, capacity, overflow policy, and telemetry fields. |
| SOA | Struct of Arrays. Data layout where each field is stored in a flat array for cache-friendly jobs and Burst access. |
| SystemDispatcher phase | One of `PRE_SIMULATION`, `SIMULATION`, `POST_SIMULATION`, or `VISUAL_SYNC`. Runtime work must declare its phase. |
| Vault handle | Stable reference to a DataVault-owned buffer with buffer id, system id, generation, count, capacity, and safety checks. |
| Visual fake | Deterministic presentation shortcut that preserves player belief without simulating invisible physical causes. |
| Visual overkill | High/ultra-tier spending of saved performance on richer presentation after gameplay truth stays within budget. |

## Required Five

- AUP: position truth model.
- Vault: native data ownership model.
- Sentinel: fault/memory/health guard model.
- SHI: system pressure scalar.
- Bucketer: deterministic cadence distribution model.

