"""Scratch diagnostic: which structure carries the run-breaking load, per row."""
import os, sys
import numpy as np

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
from h8forge import texture  # noqa: E402

RES = int(sys.argv[1]) if len(sys.argv) > 1 else 512
SEED = int(sys.argv[2]) if len(sys.argv) > 2 else 1713

spec = texture.GeologyTextureSpec(seed=SEED, quality=1.0, resolution=RES)
h = texture.build_height_field(spec)
lam = h.lamina


def runs(mask):
    intact = ~mask
    r = np.array([texture._longest_wrapped_run(intact[i]) for i in range(intact.shape[0])],
                 dtype=np.float64) / float(intact.shape[1])
    return r


parts = {
    "spall": lam.spall > 0.5,
    "joint": h.joint > 0.5,
    "fault": lam.fault_gouge > 0.5,
    "unconf": lam.unconformity > 0.5,
}
full = np.zeros_like(parts["spall"])
for v in parts.values():
    full |= v

print("res", RES, "seed", SEED)
for name, m in list(parts.items()) + [("ALL", full)]:
    r = runs(m)
    print("{:<8} cover {:.4f}  rowsTouched {:.4f}  runs p50 {:.4f} p95 {:.4f} max {:.4f}".format(
        name, m.mean(), m.any(axis=1).mean(),
        np.percentile(r, 50), np.percentile(r, 95), r.max()))

# leave-one-out: how much does each component contribute to breaking runs?
for name in parts:
    m = np.zeros_like(full)
    for k, v in parts.items():
        if k != name:
            m |= v
    r = runs(m)
    print("without {:<8} p95 {:.4f}".format(name, np.percentile(r, 95)))

r = runs(full)
bad = np.flatnonzero(r > 0.55)
print("rows over budget:", bad.size, "of", r.size)
if bad.size:
    print("  row block spans:", np.split(bad, np.flatnonzero(np.diff(bad) > 1) + 1)[:6].__len__(),
          "blocks; first rows", bad[:12].tolist())
    # transitions per bad row
    rows = full.astype(np.int8)
    tr = (rows != np.roll(rows, 1, axis=1)).sum(axis=1) // 2
    print("  crossings per bad row: mean {:.2f} min {} max {}".format(
        tr[bad].mean(), tr[bad].min(), tr[bad].max()))
    print("  crossings per good row: mean {:.2f}".format(tr[r <= 0.55].mean()))
