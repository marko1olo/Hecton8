# L09 Product-Fix Critique (staff)
HEAD reviewed: a233fb682 / product fix 5b8c23aba
Scope: HectonPlayerMovement fixed-step input sample + PlayerToolManager IFixedTickable grant retry
Policy: real product only, no mocks. Critique of committed fix vs L08 measured fails.

---

## Executive verdict

| Fix | Verdict | Closes L08 measured fail? | Confidence |
|-----|---------|---------------------------|------------|
| HPM fixed-step locomotion sample | **CONDITIONAL PASS** (design right; one intent leak) | movementIntent01max=0 from Tick-starvation | **78%** |
| PTM FixedTick STARTERGRANT retry | **FAIL as complete fix** (drive path only; silent reg hole + vault root intact) | STARTERGRANT refusalMask 0x1E / available=0 | **42%** |

**Overall:** Do **not** fully trust an L09 green yet. Movement side is the stronger close. Tool side still has a real product registration hole and the 0x1E vault-lane root is unchanged. If L09 still shows movementIntent=0 or STARTERGRANT 0x1E, treat as residual product bugs below, not probe mocks.

---

## 1) HectonPlayerMovement.cs

### 1.1 SampleGameplayLocomotionInputForFixedStep ~L8208 - CONDITIONAL PASS

What landed:
- Resolves input binding, menu-zeros locomotion fields only, then ProcessPlayerInputFrame() + ProcessWipeoutInputOverride().
- Docstring correctly names L08 mode: FixedTick-heavy / render Tick starved, _input* stale while publishOk>0.

What is good:
- Right seam: fixed-step authority for move axes / vertical / sprint / wipeout.
- Does not call full HandleMenuBlockedInput (cursor/camera stay Tick-owned). Menu path zeros _inputH/_inputV/_inputVertical/_mouseXDelta and clears sprint - correct for physics step.
- Vehicle zeroing still happens inside ProcessPlayerInputFrame when transport active.

What is wrong / incomplete:
- Comment vs code: comment says look remains render-owned, but body calls full ProcessPlayerInputFrame(), which does ApplyLookInput (HPM ~L8149-8151, ApplyLookInput ~L8711). Fixed path is not locomotion-only.
- Jump consume uses default consumeBufferedJump: true on both Tick and FixedTick. First consumer wins; usually OK for buffer, but not isolated.

**Verdict:** PASS for the measured L08 intent-zero class. FAIL against its own look-ownership contract.

### 1.2 FixedTick call ~L9921-9935 - PASS

Order is correct: SampleGameplayLocomotionInputForFixedStep() then PrepareTransportAndFrameState(...).
Sample runs before transport/frame state snapshot that reads _input* into kinematics intent.

Residual early-outs above sample:
- if (suit == null) return;
- if (_juiceProcessor == null) return;
If either is null under probe, sample never runs and intent stays 0. Pre-existing, still live residual.

### 1.3 ProcessPlayerInputFrame still on render Tick? - YES (PASS)

Tick ~L8103 still calls ProcessPlayerInputFrame() for look/juice/cursor path. Fixed path did not remove render sampling. Dual ownership is intentional for batchmode, with caveats in 1.4.

### 1.4 Double-sample risk (Tick + FixedTick same frame) - PARTIAL FAIL

| Channel | Consume? | Double-sample effect |
|---------|----------|----------------------|
| MoveDelta / VerticalDelta / Sprint | GetState() snapshot, non-consuming | Harmless re-read of held axes |
| Jump buffer | TryConsumeBufferedAction | First lane wins; Fixed-first is better for gameplay |
| LookDelta | Applied via ApplyLookInput on both lanes | MUST-FIX product bug when both Tick and FixedTick run: look can apply 2x that frame |

For L09 movementIntent gate specifically: double move sample is fine. For shipping camera: not fine.

**Must-fix before trusting green means product-correct:** fixed sample must be locomotion-only (axes/sprint/wipeout/menu zero), not full ProcessPlayerInputFrame. Either extract SampleLocomotionAxesFromInputService() without look/jump-consume, or call TryReadFrame(..., consumeBufferedJump: false) and assign only _inputH/_inputV/_inputVertical + sprint, leave look to Tick.

### 1.5 Menu zeroing correctness on fixed path - PASS

