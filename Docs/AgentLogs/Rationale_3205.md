# Rationale 3205

- Kept P464 as a standalone production draft packet only. Reason: task forbids editing P461/P462/P463/RS093/route_cards/source CSV/h8bin/runtime outputs.
- Treated Runtime/Monolith content as authoring intent only. Reason: DataMonolith readiness requires source CSV/hash/bake/import/boot proof not present in this task.
- Used draft_machine_or_llm for every non-English locale. Reason: no native/fluent review or RTL/CJK/layout proof was provided.
- Reworded Portuguese scanner label to ASCII `REIVINDICACAO`. Reason: task requires U+00C3 count 0; uppercase Portuguese `Ã` is a legitimate character but still violates this static marker gate.
