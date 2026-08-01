# -*- coding: utf-8 -*-
"""L15 product fix: dual-register heal + HPM lane-membership ensure."""
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

GR = r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\GlobalRegistry.cs"
HPM = r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs"
SD = r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\SystemDispatcher.cs"

# --- GlobalRegistry: heal dual-register for Fixed / Updatable / Cold ---

OLD_FIXED = """        public static bool TryRegisterFixedTickable(IFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;
            if (!_fixedTickables.TryRegister(item))
                return false;

            if (!SystemDispatcher.Register(item, layer))
            {
                _fixedTickables.Unregister(item);
                return false;
            }

            return true;
        }"""

NEW_FIXED = """        public static bool TryRegisterFixedTickable(IFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;

            // L15: dual-register heal. RegistryBucket.TryRegister returns false when
            // Contains — previously that aborted BEFORE SystemDispatcher.Register, so a
            // desync (global bucket has item, fixed lane cleared / never healed) left the
            // owner permanently off the dispatch path. Soft-reset / lane Clear can create
            // that split. If global already contains, still ensure the dispatcher lane.
            bool addedToGlobal = _fixedTickables.TryRegister(item);
            if (!addedToGlobal && !_fixedTickables.Contains(item))
                return false;

            if (SystemDispatcher.GetFixedLane(layer).Contains(item))
                return true;

            if (!SystemDispatcher.Register(item, layer))
            {
                // Only roll back a registration we just added; do not strip a pre-existing
                // global entry that may still be valid for another lane heal attempt.
                if (addedToGlobal)
                    _fixedTickables.Unregister(item);
                return false;
            }

            return true;
        }"""

OLD_UPDATABLE = """        public static bool TryRegisterUpdatable(IUpdatable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;
            if (!_updatables.TryRegister(item))
                return false;

            if (!SystemDispatcher.Register(item, layer))
            {
                _updatables.Unregister(item);
                return false;
            }

            return true;
        }"""

NEW_UPDATABLE = """        public static bool TryRegisterUpdatable(IUpdatable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;

            // L15: dual-register heal (same pattern as TryRegisterFixedTickable).
            bool addedToGlobal = _updatables.TryRegister(item);
            if (!addedToGlobal && !_updatables.Contains(item))
                return false;

            if (SystemDispatcher.GetLane(layer).Contains(item))
                return true;

            if (!SystemDispatcher.Register(item, layer))
            {
                if (addedToGlobal)
                    _updatables.Unregister(item);
                return false;
            }

            return true;
        }"""

OLD_COLD = """        public static bool TryRegisterColdTickable(IColdTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;
            if (!_coldTickables.TryRegister(item))
                return false;

            if (!SystemDispatcher.Register(item, layer))
            {
                _coldTickables.Unregister(item);
                return false;
            }

            return true;
        }"""

NEW_COLD = """        public static bool TryRegisterColdTickable(IColdTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;

            // L15: dual-register heal (same pattern as TryRegisterFixedTickable).
            bool addedToGlobal = _coldTickables.TryRegister(item);
            if (!addedToGlobal && !_coldTickables.Contains(item))
                return false;

            if (SystemDispatcher.GetColdLane(layer).Contains(item))
                return true;

            if (!SystemDispatcher.Register(item, layer))
            {
                if (addedToGlobal)
                    _coldTickables.Unregister(item);
                return false;
            }

            return true;
        }"""

# --- HPM TryRegisterToDispatchers: verify lane membership ---

OLD_HPM = """        private void TryRegisterToDispatchers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            // L14: sticky false -> TryRegister once. sticky true -> leave registered.
            // Do NOT Unregister+Register every Ensure: WorldDriver calls Ensure every settle/swim
            // tick; thrash would drop HPM from the fixed lane mid-hold. RegistryBucket.TryRegister
            // returns false when Contains (not idempotent-true), so re-call alone cannot heal a
            // missing entry either - membership loss is rare; SD L14 no longer bootstrap-skips
            // Player lane, so once registered FixedTick runs for hop2/Sample/intent.
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

NEW_HPM = """        private void TryRegisterToDispatchers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            // L15: sticky alone is insufficient. Soft-reset / dispatcher lane Clear can leave
            // GlobalRegistry containing HPM while the Player fixed lane does not (dual-register
            // desync). WorldDriver calls Ensure every settle/swim tick — do NOT Unregister+
            // Register thrash; instead verify actual lane membership and re-TryRegister when
            // sticky is false OR lane is missing. GR L15 heals desync when global Contains.
            // FixedTick on Player lane is the only path to hop2 (GetState) + movementIntent01.
            if (_registeredTick && !SystemDispatcher.GetLane(PriorityLayer.Player).Contains(this))
                _registeredTick = false;
            if (!_registeredTick)
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);

            if (_registeredFixedTick && !SystemDispatcher.GetFixedLane(PriorityLayer.Player).Contains(this))
                _registeredFixedTick = false;
            if (!_registeredFixedTick)
                _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);

            if (_registeredColdTick && !SystemDispatcher.GetColdLane(PriorityLayer.Player).Contains(this))
                _registeredColdTick = false;
            if (!_registeredColdTick)
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Player);

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


def apply(path, old, new, label):
    text = open(path, encoding="utf-8", errors="replace").read()
    if old not in text:
        # try CRLF
        old_crlf = old.replace("\n", "\r\n")
        new_crlf = new.replace("\n", "\r\n")
        if old_crlf in text:
            text = text.replace(old_crlf, new_crlf, 1)
            open(path, "w", encoding="utf-8", newline="").write(text)
            print("OK", label, "(crlf)")
            return True
        print("FAIL", label, "- old block not found")
        # dump nearby for debug
        key = old.split("\n")[0].strip()
        idx = text.find(key)
        print("  key:", repr(key), "idx", idx)
        if idx >= 0:
            print(repr(text[idx:idx + 200]))
        return False
    text = text.replace(old, new, 1)
    open(path, "w", encoding="utf-8", newline="\n").write(text)
    print("OK", label)
    return True


# Verify GetLane is public
sd = open(SD, encoding="utf-8", errors="replace").read()
if "public static RegistryBucket<IUpdatable> GetLane" not in sd and "public static RegistryBucket<" not in sd:
    # find GetLane signature
    import re
    m = re.search(r".{0,40}GetLane\(PriorityLayer", sd)
    print("GetLane context:", repr(m.group(0) if m else "MISSING"))
else:
    print("GetLane present")

# Check GetLane visibility
import re
for pat in [r"public static RegistryBucket<.*> GetLane\(", r"static RegistryBucket<.*> GetLane\(", r"private static RegistryBucket<.*> GetLane\("]:
    m = re.search(pat, sd)
    if m:
        print("MATCH", m.group(0))

ok = True
ok &= apply(GR, OLD_FIXED, NEW_FIXED, "GR Fixed")
ok &= apply(GR, OLD_UPDATABLE, NEW_UPDATABLE, "GR Updatable")
ok &= apply(GR, OLD_COLD, NEW_COLD, "GR Cold")
ok &= apply(HPM, OLD_HPM, NEW_HPM, "HPM TryRegister")
print("ALL_OK" if ok else "PARTIAL_FAIL")