Fixed menu branch zeros locomotion only; does not unlock cursor or drive juice/camera. Tick still owns HandleMenuBlockedInput. Correct split.

### 1.6 Does ResolveRawInputIntentVector / PrepareTransportAndFrameState still run after sample? - PASS

Yes. FixedTick samples then immediately PrepareTransportAndFrameState, which is the path that materializes intent from _input* into kinematics (CurrentMovementIntent01 chain). No post-sample dead zone introduced.

### HPM confidence vs L08 swim/intent fail

- **78%** this closes L08 movementIntent01max=0 when FixedTick runs with live suit/juice and InputDispatcher has non-zero MoveDelta (publishOk>0, inputEnabled, blockMask=0).
- Not 90%+ because: (a) FixedTick still gated on suit/juice null, (b) publishGuardFail residue can still starve state, (c) any later intent clamp/vehicle/wipeout/menu can zero after sample, (d) L09 still needs measurement.

---

## 2) PlayerToolManager.cs

### 2.1 class implements IFixedTickable L45 - PASS

PlayerToolManager implements IFixedTickable.

### 2.2 register L343-349 / unregister L362-365 - CONDITIONAL FAIL

Register block exists and calls GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player).

**MUST-FIX registration hole:**

TryRegisterToTickManager ~L330:
if ((_registeredToTick && _registeredToLateFrame) || !Application.isPlaying) return;

Early-out keys only Tick + LateFrame. _registeredToFixedTick is not part of the predicate.

Failure mode:
1. First OnEnable/register: Updatable + LateFrame succeed, FixedTickable fails (dispatcher not ready / bucket / SystemDispatcher.Register false).
2. Later retry (hot-swap Dispatcher, re-enable, etc.): early-out because tick && lateFrame already true.
3. _registeredToFixedTick stays false forever. Silent. No log.

Unrelated: unregister early-out if (!_registeredToTick && !_registeredToLateFrame) return; can also skip fixed-only unregister in a broken partial state.

Also: _registeredToFixedTick = TryRegister... swallows false with no DEV warning. Failures are silent by construction.

### 2.3 FixedTick L495-497 - PASS (body)

FixedTick only calls RetryRuntimeStartToolGrantIfPending(). Correct minimal fixed lane. Tick still calls retry first (~L395). Dual drive is right for batchmode.

### 2.4 Is TryRegisterToTickManager always called? Can FixedTick registration fail silently? - FAIL

Called from:
- OnEnable ~L295
- Dispatcher hot-swap ~L1641-1644 (unregister then register if active)

Not a continuous ensure-on-Tick. Combined with L330 early-out, yes, FixedTick registration can fail silently and never retry.

This is a product MUST-FIX before trusting L09 tool green attributable to this commit.

### 2.5 RetryRuntimeStartToolGrantIfPending still gated by vault 0x1E? - YES (residual FAIL root)

Retry still:
- bails if completed / grant disabled / inventory null
- increments attempts, eager then stride
- if !CanServiceItemAdds() -> optional TryRecoverRuntimeStorageCold() then return on failure
- else may stride on repeated refusal
- then TryGrantAssignedToolItemsOnRuntimeStart()

L08 mask 0x1E = bits 1..4: gridMissing, stackLaneDead, simStackLaneDead, simOccupancyLaneDead.

FixedTick only multiplies opportunities to recover/grant. It does not make dead vault lanes live. If TryRecoverRuntimeStorageCold cannot rebind after other vault allocs stamp meta, STARTERGRANT stays deferred and IsToolAvailableInSlot stays 0.

**Verdict:** drive-path fix only. Root vault serviceability not closed by 5b8c23aba.

### PTM confidence vs L08 STARTERGRANT / tools fail

- **42%** this alone turns L08 tool fail green.
- Would be ~70% if registration early-out fixed and L09 shows STORAGE recover + STARTERGRANT applied.
- Stays low while 0x1E can persist after recover attempts.

---

## 3) Residual risks for live L09 probe

If probe still reports:

### movementIntent still 0
Not explained away by fix not present. Check in order:
1. HPM FixedTick actually scheduled (_registeredFixedTick)
2. FixedTick not early-outing on null suit / null _juiceProcessor
3. IsGameplayInputBlockedByMenu false on fixed path
4. InputDispatcher still publishing (publishOk rising; not only publishGuardFail)
5. GetState().MoveDelta non-zero at sample time
6. Post-sample zeros: wipeout, vehicle transport, walking vertical clamp, authority gates
7. Intent metric sampled from kinematics after PrepareTransportAndFrameState, not a stale render mirror

