# UX_HPHI_SIGNAL_WIRING Status

Authority: `Docs/Tasks/CURRENT_BATCH.md` `<AGENT_PROMPT id="UX_HPHI_SIGNAL_WIRING">`
Role: UX_ENGINEER
Domain: ECHELON 8 - PRESENTATION & UX
Task count: 15 primary tasks
Status: PENDING VERIFICATION

## Gatekeeping
- [x] Batch prompt extracted cover-to-cover | Justification: PowerShell raw read + singleline regex isolated only this XML tag. Alternative rejected: chat memory or neighboring prompts. Estimate: 240 us.
- [x] Hygiene check for own state files | Justification: `Status_UX_HPHI_SIGNAL_WIRING.md` and `Rationale_UX_HPHI_SIGNAL_WIRING.md` were absent before creation. Alternative rejected: reusing another agent log. Estimate: 55 us.
- [x] Relevant mandates identified and read | Justification: UI zero-GC, diegetic UI, registry DI, zero-GC policy, telemetry, performance, input abstraction, and VR stencil were loaded before code. Alternative rejected: coding from generic Unity assumptions. Estimate: 1900 us.
- [x] Root and Docs authority review | Justification: Loaded `AGENTS.md`, domain map, docs README, architecture map, runtime plan, system contracts, quality gates, signal corridor, zero-GC UI pipeline, dispatch pipeline, and root authority trackers before code. Alternative rejected: isolated source-only edit. Estimate: 4800 us.

## Primary Tasks
- [ ] 1. Singleton eradication in `Assets/_Project/Scripts/UI` | Justification: pending. Alternative rejected: pending. Estimate: pending.
- [ ] 2. Signal migration to `SignalBus<PlayerStateSignal>`, `SignalBus<InventoryChangedSignal>`, `SignalBus<SystemHealthSignal>` | Justification: pending. Alternative rejected: pending. Estimate: pending.
- [ ] 3. ASMDEF isolation for `Hecton8.UI` | Justification: pending. Alternative rejected: pending. Estimate: pending.
- [ ] 4. Initialization cache / formatter cache | Justification: pending. Alternative rejected: pending. Estimate: pending.
- [ ] 5. Register UI controllers as `ILateFrameTickable` in VISUAL_SYNC | Justification: pending. Alternative rejected: pending. Estimate: pending.
- [ ] 6. Dirty flag snapshot reads | Justification: pending. Alternative rejected: pending. Estimate: pending.
- [ ] 7. Zero-GC TMP render via `SetCharArray` | Justification: pending. Alternative rejected: pending. Estimate: pending.
- [ ] 8. Steam Deck / gamepad glyph switching from `InputStateSignal` | Justification: pending. Alternative rejected: pending. Estimate: pending.
- [ ] 9. VR recentering lazy-follow without `Quaternion.Slerp` | Justification: pending. Alternative rejected: pending. Estimate: pending.
- [ ] 10. H-Phi Update deletion count documented | Justification: pending. Alternative rejected: pending. Estimate: pending.
- [ ] 11. Low-tier 15Hz UI dirty evaluation throttle | Justification: pending. Alternative rejected: pending. Estimate: pending.
- [ ] 12. Static scan: no `string.Format` or LINQ in UI update path | Justification: pending. Alternative rejected: pending. Estimate: pending.
- [ ] 13. Blackbox telemetry `ActiveUiUpdatesPerFrame` | Justification: pending. Alternative rejected: pending. Estimate: pending.
- [ ] 14. Triple-strike assembly repair | Justification: pending. Alternative rejected: pending. Estimate: pending.
- [ ] 15. Omega compile / managed string allocation check | Justification: pending. Alternative rejected: pending. Estimate: pending.

## Iterative Loops
- [ ] Loop 1: Tasks 1-5 then compile
- [ ] Loop 2: Tasks 6-10 then compile
- [ ] Loop 3: Tasks 11-15 then compile
- [ ] Loop 4: Re-read prompt and self-review
- [ ] Loop 5: Polish mandate after all core tasks done or blocked
