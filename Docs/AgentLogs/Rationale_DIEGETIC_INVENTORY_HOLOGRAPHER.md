# Rationale_DIEGETIC_INVENTORY_HOLOGRAPHER

STATUS: PENDING VERIFICATION

## Decision 1 - Native Signal Boundary
Problem: The assignment requires `PlayerInputSignal(ToggleInventory)`, but the project only exposes managed `OnInventory` events and the generic `InputStateSignal`.
Solution: Add a 32-byte `PlayerInputSignal` lane in `GlobalSignals` and publish `ToggleInventory` from `InputDispatcher` while leaving managed events alive for existing consumers.
Rejected Alternatives: Directly subscribing the hologram to `IInputService.OnInventory` was rejected because the prompt demands signal migration and managed delegates create tighter ordering/coupling. Polling hardware input was rejected because `InputDispatcher` is the authoritative input owner.
Scalability potential: Low uses one tiny native signal when inventory is pressed. Middle/High/Ultra can add more player input commands without UI systems binding to input package types.
Hardware Impact: Estimated 0.2-0.6 us only on the press frame on i3/MX350; 0 us steady frame.

## Decision 2 - SOA Snapshot Before Burst
Problem: `ItemTemplateRegistry` is managed and cannot be called from Burst, while the grid job needs icon atlas indices.
Solution: Copy up to 64 hash/count records into owned persistent NativeArrays on dirty/toggle frames and resolve icon atlas indices on the main thread before scheduling the Burst layout job.
Rejected Alternatives: Calling the registry inside Burst is impossible. Holding direct `PlayerInventory` arrays across frames was rejected because other systems can mutate the vault.
Scalability potential: Low copies 64 records max and uses a flat grid. Middle/High/Ultra can spend saved cycles on curvature, hover math, and glow shader intensity.
Hardware Impact: Copy/lookup occurs only on inventory dirty/open frames; estimated 4-18 us on MX350-class CPU depending registry hit rate, not a per-frame slot rebuild.

## Decision 3 - GPU Hologram Instead Of UGUI Slots
Problem: Standard `ScrollRect`/slot prefabs break VR/diegesis and allocate during layout rebuilds.
Solution: Render icon quads from persistent matrices and a shader atlas through indirect GPU drawing; optional count labels use preallocated char buffers and `TMP_Text.SetCharArray`.
Rejected Alternatives: Rebuilding `GridLayoutGroup`, pooling uGUI slots, or instantiating 3D item slot prefabs was rejected because those remain GameObject/UI-slot patterns.
Scalability potential: Low/Middle use 64 quads and flat atlas sampling. High/Ultra can keep the curved projection and stronger hologram material without changing data flow.
Hardware Impact: Eliminates Canvas layout/rebuild cost for the backpack view; expected savings are bursty and scene-dependent, commonly hundreds of microseconds on weak CPUs when old UI would rebuild.

## Decision 4 - AUP And VR Safety
Problem: Inventory UI should not drift or break during Absolute Universe Position shifts, and VR hover must not create physics queries.
Solution: Build matrices from camera/hand local basis every dirty/hover frame and use analytic ray-to-slot tests against cached quad centers.
Rejected Alternatives: World-space persisted hologram transforms and collider-driven hover were rejected because they add AUP coupling and physics overhead.
Scalability potential: Low uses flat camera plane. Middle uses light curved offsets. High/Ultra use fuller cylindrical layout and brighter shader.
Hardware Impact: Low-tier flat path avoids trig. Hover is bounded to 64 analytic checks; estimated 3-12 us on low-end CPU while open.

## Decision 5 - Black Box Scope
Problem: The global Black Box mandate requires critical systems to retain recent high-level state, but this task is a presentation system, not authoritative physics/AI.
Solution: Add a fixed 300-frame managed telemetry ring for open state, revision, slot count, hover slot, and finite-matrix flags, plus dump on NaN detection.
Rejected Alternatives: Ignoring telemetry was rejected by mandate. Allocating a new `NativeArray<TelemetryEntry>` after crash was rejected because crash paths must already have storage.
Scalability potential: All tiers keep the same 300 compact records. High/Ultra visual complexity does not change postmortem coverage.
Hardware Impact: One struct write per open-frame/tick; below 1 us on i3/MX350, no GC after cold allocation.
