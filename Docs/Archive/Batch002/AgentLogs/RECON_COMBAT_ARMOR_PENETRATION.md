# RECON_COMBAT_ARMOR_PENETRATION

Status: PENDING VERIFICATION
Timestamp: 2026-05-12 00:24:46 +04:00

## Exact Mandated Scan

Command:

`rg -n 'SendMessage\(\s*"ApplyDamage"\s*\)|GetComponent\s*<\s*IDamageable\s*>|GetComponentInParent\s*<\s*IDamageable\s*>' "C:\hades\Hecton8\Assets\_Project\Scripts\Fauna\FaunaBrain.cs" "C:\hades\Hecton8\Assets\_Project\Scripts\Gameplay" "C:\hades\Hecton8\Assets\_Project\Scripts\HectonSurvivalSystem.cs" "C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs"`

Result: no matches. No direct `SendMessage("ApplyDamage")`, `GetComponent<IDamageable>()`, or `GetComponentInParent<IDamageable>()` call was found in the requested scan surface.

## Legacy Damage Risk Scan

Command:

`rg -n 'OnCollisionEnter\s*\(|TakeDamage\s*\(|ApplyDamage\s*\(|Health\s*-=' "C:\hades\Hecton8\Assets\_Project\Scripts\Fauna\FaunaBrain.cs" "C:\hades\Hecton8\Assets\_Project\Scripts\HectonSurvivalSystem.cs" "C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs"`

Findings:

- `Assets/_Project/Scripts/HectonPlayerMovement.cs:5326` contains `OnCollisionEnter(Collision collision)`. It queues a `QueuedCollisionEvent` ring entry and reads only `GetContact(0)`; no direct health mutation was observed in that callback.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:4777` falls back to `playerHealth.TakeDamage(damage)` when `TryQueuePredatorBiteDamage` fails.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:4936` exposes a legacy `TakeDamage(float amount)` path for fauna health.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:5037` calls `TakeDamage(bonusDamage)` from a local fauna path.
- `Assets/_Project/Scripts/HectonSurvivalSystem.cs:1315` calls `_playerHealth.TakeDamage(damage, true)`.
- `Assets/_Project/Scripts/HectonSurvivalSystem.cs:2175` contains legacy survival `TakeDamage(float amount)` with direct integrity subtraction after event filtering.

## Assessment

Mandated `IDamageable`/`SendMessage` hazards are absent. Remaining risk is not string-dispatch damage; it is fallback direct `TakeDamage` when combat targets are not registered. Combat runtime now drains `GlobalSignals.DamageSignal` and local `CombatDamageSignal` queues, but migration of every legacy caller is outside this agent prompt and should be assigned to the owning player/fauna agents.