### STARTERGRANT still 0x1E
Expected residual if vault lanes dead. FixedTick retry != vault heal.
Check:
1. _registeredToFixedTick == true (reg hole)
2. RetryRuntimeStartToolGrantIfPending attempt counter climbing on fixed cadence
3. CanServiceItemAdds / TryRecoverRuntimeStorageCold success logs
4. Final refusal mask after recover (not only first OnEnable 0x1E)
5. IsToolAvailableInSlot only after grant completed, not merely gridBound=true / inventoryVersion=0

### Other residual product bugs (not mocks)
- High publishGuardFail lock conflicts (L08 secondary) - separate from intent sample
- PNG / RESULT JSON / fauna / death route still unproven (out of this commit allowlist)
- HPM look double-apply when Tick+FixedTick both run
- PTM registration predicate stale w.r.t. fixed lane
- Any other gameplay still Tick-only remains unwired under batchmode physics flood (class bug, two instances already hit)

---

## 4) MUST-FIX before trusting L09 green

1. **PTM TryRegisterToTickManager early-out must include fixed lane**
   Treat registered as all required lanes, e.g. require _registeredToTick && _registeredToLateFrame && _registeredToFixedTick before return, or always attempt missing lanes without a compound early-out. Log DEV once on fixed register false.

2. **HPM fixed sample must not apply look**
   Stop calling full ProcessPlayerInputFrame from FixedTick; sample locomotion fields only. Prevents 2x look and matches the committed comment contract.

3. **Do not interpret L09 tool PASS without vault evidence**
   Require log proof: recover success and/or STARTERGRANT applied / owned counts - not merely FixedTick exists.

4. **If L09 movement still 0 after (2)**
   Instrument one DEV census on FixedTick: registered, suit/juice non-null, menu block, MoveDelta, _inputH/_inputV, intent01 - product truth, not a mock publish.

Nice-to-fix (not blocking movement gate): jump consume policy explicit on fixed vs render; unregister predicate includes _registeredToFixedTick alone.

---

## 5) Per-question answer sheet

### HPM
| Question | Answer |
|----------|--------|
| SampleGameplayLocomotionInputForFixedStep ~L8208 | Present; right idea; wrongly reuses full ProcessPlayerInputFrame (look leak) |
| FixedTick call ~L9921-9935 | Present; ordered before PrepareTransportAndFrameState |
| ProcessPlayerInputFrame still on render Tick? | Yes ~L8103 |
| Double-sample risk? | Move OK; jump first-wins; look double-apply real |
| Menu zeroing on fixed path | Correct (locomotion only) |
| ResolveRawInputIntent / PrepareTransport after sample? | Yes |

### PTM
| Question | Answer |
|----------|--------|
| IFixedTickable L45 | Yes |
| register L343-349 / unregister L362-365 | Present |
| FixedTick L495-497 | Calls RetryRuntimeStartToolGrantIfPending |
| TryRegister always / silent fail? | Can fail silent via L330 early-out |
| Retry still gated by vault 0x1E? | Yes |

---

## 6) Confidence summary

| Item | Confidence fix closes L08 measured fail |
|------|----------------------------------------|
| Movement intent zero (Tick-starved FixedTick) | **78%** |
| STARTERGRANT / tools available (0x1E class) | **42%** |
| Both gates green for honest L09 product PASS | **35%** until MUST-FIXes + vault evidence |

---

## Relevant file paths
C:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs
C:/hades/Hecton8/Assets/_Project/Scripts/PlayerToolManager.cs
C:/hades/Hecton8/Assets/_Project/Scripts/Gameplay/HectonPlayerInputHandler.cs
C:/hades/Hecton8/Assets/_Project/Scripts/Core/GlobalRegistry.cs
C:/hades/Hecton8/Assets/_Project/Scripts/PlayerInventory.cs
C:/hades/Hecton8/Docs/V0_Playtest/V0_L08_MEASURED.md
C:/hades/Hecton8/Docs/AgentLogs/V0_L08_MEASURED.md

