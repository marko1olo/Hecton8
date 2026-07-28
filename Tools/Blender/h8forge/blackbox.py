"""Fixed-capacity bake-step ring + failure dump.

3dmodel.md section 11, "Black Box And Failure Evidence":

    "Critical generator pipelines must keep the last 300 high-level bake steps in a
     fixed ring during generation: seed, family, stage, vertex count, triangle
     count, warning flags, hash, and failure code. On exception, non-finite
     geometry, or validation abort, the generator must dump the ring..."

    "The accepted answer to a corrupt mesh is never 'unknown.' The ring must
     explain the last accepted stage and the first invalid stage."

AGENTS.md ``Black Box`` narrows where the dump goes:

    "Use Docs\\AgentLogs\\Dump_[ID].bin only when an explicit agent ID exists;
     otherwise use system name and timestamp."

No agent ID is supplied to an offline generator run, so dumps land under
``Docs/AgentLogs/`` named by system + deterministic run tag. The ring itself is
preallocated: a generator that corrupts geometry must not also allocate while
reporting it.
"""

from __future__ import annotations

import json
import os
import sys
from typing import Any, Optional

from . import law


class BlackBox:
    """Preallocated ring of bake steps. Never grows, never raises on record."""

    __slots__ = ("_slots", "_capacity", "_write_index", "_total_recorded",
                 "_system", "_run_tag", "_first_invalid_index")

    def __init__(self, system: str, run_tag: str,
                 capacity: int = law.BLACKBOX_RING_CAPACITY) -> None:
        if capacity <= 0:
            raise ValueError("black box capacity must be positive, got " + str(capacity))
        # COLD ALLOC: list[capacity] - fixed bake-step ring - owner: BlackBox
        self._slots: list = [None] * capacity
        self._capacity = capacity
        self._write_index = 0
        self._total_recorded = 0
        self._system = system
        self._run_tag = run_tag
        self._first_invalid_index: Optional[int] = None

    # -- recording ---------------------------------------------------------

    def record(
        self,
        stage: str,
        *,
        seed: Optional[int] = None,
        family: Optional[str] = None,
        vertex_count: int = -1,
        triangle_count: int = -1,
        warning: str = "",
        digest: str = "",
        failure_code: str = "",
    ) -> None:
        """Append one bake step. Overwrites the oldest slot when full.

        Deliberately total: recording must never be the thing that throws while a
        generator is already failing.
        """
        entry = {
            "n": self._total_recorded,
            "stage": stage,
            "seed": seed,
            "family": family,
            "verts": vertex_count,
            "tris": triangle_count,
            "warning": warning,
            "digest": digest,
            "failure": failure_code,
        }
        self._slots[self._write_index] = entry
        if failure_code and self._first_invalid_index is None:
            self._first_invalid_index = self._total_recorded
        self._write_index = (self._write_index + 1) % self._capacity
        self._total_recorded += 1

    def note_invalid(self, stage: str, failure_code: str, detail: str = "") -> None:
        """Record a failure step. Marks the first invalid stage if not already set."""
        self.record(stage, warning=detail, failure_code=failure_code)

    # -- inspection --------------------------------------------------------

    @property
    def total_recorded(self) -> int:
        return self._total_recorded

    def last_accepted_stage(self) -> Optional[str]:
        """The newest step recorded without a failure code."""
        for offset in range(1, self._capacity + 1):
            idx = (self._write_index - offset) % self._capacity
            entry = self._slots[idx]
            if entry is None:
                continue
            if not entry.get("failure"):
                return entry.get("stage")
        return None

    def first_invalid_stage(self) -> Optional[str]:
        """The oldest step still in the ring that carried a failure code."""
        if self._first_invalid_index is None:
            return None
        ordered = self.ordered_entries()
        for entry in ordered:
            if entry.get("failure"):
                return entry.get("stage")
        return None

    def ordered_entries(self) -> list:
        """Ring contents oldest-first. Only the surviving window, by design."""
        out = []
        for offset in range(self._capacity):
            idx = (self._write_index + offset) % self._capacity
            entry = self._slots[idx]
            if entry is not None:
                out.append(entry)
        return out

    # -- dumping -----------------------------------------------------------

    def dump_dir(self) -> str:
        return os.path.join(law.project_root(), "Docs", "AgentLogs")

    def dump(self, reason: str) -> str:
        """Write the ring to Docs/AgentLogs and return the path.

        Named by system + run tag, per AGENTS.md ``Black Box`` -- no agent ID is
        available to an offline generator, and inventing one would be a fabricated
        batch identity.
        """
        directory = self.dump_dir()
        os.makedirs(directory, exist_ok=True)
        path = os.path.join(
            directory,
            "Dump_{system}_{tag}.json".format(system=self._system, tag=self._run_tag),
        )
        payload = {
            "system": self._system,
            "runTag": self._run_tag,
            "reason": reason,
            "forgeVersion": law.FORGE_VERSION,
            "ringCapacity": self._capacity,
            "totalRecorded": self._total_recorded,
            "droppedBeforeWindow": max(0, self._total_recorded - self._capacity),
            "lastAcceptedStage": self.last_accepted_stage(),
            "firstInvalidStage": self.first_invalid_stage(),
            "entries": self.ordered_entries(),
        }
        with open(path, "w", encoding="utf-8") as handle:
            json.dump(payload, handle, indent=1, sort_keys=False)
        sys.stderr.write(
            "[h8forge:blackbox] dumped {n} steps to {p} (reason={r}, "
            "lastAccepted={la}, firstInvalid={fi})\n".format(
                n=len(payload["entries"]), p=path, r=reason,
                la=payload["lastAcceptedStage"], fi=payload["firstInvalidStage"],
            )
        )
        return path


class GenerationAborted(RuntimeError):
    """Raised when a validation gate rejects geometry.

    3dmodel.md section 10: "Before any generator calls AssetDatabase.SaveAssets,
    PrefabUtility.SaveAsPrefabAsset, or writes a manifest, it must run validation.
    Failure aborts save."

    Carries the dump path so the caller reports evidence, not a bare message.
    """

    def __init__(self, message: str, dump_path: Optional[str] = None,
                 failures: Optional[list] = None) -> None:
        super().__init__(message)
        self.dump_path = dump_path
        self.failures = failures or []
