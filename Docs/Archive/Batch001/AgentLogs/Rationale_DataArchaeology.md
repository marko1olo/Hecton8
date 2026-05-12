# Rationale DataArchaeology

## Session Start

Problem: Scanner discovery currently advances on held focus scan only; the active prompt requires signal tuning, hash-based unlocks, MMF text, and zero-GC persistence without replacing the scanner architecture.

Solution: Add a scanner-owned runtime component and narrow hooks into `ScannerTool`/`ScannableFragment`. Use existing `ScanEvents`, `LoreDatabaseManager`, `LoreMmfEncyclopedia`, `GlobalRegistry`, and `Hecton_BlueprintWireInstanced.shader`.

Rejected Alternatives: A new scanner controller would duplicate tool ownership and risk divergent scan state. A full physical reconstruction simulation was rejected; the player-facing result is a deterministic wireframe draw batch.

Scalability potential: Low tier uses parabolic wave matching and 64 hologram instances. Middle/High can increase material fidelity through the existing wire shader. Ultra can use denser authored meshes without changing discovery truth.

Hardware Impact: Expected hot-path cost is one active fragment frequency evaluation plus fixed ring-buffer writes, below 0.01 ms idle on i3/MX350. Exact profiler proof is pending Unity Editor GCMonitor logs.

## Decision - Scanner-Owned Runtime

Problem: Discovery tuning must alter focused scan progression without creating a competing tool loop.
Solution: Attach `DataArchaeologyRuntime` to the existing scanner in cold `Awake()` and keep scanner focus as the owner of held-scan progression.
Rejected Alternatives: A separate scanner controller would duplicate range/cooldown/pulse state. A global singleton would violate registry/service ownership.
Scalability potential: Low tier pays only when a fragment is actively focused; higher tiers can increase hologram mesh density/material fidelity.
Hardware Impact: Idle path is one registered render callback with `_hologramCount <= 0` early return, estimated under 1 us.

## Decision - Parabolic Signal Proxy

Problem: Prompt asks for sine matching, but `sin/cos` in held scan is wasteful and not visually/audibly distinguishable.
Solution: Use a parabolic sine proxy and preserve the gameplay truth with `math.abs(signal - noise) < threshold`.
Rejected Alternatives: `math.sin`/`math.cos` pair per scan tick; 1024 LUT was rejected because phase evaluation is already scalar and cheaper than memory fetch plus cache pressure here.
Scalability potential: Low/Middle use proxy; High/Ultra can spend saved time in shader/hologram presentation, not gameplay math.
Hardware Impact: Estimated 3-6 us saved per active scan tick on low-end CPU.

## Decision - Fixed Bit/Array Persistence

Problem: Discovery flags and partial scan progress must persist without string keys or bloated save payload.
Solution: Store discovery in 16 `long` words and partial scans in fixed hash/progress arrays; codec writes the 16 discovery words as raw longs.
Rejected Alternatives: `bool[1024]`, `HashSet<string>`, or JSON maps were rejected for payload size and GC.
Scalability potential: 1024 entries remain fixed-cost; higher tiers do not change persistence format.
Hardware Impact: Discovery flag payload is exactly 128 bytes; estimated 896 B saved versus bool payload and no runtime string hashing after fragment cache.

## Decision - Wireframe Reconstruction

Problem: Completed artifact reconstruction needs to look diegetic without CPU mesh repair work.
Solution: Reuse `Hecton_BlueprintWireInstanced.shader` and `Graphics.DrawMeshInstanced` over fixed matrices.
Rejected Alternatives: CPU reconstruct missing mesh parts or spawn per-fragment GameObjects. Both add CPU, GC, and lifecycle risk.
Scalability potential: Low tier caps at 64 instances; High/Ultra can use richer authored meshes/material settings.
Hardware Impact: Estimated 80-300 us saved per completion event versus CPU-side reconstruction.
