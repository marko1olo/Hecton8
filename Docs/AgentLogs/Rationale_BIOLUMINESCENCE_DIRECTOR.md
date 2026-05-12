# Rationale_BIOLUMINESCENCE_DIRECTOR

Date: 2026-05-12
Status: PENDING VERIFICATION

## Decision 0 - Prompt Boundary

Problem: The batch file contains prompts for multiple agents; neighboring tasks can contaminate architecture.
Solution: Extracted only `<AGENT_PROMPT id="BIOLUMINESCENCE_DIRECTOR">` using a CLI regex over `Docs/Tasks/CURRENT_BATCH.md`.
Rejected Alternatives: Reading adjacent prompts or using partial IDE context was rejected because it violates strict parsing and would mix domains.
Scalability potential: Low uses one global phase and no local ripple work. Middle adds limited ripples. High/Ultra can afford ripple detail and stronger glow response.
Hardware Impact: On i3/MX350 this prevents per-agent scope drift, not a direct runtime gain. Runtime target remains 0 B/frame and less than 0.1 ms suspicious threshold.

## Decision 1 - Mandate Selection

Problem: Bioluminescence touches lighting, shaders, global timing, AUP shifts, low-tier performance, and telemetry.
Solution: Loaded visual-fake-first, zero-GC, performance budgets, abyssal lighting, noir shader, URP hot path, crash telemetry, and AUP precision mandates.
Rejected Alternatives: Loading all mandates was rejected as noise. Loading only shader mandates was rejected because the prompt requires registry, telemetry, and AUP safety.
Scalability potential: Low disables touch ripple shader work. Middle keeps bounded 16 ripple inputs. High/Ultra use saved CPU from global pulse to buy stronger emissive/ripple visuals.
Hardware Impact: Expected MX350 gain comes from deleting per-object Update/MPB pulse paths and replacing them with one global shader state.
