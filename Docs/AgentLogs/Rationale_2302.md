# Rationale 2302

## Decision: reject filename-only underwater proof

Reason: 1473 has files named underwater, but 2205 already inspected them as surface/coast-like or weak. Static evidence cannot prove active underwater state. Future packets must attach per-capture metadata.

## Decision: require editor/dev-only wrapper, not shipping capture code

Reason: `MMScreenshot` and dynamic MCP capture paths allocate, render to textures, encode PNG, and write files. That is acceptable only outside gameplay shipping code. Runtime save thumbnail capture is a different feature and is not proof packet authority.

## Decision: keep `Assets/Screenshots` as a hard reject

Reason: earlier logs show `Assets/Screenshots/screenshot-20260604-114736.png` imported by Unity. Current directory is empty, but any future write under `Assets` can trigger import loops and stale log risk.

## Decision: log tail must be newer than final screenshot

Reason: 1473 screenshot timestamps run through 2026-06-04 18:02:23; available 1474 logs are only Unity launch/licensing tails and do not prove scene/camera/state. A clean log must be from the same capture session and after the last PNG write.
