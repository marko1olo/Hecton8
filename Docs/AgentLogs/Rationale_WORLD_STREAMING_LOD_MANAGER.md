# Rationale_WORLD_STREAMING_LOD_MANAGER

Status: PENDING VERIFICATION

## 2026-05-13 Initialization

Problem: HLOD impostor residency is not connected to chunk hydration/dehydration, causing distant chunks to disappear instead of swapping into cheap impostor representation.

Solution: Inspect existing streaming, signal, HLOD, telemetry, memory, audio, and cartography contracts before edits; implement only inside world streaming/HLOD boundaries or via existing interfaces.

Rejected Alternatives: Direct GameObject impostors were rejected because the prompt requires no standard GameObject rendering path for impostors; raw dependency on neighboring agents' concrete classes is rejected because batch execution requires EventBus/GlobalRegistry boundaries.

Scalability potential: Low uses aggressive dehydration and cheapest impostor records. Middle uses stable crossfade. High extends residency with richer render data. Ultra can spend saved cycles on visual overkill through richer shader/dither data, not extra CPU objects.

Hardware Impact: Target i3/MX350 gain is expected from removing full chunk residency past low-tier range and replacing it with dense NativeArray records. Measured proof absent.
