# -*- coding: utf-8 -*-
"""L13 product fix: HPM FixedTick samples locomotion before suit/juice early-outs;
public EnsureDispatcherRegistration; driver EnsureGameplay calls it."""
from pathlib import Path

HPM = Path(r"C:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs")
DRV = Path(r"C:/hades/Hecton8/Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs")


def patch_hpm(text: str) -> str:
    old_fixed = """        public void FixedTick(float fixedDeltaTime)
        {
            SuitData suit = currentSuitData;
            if (suit == null) return;
            if (_juiceProcessor == null) return;

            using (_fixedTickProfilerMarker.Auto())
            {
                // Fixed-step locomotion must sample input here. Render Tick is not guaranteed
                // before each FixedTick (batchmode / catch-up), and PrepareTransportAndFrameState
                // snapshots intent from _input* immediately below.
                SampleGameplayLocomotionInputForFixedStep();
"""
    new_fixed = """        public void FixedTick(float fixedDeltaTime)
        {
            // L13: Sample locomotion BEFORE suit/juice physics gates. hop2 (GetState) must not
            // depend on suit asset or juice processor being ready — L12 proved dispatcher already
            // holds non-zero MoveDelta while movementIntent01max stayed 0 because these early-outs
            // skipped SampleGameplayLocomotionInputForFixedStep for the whole Swim window.
            EnsureJuiceProcessor();
            SampleGameplayLocomotionInputForFixedStep();

            SuitData suit = currentSuitData;
            if (suit == null)
                return;

            using (_fixedTickProfilerMarker.Auto())
            {
"""
    if old_fixed not in text:
        raise SystemExit("HPM FixedTick block not found exactly")
    text = text.replace(old_fixed, new_fixed, 1)

    # Insert public EnsureDispatcherRegistration near TryRegisterToDispatchers
    old_reg = """        private void TryRegisterToDispatchers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
"""
    new_reg = """        /// <summary>
        /// L13: Product entry so gameplay bootstrap / headless route can re-assert Player fixed+update
        /// lane registration if OnEnable raced an empty Dispatcher. Does not mock input.
        /// </summary>
        public void EnsureDispatcherRegistration()
        {
            TryRegisterToDispatchers();
        }

        private void TryRegisterToDispatchers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
"""
    if old_reg not in text:
        raise SystemExit("TryRegisterToDispatchers block not found")
    if "public void EnsureDispatcherRegistration()" not in text:
        text = text.replace(old_reg, new_reg, 1)
    return text


def patch_driver(text: str) -> str:
    old = """            IInputService input = GlobalRegistry.RegisteredInput;
            if (input != null)
            {
                input.SwitchToPlayerInput();
                _switchedToPlayerInput = true;
            }
        }

        private static void TickSettle()
"""
    new = """            IInputService input = GlobalRegistry.RegisteredInput;
            if (input != null)
            {
                input.SwitchToPlayerInput();
                _switchedToPlayerInput = true;
            }

            // L13: Re-assert HPM Player fixed-tick registration so SampleGameplay/GetState (hop2)
            // runs during Swim. Suit/juice no longer gate sampling (HPM FixedTick L13), but a
            // missed TryRegisterFixedTickable still starves the entire locomotion read path.
            HectonPlayerMovement movement =
                _movement as HectonPlayerMovement
                ?? UnityEngine.Object.FindFirstObjectByType<HectonPlayerMovement>();
            if (movement != null)
            {
                movement.EnsureDispatcherRegistration();
                if (_movement == null)
                    _movement = movement;
            }
        }

        private static void TickSettle()
"""
    if old not in text:
        raise SystemExit("EnsureGameplayLocomotionInputReady tail not found")
    if "EnsureDispatcherRegistration()" not in text:
        text = text.replace(old, new, 1)
    return text


def main():
    hpm = HPM.read_text(encoding="utf-8-sig")
    # preserve newline style
    nl = "\r\n" if "\r\n" in hpm else "\n"
    hpm_n = hpm.replace("\r\n", "\n")
    hpm_n = patch_hpm(hpm_n)
    HPM.write_text(hpm_n.replace("\n", nl), encoding="utf-8", newline="")
    print("HPM patched OK")

    drv = DRV.read_text(encoding="utf-8-sig")
    nl2 = "\r\n" if "\r\n" in drv else "\n"
    drv_n = drv.replace("\r\n", "\n")
    drv_n = patch_driver(drv_n)
    DRV.write_text(drv_n.replace("\n", nl2), encoding="utf-8", newline="")
    print("DRV patched OK")

    # verify
    h2 = HPM.read_text(encoding="utf-8-sig")
    d2 = DRV.read_text(encoding="utf-8-sig")
    assert "L13: Sample locomotion BEFORE" in h2
    assert "public void EnsureDispatcherRegistration()" in h2
    assert "EnsureDispatcherRegistration()" in d2
    # Sample must appear before suit null return in FixedTick
    idx_ft = h2.find("public void FixedTick(float fixedDeltaTime)")
    chunk = h2[idx_ft : idx_ft + 600]
    assert "SampleGameplayLocomotionInputForFixedStep()" in chunk
    assert chunk.find("SampleGameplayLocomotionInputForFixedStep()") < chunk.find("if (suit == null)")
    print("VERIFY OK")
    print(chunk[:500])


if __name__ == "__main__":
    main()
