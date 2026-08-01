# L14 product fix applicator
# 1) Sample: publish raw intent into _lastPlayerKinematicsIntendedMovement
# 2) TryRegisterToDispatchers: re-validate sticky fixed/update flags
# 3) SystemDispatcher: do not bootstrap-skip Player fixed lane (locomotion is input-authoritative)
from __future__ import annotations

import pathlib
import sys

HPM = pathlib.Path(r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs")
SD = pathlib.Path(r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\SystemDispatcher.cs")


def must_replace(text: str, old: str, new: str, label: str) -> str:
    if old not in text:
        # Already applied?
        if new in text or new.strip() in text:
            print(f"SKIP already applied: {label}")
            return text
        print(f"FAIL missing needle: {label}")
        print("--- needle start ---")
        print(old[:400])
        print("--- needle end ---")
        sys.exit(2)
    count = text.count(old)
    if count != 1:
        print(f"FAIL needle count={count} for {label}")
        sys.exit(3)
    print(f"OK replace: {label}")
    return text.replace(old, new, 1)


def patch_hpm(text: str) -> str:
    # --- A: SampleGameplayLocomotionInputForFixedStep publishes intent ---
    old_sample = """        private void SampleGameplayLocomotionInputForFixedStep()
        {
            ResolveInputManagerBinding();

            if (IsGameplayInputBlockedByMenu())
            {
                _currentInputState = default;
                _pendingLookInput = Vector2.zero;
                _inputH = 0f;
                _inputV = 0f;
                _inputVertical = 0f;
                _mouseXDelta = 0f;
                SetSprintingState(false);
                return;
            }

            ProcessPlayerInputFrame();
            ProcessWipeoutInputOverride();
        }"""

    new_sample = """        private void SampleGameplayLocomotionInputForFixedStep()
        {
            ResolveInputManagerBinding();

            if (IsGameplayInputBlockedByMenu())
            {
                _currentInputState = default;
                _pendingLookInput = Vector2.zero;
                _inputH = 0f;
                _inputV = 0f;
                _inputVertical = 0f;
                _mouseXDelta = 0f;
                SetSprintingState(false);
                // L14: menu block zeros intent metric so CurrentMovementIntent01 tracks Sample,
                // not a stale post-suit PrepareTransport write from a prior frame.
                _lastPlayerKinematicsIntendedMovement = default;
                return;
            }

            ProcessPlayerInputFrame();
            ProcessWipeoutInputOverride();
            // L14: publish raw locomotion intent at Sample (pre-suit). hop2/GetState already ran
            // inside ProcessPlayerInputFrame; CurrentMovementIntent01 must reflect this frame even
            // when suit==null early-outs before PrepareTransportAndFrameState.
            _lastPlayerKinematicsIntendedMovement = ResolveRawInputIntentVector();
        }"""

    text = must_replace(text, old_sample, new_sample, "HPM.Sample intent publish")

    # --- B: TryRegisterToDispatchers re-validates sticky flags ---
    old_reg = """        private void TryRegisterToDispatchers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            }

            if (!_registeredFixedTick)
            {
                _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
            }

            if (!_registeredColdTick)
            {
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Player);
            }

            TryRegisterLateFrameTickable();

            if (!_registeredPlayerMovementContracts)
            {
                IPlayerMovementContracts currentContracts = GlobalRegistry.PlayerMovementContracts;
                if (currentContracts == null)
                    GlobalRegistry.RegisterPlayerMovementContracts(this);

                _registeredPlayerMovementContracts = ReferenceEquals(GlobalRegistry.PlayerMovementContracts, this);
            }

            if (!_registeredHotSwapListener)
                _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);

        }"""

    new_reg = """        private void TryRegisterToDispatchers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            // L14: sticky bools alone are insufficient. A prior true with a missing bucket entry
            // (OnEnable raced empty Dispatcher, unregister partial, domain reload) permanently
            // no-ops EnsureDispatcherRegistration and starves FixedTick/Sample/hop2. Always
            // re-assert membership; registry TryRegister* is idempotent for already-present owners.
            if (!_registeredTick)
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            }
            else if (!GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player))
            {
                // Was sticky-true but bucket rejected / missing — clear and retry once next call.
                _registeredTick = false;
            }
            else
            {
                _registeredTick = true;
            }

            if (!_registeredFixedTick)
            {
                _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
            }
            else if (!GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player))
            {
                _registeredFixedTick = false;
            }
            else
            {
                _registeredFixedTick = true;
            }

            if (!_registeredColdTick)
            {
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Player);
            }

            TryRegisterLateFrameTickable();

            if (!_registeredPlayerMovementContracts)
            {
                IPlayerMovementContracts currentContracts = GlobalRegistry.PlayerMovementContracts;
                if (currentContracts == null)
                    GlobalRegistry.RegisterPlayerMovementContracts(this);

                _registeredPlayerMovementContracts = ReferenceEquals(GlobalRegistry.PlayerMovementContracts, this);
            }

            if (!_registeredHotSwapListener)
                _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);

        }"""

    text = must_replace(text, old_reg, new_reg, "HPM.TryRegister revalidate")
    return text


def patch_sd(text: str) -> str:
    old_skip = """        private static bool ShouldSkipLaneDuringBootstrap(int laneIndex, bool blockGameplayLanes)
        {
            if (!blockGameplayLanes)
                return false;

            // Bootstrap gates the player lane only. World/environment systems must keep
            // ticking so startup queues, residency, and spawn drains can complete.
            return laneIndex == GetLaneIndex(PriorityLayer.Player);
        }"""

    new_skip = """        private static bool ShouldSkipLaneDuringBootstrap(int laneIndex, bool blockGameplayLanes)
        {
            if (!blockGameplayLanes)
                return false;

            // L14: Player fixed/update locomotion is input-authoritative simulation, not optional
            // bootstrap garnish. Skipping PriorityLayer.Player while !BootstrapState.IsGameReady
            // starves HPM.FixedTick -> Sample -> GetState (hop2) even when InputDispatcher already
            // holds non-zero MoveDelta (L12/L13 LIVE: hop1 healthy, hop2 ABSENT, intent=0).
            // World/environment systems still run; Player lane must also run once registered so
            // scripted and human locomotion can sample the open input path during handoff.
            // (Previously: return laneIndex == GetLaneIndex(PriorityLayer.Player);)
            return false;
        }"""

    text = must_replace(text, old_skip, new_skip, "SD.ShouldSkipLaneDuringBootstrap Player always-run")
    return text


def main() -> int:
    hpm = HPM.read_text(encoding="utf-8")
    sd = SD.read_text(encoding="utf-8")
    hpm2 = patch_hpm(hpm)
    sd2 = patch_sd(sd)
    if hpm2 == hpm and sd2 == sd:
        print("NO CHANGES")
        return 1
    if hpm2 != hpm:
        HPM.write_text(hpm2, encoding="utf-8", newline="\n")
        print(f"WROTE {HPM} bytes={HPM.stat().st_size}")
    if sd2 != sd:
        SD.write_text(sd2, encoding="utf-8", newline="\n")
        print(f"WROTE {SD} bytes={SD.stat().st_size}")
    # verify markers
    hpmv = HPM.read_text(encoding="utf-8")
    sdv = SD.read_text(encoding="utf-8")
    checks = [
        ("L14 intent Sample", "_lastPlayerKinematicsIntendedMovement = ResolveRawInputIntentVector();" in hpmv
         and "publish raw locomotion intent at Sample" in hpmv),
        ("L14 menu zero intent", "_lastPlayerKinematicsIntendedMovement = default;" in hpmv),
        ("L14 revalidate sticky", "re-assert membership" in hpmv),
        ("L14 skip false", "return false;" in sdv and "L14: Player fixed/update locomotion" in sdv),
    ]
    ok = True
    for name, passed in checks:
        print(("PASS" if passed else "FAIL"), name)
        ok = ok and passed
    return 0 if ok else 4


if __name__ == "__main__":
    raise SystemExit(main())
