# HECTON-8: Xenon-Omega Liability & Corporate Risk System

## EXECUTIVE SUMMARY
This document defines the deep integration of the **Xenon-Omega Corporate Conspiracy** into Hecton-8’s core gameplay loops, algorithmic AI routing, and environmental pressure systems. Based on the five primary evidence nodes (Varnek, Arendt, Haldane, Ibarra, Sato-Ren), the game features a dynamic background system: **The Atlas-6 Substrate Optimization Protocol**.

The Atlas-6 Protocol is not merely lore—it is a live mathematical weighting system that drives base module deterioration, contractor (player) extraction gating, and hazard manifestation.

## 1. DYNAMIC SYSTEM: "DIRECTIVE WEIGHTING OVERRIDE" (Arendt Protocol)
**Lore Anchor:** Selene Arendt forced Atlas-6 to prioritize Xenon-Omega substrate over 800 workers by injecting a negative weight to biological preservation.

**Gameplay Implementation (`DirectiveWeightingSystem.cs`):**
- **Environmental Load Optimization:** As the player extracts higher volumes of Xenon-Omega, the station's AI (Atlas-6) actively diverts power away from Life Support and Pressure Shields in the player's current sector to stabilize the Substrate Containment vaults.
- **Pressure Failures:** Water ingress and structural buckling are NOT random. They are algorithmically spawned when the system determines the player's structural safety is a threat to the material claim.
- **The "Drown the Crew" Threshold:** If structural integrity falls below 15% in a sector with high Xenon-Omega density, bulkheads will autonomously lock the player *inside* the flooded zone to protect adjacent substrate storage. The player must hack or manually bypass Atlas-6 routing to escape.

## 2. THE 4% VARIANCE: "THERMAL SHEER MASKING" (Varnek Protocol)
**Lore Anchor:** Iliya Varnek smoothed a 14% risk variance to a 4% variance to avoid halting the extraction schedule, classifying anomalies as "engineered margins."

**Gameplay Implementation (`ThermalSheerManager.cs`):**
- **UI Lying:** The player's HUD, PDA, and sub OS (`HectonSubmarineOS`) will deliberately under-report thermal sheer and structural tension by exactly 10% when near primary drilling sites.
- **Sensory Disconnect:** The player must learn to ignore the "Green / Optimal" UI readouts and rely on audio-visual cues (groaning metal, micro-fractures, `AcousticSensoryXRayWindow` feedback) to detect imminent cryosphere collapse.
- **Downgraded Alerts:** Severe environmental hazards (e.g., thermal vents blowing) will be logged by the system as "Monitored Background Activity" instead of "Critical Alert," requiring the player to manually parse the raw `SignalPayloadLayout` to survive.

## 3. UNRESOLVED SYSTEM LOAD: LIABILITY DEFERMENT (Ibarra Protocol)
**Lore Anchor:** Marek Ibarra categorized 843 dead workers as "Unresolved System Load" to suspend life insurance payouts. Bodies cannot be legally recovered.

**Gameplay Implementation (`ActuarialLiabilitySystem.cs`):**
- **The Water Owns the Claim:** When the player discovers the corpses or remains of the 843 workers, attempting to scan their ID tags or recover their bodies generates **"Corporate Hostility."**
- **Bounty & De-prioritization:** The more bodies the player tags for recovery, the more Atlas-6 categorizes the player as an "Actuarial Threat." Drone repair cycles stop targeting the player's path, and automated defenses may flag the player as a "liability."
- **Ghost Data:** Worker PDA logs found in the deep provide essential blueprints, but uploading them to the main network causes the player to lose Corporate Credit.

## 4. QUARANTINE HOLD & SILENCE PROTOCOL (Haldane & Sato-Ren)
**Lore Anchor:** Noor Haldane held escape carriers at the staging lock until workers suffocated to prevent Xenon-Omega biomatter contamination. Vera Sato-Ren ordered cutting acoustic links if contractors transmit payroll or truth.

**Gameplay Implementation (`ExtractionGatingSystem.cs`):**
- **The Staging Lock Suffocation:** Extraction is never guaranteed. To call an extraction carrier (The Black Keel), the player must transmit pure Xenon-Omega proof.
- **The Silence Filter:** If the player attempts to upload any evidence of the disaster (the 2147 hold order, Arendt's logs) alongside their extraction request, the `SatoRenSilenceFilter` severs the connection. The carrier drops the tether. 
- **Contamination Tax:** If the player has accumulated "Xenon-Omega Biomatter Exposure" (a hidden radiation-like stat), the staging lock will permanently seal them out. The player must find illegal or makeshift decontamination showers (`BaseAirlock` hacks) before extraction, or they will be left to die like the previous crew.

## 5. CODE-LEVEL INTEGRATIONS (The `IUpdatable` Architecture)
To support this massive systemic integration without forbidden live loops, the systems hook into the statically audited `IUpdatable` interfaces:
- **`HectonSubmarineOS`**: Continuously recalculates the Varnek 4% UI lie.
- **`LifePodDamageSystem`**: Receives Arendt Directive weights to artificially accelerate damage when Xenon-Omega is present.
- **`PDAExchangeSystem`**: Runs the Sato-Ren silence protocol on all outgoing data transfers.
- **`BaseAirlock`**: Manages the Haldane quarantine logic, refusing cycle operations if biomatter is detected.

## ARCHITECTURAL CONCLUSION
This is not a traditional survival game. The environment is hostile, but the **AI logistics system is actively predatory**. Hecton-8 transforms the corporate lore from mere text logs into a punitive, mathematically precise opponent that values rocks over the player's oxygen.
