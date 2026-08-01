# -*- coding: utf-8 -*-
"""L14: replace thrash Unregister+Register with sticky-only registration."""
from pathlib import Path
import re
import sys

p = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs")
raw = p.read_bytes()
print("size", len(raw), "crlf", raw.count(b"\r\n"), "lf", raw.count(b"\n"))
text = raw.decode("utf-8")
use_crlf = "\r\n" in text

pat = re.compile(
    r"[ \t]*// L14: sticky bools alone are insufficient\..*?"
    r"_registeredFixedTick = GlobalRegistry\.TryRegisterFixedTickable\(this, PriorityLayer\.Player\);\r?\n",
    re.S,
)
m = pat.search(text)
print("match", bool(m), "span", m.span() if m else None)
if not m:
    # already fixed?
    if "sticky false -> TryRegister once" in text:
        print("ALREADY_FIXED")
        sys.exit(0)
    print("NO_MATCH")
    # dump around L14 sticky or registeredTick block
    i = text.find("_registeredFixedTick")
    print("first _registeredFixedTick", i)
    i2 = text.find("TryRegisterFixedTickable(this, PriorityLayer.Player)")
    print("TryRegisterFixedTickable", i2)
    if i2 >= 0:
        print(repr(text[max(0, i2 - 600) : i2 + 120]))
    sys.exit(1)

print("MATCHED HEAD:", repr(m.group(0)[:180]))
print("MATCHED TAIL:", repr(m.group(0)[-120:]))

replacement = """            // L14: sticky false -> TryRegister once. sticky true -> leave registered.
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
"""
if use_crlf:
    replacement = replacement.replace("\n", "\r\n")
    nl = "\r\n"
else:
    nl = "\n"

new_text, n = pat.subn(replacement + nl, text, count=1)
print("subs", n)
if n != 1:
    sys.exit(2)

tmp = p.with_suffix(".cs.l14tmp")
tmp.write_bytes(new_text.encode("utf-8"))
tmp.replace(p)

v = p.read_text(encoding="utf-8")
ok1 = "sticky false -> TryRegister once" in v
ok2 = "UnregisterUpdatable(this, PriorityLayer.Player)" not in v
ok3 = "_lastPlayerKinematicsIntendedMovement = ResolveRawInputIntentVector()" in v
print("has non-thrash comment", ok1)
print("no UnregisterUpdatable thrash", ok2)
print("has sample intent", ok3)
i = v.find("sticky false")
print(v[i - 40 : i + 550] if i >= 0 else "FAIL_SEGMENT")
if not (ok1 and ok2 and ok3):
    sys.exit(3)
print("OK")
