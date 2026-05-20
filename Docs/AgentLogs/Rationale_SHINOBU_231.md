# Rationale_SHINOBU_231

Date: 2026-05-20
Status: PENDING VERIFICATION
Agent: SHINOBU_231

Problem: Upgrade stat evaluation request crosses equipment, vehicles, inventory, DataVault, visual sync, and telemetry. Direct concrete dependencies on unfinished agents would create compile walls.
Solution: Start with owner-local unmanaged DTOs/jobs and adapters that can be wired to Vault buffers later. Use `GlobalDataVault` only where existing interfaces are found; otherwise expose unmanaged kernels without inventing global surface.
Rejected Alternatives: Direct `GlobalRegistry` polling for PlayerInventory or vehicle components in the evaluation loop. This violates hot-path global authority law and creates branchy concrete coupling.
Scalability potential: Low uses the same deterministic bitwise truth at minimum cost; Middle runs full gameplay truth at normal cadence; High/Ultra spend saved time in VISUAL_SYNC through shader flags/glow/extrusion, not heavier simulation.
Hardware Impact: i3/MX350 gain depends on existing branch/OOP debt. Expected static target is eliminating virtual calls and string/list checks from stat loops; measured proof absent.

Problem: Upgrade mask layout requires ARM64-safe 64-bit reads.
Solution: Define `UpgradeMaskDTO` as `[StructLayout(LayoutKind.Explicit, Size = 16)]` with `EntityHashID` offset 0, `EquipmentHashID` offset 4, `ActiveUpgradesMask` offset 8.
Rejected Alternatives: Default sequential layout or `[StructLayout(Pack=1)]`. Sequential may drift under future edits; Pack=1 is banned for runtime DTOs with 8-byte fields.
Scalability potential: Low/Middle/High/Ultra all use same deterministic truth; presentation richness is decoupled through visual bits.
Hardware Impact: Prevents ARM64 misaligned `ulong` load traps and cache penalties; estimated prevention value is structural, not a claimed measured microsecond delta.

Problem: Complex upgrades such as depth modules do not stack linearly.
Solution: Cold-build flat LUTs for narrow bit groups and read them O(1) in the hot job. Use bit extraction and shifts to form LUT indices.
Rejected Alternatives: Sequential `if/else` priority chains and polymorphic `ApplyModifier(ref stats)` methods. Both create branch/dispatch cost and hardcoded behavior spread.
Scalability potential: Low uses the same LUT; Ultra can add visual flag richness without changing survival truth.
Hardware Impact: Replaces unpredictable branches with linear memory reads; expected low-end gain is branch predictor stability and better Burst vectorization. Measured proof absent.

Problem: Environmental upgrades require AUP precision without contaminating float stat math.
Solution: Keep AUP subtraction in double/int64 authority first, then cast local delta to `float3` for thermal/pressure sampling.
Rejected Alternatives: Casting absolute coordinates to float before subtracting origin. This loses precision at long range and breaks 50km sampling.
Scalability potential: Low samples coarse fields; Middle/High/Ultra can densify environment grids without changing the stat publication contract.
Hardware Impact: Prevents precision bugs rather than raw CPU savings. Low-end cost is bounded by branchless mask gate.
