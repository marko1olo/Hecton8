"""L14.1: fix sticky re-assert — Unregister+Register when sticky-true.

GlobalRegistry.TryRegisterFixedTickable / TryRegisterUpdatable call
_bucket.TryRegister which typically returns false when the item is already
present. Re-calling TryRegister while sticky-true would clear the flag and
unregister thrash. Correct product pattern: when sticky-true, force
Unregister then TryRegister so EnsureDispatcherRegistration always restores
bucket membership.
"""
from pathlib import Path
import sys

HPM = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs")
ROOT = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts")


def dump_bucket() -> None:
    out = []
    for p in ROOT.rglob("*.cs"):
        t = p.read_text(encoding="utf-8", errors="replace")
        if "bool TryRegister" not in t:
            continue
        if "RegistryBucket" not in t and "RegistryBucket" not in p.name:
            continue
        i = t.find("bool TryRegister")
        out.append(f"FILE {p}")
        out.append(t[i : i + 900])
        out.append("====")
    Path(r"C:\hades\Hecton8\Tools\_cline_scratch\_l14_bucket.txt").write_text(
        "\n".join(out), encoding="utf-8"
    )
    print("bucket dumps", len(out))
    if out:
        print(out[1][:600] if len(out) > 1 else out[0][:600])


def fix_hpm() -> None:
    text = HPM.read_text(encoding="utf-8")
    old = """            // L14: sticky bools alone are insufficient. A prior true with a missing bucket entry
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
            }"""

    # Also match mojibake emdash variant from prior write
    old_alt = old.replace("—", "")

    new = """            // L14: sticky bools alone are insufficient. A prior true with a missing bucket entry
            // (OnEnable raced empty Dispatcher, unregister partial, domain reload) permanently
            // no-ops EnsureDispatcherRegistration and starves FixedTick/Sample/hop2.
            // TryRegister* returns false when already present (bucket.TryRegister reject), so
            // re-call is NOT idempotent-true. Force Unregister+Register when sticky-true so
            // membership is restored without thrashing the false-negative clear path.
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredTick = false;
            }
            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);

            if (_registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixedTick = false;
            }
            _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);"""

    if old in text:
        text = text.replace(old, new, 1)
        print("OK replaced unicode emdash variant")
    elif old_alt in text:
        text = text.replace(old_alt, new, 1)
        print("OK replaced mojibake variant")
    elif "Force Unregister+Register when sticky-true" in text:
        print("SKIP already fixed")
        return
    else:
        # try looser match via marker
        marker = "// L14: sticky bools alone are insufficient."
        if marker not in text:
            print("FAIL no L14 sticky block")
            sys.exit(2)
        # find block from marker through cold tick section start
        i = text.find(marker)
        j = text.find("if (!_registeredColdTick)", i)
        if j < 0:
            print("FAIL no cold tick anchor")
            sys.exit(3)
        # include preceding whitespace on marker line start
        line_start = text.rfind("\n", 0, i) + 1
        text = text[:line_start] + new + "\n\n            " + text[j:]
        print("OK replaced via marker slice")

    HPM.write_text(text, encoding="utf-8", newline="\n")
    print("WROTE", HPM, "bytes", HPM.stat().st_size)
    v = HPM.read_text(encoding="utf-8")
    assert "Force Unregister+Register when sticky-true" in v
    assert "else if (!GlobalRegistry.TryRegisterFixedTickable" not in v
    print("PASS verify")


if __name__ == "__main__":
    dump_bucket()
    fix_hpm()
