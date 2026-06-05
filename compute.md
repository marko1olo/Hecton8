# HECTON-8 GPU Compute And Kernel Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Evidence class: STATIC_DOC
Scope: compute shaders, GPU simulation, buffer ownership, dispatch sizing, async readback, barriers, current/silt/swarm kernels, GPU-driven presentation, and MX350-first kernel proof.

## First-20 Route Hook

- First-20 moment: shallow water density, silt, bioluminescence, marine snow, indirect draw support, and optional route diagnostics may use compute only when gameplay truth stays owner-routed.
- Route blocker removed: same-frame GPU readback, unowned buffers, missing barriers, MX350-overrun kernels, and compute output treated as immediate survival/tool truth.
- Proof class: `STATIC_DOC` until Frame Debugger, RenderDoc or Unity Profiler GPU capture, RenderGraph/barrier proof, and async readback evidence exist.

## Prime Law

GPU compute is a throughput tool, not a magic realism button. HECTON-8 uses compute when it moves dense visual or amortized data work off the CPU and keeps the result bounded, readable, and measurable. It rejects compute kernels that hide O(N^2) logic, stall the bus, desync gameplay, or exist only because "GPU sounds fast."

MX350 is the floor. High-end GPUs buy richer presentation after compact proof exists.

## Truth Ownership

Compute owns GPU-side presentation fields, simulation buffers explicitly assigned to GPU, indirect draw support, and async diagnostic outputs. It does not automatically own gameplay truth.

Gameplay truth may consume compute output only through an owner-approved delayed readback or CPU-side authoritative mirror. Render-only buffers are excluded from save, rollback, Merkle hashes, and gameplay authority unless a route explicitly says otherwise.

## Buffer Contract

Every compute buffer must declare:

- owner system;
- element stride;
- capacity;
- read/write access;
- lifetime;
- clear/reset route;
- producer pass;
- consumer pass;
- barrier requirement;
- async readback behavior;
- compact memory budget.

Use `GraphicsBuffer` for new C# graphics/compute resource routes. Constant data belongs in grouped constant buffers. Hot CPU-to-GPU uploads use dirty pages or ranges. Full-buffer hot reupload is rejected unless the buffer is documented as all-dirty.

## Dispatch Sizing

Do not hardcode 256-thread assumptions as universal law. Portable logic kernels default to 64 threads unless a capture proves another group size on the target device.

Dispatch rules:

- shader `numthreads` and C# group count must agree;
- C# dispatch code queries or shares generated constants for kernel group size;
- no early return before `GroupMemoryBarrierWithGroupSync` when other threads in the group can reach the barrier;
- large dispatches are split, staggered, or indirect to avoid TDR;
- a single kernel over 2 ms GPU time is rejected without an emergency load-shed route.

## Synchronization And Barriers

GPU work must name dependencies.

Required:

- UAV barrier between dependent write/read passes;
- RenderGraph resource declaration where applicable;
- no render pass consuming a same-frame compute write without a declared barrier;
- no CPU readback in the same frame;
- no `GetData` or blocking readback in gameplay/render hot paths;
- async readback ring with at least a three-frame delay for diagnostics or gameplay-latency-tolerant data.

If a compute result is needed immediately for gameplay truth, the design is probably wrong and must use CPU authoritative data or delayed command logic.

## Kernel Math Law

Kernel math must be finite and cheap.

Required:

- finite checks before grid/cell indexing;
- bounds checks before buffer writes;
- safe normalization with `dot(v,v) > epsilon`;
- denominator guards;
- saturate/clamp before packing;
- camera-relative positions for shader math;
- no unbounded trigonometry in inner loops;
- polynomial or LUT approximations where visual error is acceptable;
- no exp/log/pow in hot kernels unless proof shows necessity.

## Approved Compute Uses

Strong candidates:

- swarm/flocking presentation when counts justify GPU;
- silt and marine snow fields;
- current field advection at low cadence;
- GPU particles with hard pool caps;
- visibility/indirect draw preparation;
- bioluminescence atlas updates;
- occlusion or HLOD assist;
- diagnostic readbacks with delay.

Weak candidates:

- single-object logic;
- player-critical collision;
- oxygen or survival truth;
- inventory/crafting;
- save identity;
- immediate gameplay queries;
- anything with constant readback pressure.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` scales dispatch range, update cadence, particle count, field resolution, optional diagnostic readback, near-field detail, and presentation density.

It must not change gameplay authority, packet layout, save identity, rollback hash fields, or whether compute output is trusted as truth.

Compact uses smaller fields, lower cadence, stricter caps, and stronger interpolation. Middle uses proved default kernels. High and Ultra add density, resolution, and secondary visual layers only with GPU captures.

## Proof Artifacts

Compute work must provide:

- kernel name and `numthreads`;
- C# group-count derivation;
- buffer layout and byte budget;
- producer/consumer graph;
- barrier plan;
- async readback plan if any;
- compact GPU capture;
- RenderDoc, Unity Profiler, Frame Debugger, or RenderGraph proof for runtime kernels;
- TDR guard;
- fallback when compute is unavailable or over budget;
- explicit rollback/save exclusion or inclusion route.

## Rejection Gates

Reject compute work if:

- it assumes a desktop warp/group size without proof;
- it uses compute to hide bad data layout;
- it blocks on GPU readback;
- it writes gameplay truth without owner route;
- it has no buffer lifetime owner;
- it exceeds compact GPU budget;
- it lacks barriers;
- it uses full-buffer uploads as normal hot path;
- it changes gameplay by quality tier;
- it reports speed without capture evidence.

## Acceptance Sentence

Compute is accepted only when it is MX350-proved, buffer-owned, barrier-safe, finite, asynchronously observed, continuously scalable, and used to buy visible density without stealing gameplay truth from CPU owners.
