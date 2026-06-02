# Rationale 1622

Problem: Active batch file does not contain `<AGENT_PROMPT id="1622">`; exact XML task count cannot be recovered from disk.
Solution: Record the prompt defect, constrain scope to the direct user assignment and stable ECHELON 6 power-grid domain definition, and avoid neighboring prompt contamination.
Rejected Alternatives: Reading 1626/1627/1628/1629 prompts, using the 1622 list mention as a full prompt, or inventing missing task text.
Scalability potential: Low/Middle/High/Ultra policy remains continuous through `GlobalQualityWeight`; no binary quality lane will be introduced.
Hardware Impact: Documentation-only step; 0 runtime impact on i3/MX350.

Problem: Damaged and overheated cable states needed a deterministic CSR route without expanding power DTO layout.
Solution: Added bit-only edge states for thermal trip and sparking contact, then resolved CSR conductance with `math.select` masks: sealed/short/thermal/offline endpoints hard-zero, damaged contacts zero base conductance, explicit spark contacts leak a tiny fixed conductance.
Rejected Alternatives: Adding cable thermal fields to `PowerGridEdgeDTO`, scene-side cable behaviours, or rebuilding graph truth from managed objects; all would violate flat CSR/native ownership and increase hot-path surface.
Scalability potential: Low uses the same cheap mask math; Middle/High/Ultra can raise visual spark density outside solver via continuous `GlobalQualityWeight` without changing electrical truth.
Hardware Impact: Replaces branch returns in conductance resolution with scalar masks; expected win is small but stable on i3/MX350 because adjacency build stays linear and data-local.

Problem: Demand collapse needed fan-style outage signalling without latching nodes permanently offline or depending on a managed event route.
Solution: Added recoverable `NodeFlagCascadeShed` in `PowerVoltageSolverJob`; the solve path uses sanitized demand, active masks, and `math.select` flag writes so a node can shed under low voltage and clear when supply returns.
Rejected Alternatives: Setting `NodeFlagOffline`, adding external reset services, or introducing quality-tier outage thresholds. Offline latching would hide recovery; quality-tier thresholds would change gameplay truth.
Scalability potential: Low/Middle/High/Ultra keep identical truth; quality only affects existing smoothing/fidelity, while visual blackout/spark cadence can scale separately.
Hardware Impact: Eliminates the damaged/offline early-return branch from the normal per-node solve path; expected gain on i3/MX350 is consistency, not a measurable frame budget windfall.

Problem: Black-box telemetry did not distinguish ordinary brownout from cascade shed.
Solution: Mixed brownout/cascade state bits into the telemetry hash and set `TelemetryReasonCascadeShed` when shed nodes exist, preserving the 64-byte telemetry DTO.
Rejected Alternatives: Adding telemetry fields or external JSON reports; both add schema churn and do not improve runtime proof.
Scalability potential: Same telemetry layout works on weak and top-tier devices; higher tiers can consume reason flags for richer diagnostics without solver changes.
Hardware Impact: One extra flag count/hash mix per node in telemetry job; POST_SIM/diagnostic cost only, not a hot managed allocation path.
