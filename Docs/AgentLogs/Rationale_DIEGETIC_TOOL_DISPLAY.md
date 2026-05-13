# Rationale: DIEGETIC_TOOL_DISPLAY

Status: PENDING VERIFICATION

## Initial Mandate Selection
Problem: Floating screen-space tool HUD violates VR/diegetic UI requirements and creates hot-path UI allocation risk.
Solution: Use local tool-surface rendering, shared RT budget, shader bars/flashes, and preallocated char buffers with `TMP_Text.SetCharArray()`.
Rejected Alternatives: Screen-space overlay and `CanvasScaler` HUD are rejected because they break VR presence and trigger Canvas rebuild paths; per-tool RT allocation is rejected because it burns VRAM on MX350.
Scalability potential: Low disables RT camera and uses static emissive plus visor fallback; Middle uses shared 256 RT; High can raise update cadence; Ultra can spend saved cycles on stronger scanline/glitch/readability shaders.
Hardware Impact: Estimated low-end gain is avoiding canvas rebuild and per-tool RT churn, target under 0.1 ms CPU and about 0.13 MB color RT plus depth for active tool at 256x256 RGB565/D16. Exact profiler data absent.
