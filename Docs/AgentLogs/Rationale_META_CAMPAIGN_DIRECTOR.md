# META_CAMPAIGN_DIRECTOR Rationale

## Intake Decision
Problem: Meta campaign logic must exist without restoring singleton architecture or string-driven quest checks.
Solution: Use GlobalRegistry contract binding, GlobalSignals queue consumption, pre-baked FNV1a uint identifiers, NativeParallelHashMap state, and Burst-compatible rule structs.
Rejected Alternatives: CampaignManager.Instance/GameManager.Instance because singleton lookup couples unrelated systems and violates local architecture mandate. String quest keys because runtime hashing/compare is allocation-prone and brittle.
Scalability potential: Low uses state-change-only toxicity/color/encounter gates. Middle adds global radio and POI deltas. High adds stronger visual overkill through shader global intensity. Ultra can add downstream renderer embellishments without changing the campaign evaluator.
Hardware Impact: i3/MX350 expected hot-frame impact near 0 us when no progression signal arrives; state shift work is cold path.

## Persistence Decision
Problem: Native campaign variables must survive save/load without managed dictionary authority.
Solution: Serialize fixed-capacity uint/int pairs in compact sorted/run-friendly arrays inside SaveData payload.
Rejected Alternatives: JSON/string mod data because it bypasses binary save ownership and adds runtime parsing.
Scalability potential: Low stores a small packed map. High/Ultra can add additional variables without changing the event evaluator contract.
Hardware Impact: Save/load work is cold path; no steady-frame cost.
