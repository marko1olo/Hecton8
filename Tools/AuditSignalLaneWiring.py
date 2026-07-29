#!/usr/bin/env python
"""Which SignalBus lanes have a writer but no reader (or a reader but no writer)? Static, with two controls.

WHY A STATIC SCAN IS THE ONLY WAY TO SEE THIS
---------------------------------------------
SignalBus consumption is strictly PULL-based. There is NO callback and NO subscription registration to
inspect at runtime: the per-lane dispatch table holds only Dispose/Flush/CopyTelemetry function pointers
(Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:308-333), so a registered lane proves nothing
about anybody reading it. A lane with no reader drains to nobody every frame. A lane with no writer hands
its reader an empty snapshot forever. Neither logs, neither throws, and the telemetry counters on a
never-pushed lane read exactly like the counters on a healthy idle lane. Nothing observable distinguishes
"wired" from "half-wired" at runtime, so the call sites are the only evidence there is.

WHAT COUNTS AS A SITE
---------------------
Member names come from the real API in SignalBusRuntime.cs, not from guessing - the bus is NOT
Publish/Subscribe. Writers: Push / TryPush / TryPushTracked / TryEnqueueBounded plus the job writer-handle
accessors. Readers: GetFrameSnapshot / GetSignals / GetFrameSnapshotArray / TryConsumeFrame / TryGetLatest /
TransformSnapshot / FilterSnapshot. Configure / EnsureInitialized / Dispose and the telemetry counters are
NEITHER: a lane that is configured, registered and flushed every frame and read by nobody is exactly the
failure this tool exists to expose, so letting Configure count as wiring would hide it.

THREE FILTERS THAT CHANGE THE ANSWER
------------------------------------
1. COMMENTS AND STRING LITERALS ARE NOT CODE. Measured on this tree: 212 textual occurrences of
   SignalBus<X>.Member outside the scan root resolve to 4 real call sites - the other 208 are inside string
   literals in tests that assert on source text. Inside GlobalSignals.LegacyFacade.cs, 93 occurrences are
   inside [Obsolete("... Use SignalBus<T>.TryConsumeFrame ...")] MESSAGE TEXT. A text-only count reports 186
   compile-dead call sites in that file; 93 exist. The report prints the phantom count per file so the
   number is auditable and not a claim you have to take on trust.
2. COMPILE-DEAD SITES. A member marked [Obsolete(..., true)] cannot be called at all - a call is CS0619, a
   compile ERROR - so its body is not a reader. Without this filter GlobalSignals.LegacyFacade.cs alone
   donates a fake TryConsumeFrame to dozens of lanes and orphans look healthy. Detected by attribute, not
   by filename, so a second legacy file cannot slip through.
3. USING-ALIASES. `using CoreCombatDamageSignal = ...Signals.CombatDamageSignal;` means
   SignalBus<CoreCombatDamageSignal> is the CombatDamageSignal lane. Unresolved, one lane splits into two
   halves and both look half-wired. Six alias names in this tree, declared in seven files, do exactly that.
4. GENERIC DISPATCH. `SignalBus<TSignal>.TryPushTracked` inside a generic helper has NO lane in its own
   text - the caller picks it. Textual attribution alone therefore drops the site, and dropping it is what
   made this tool lie about three save lanes. FILTER 4 below resolves those callers.

FILTER 4 - GENERIC DISPATCH, RESOLVED ONE CALL-GRAPH STEP
---------------------------------------------------------
For a site whose type argument is the enclosing method's own type PARAMETER, the lane is chosen by the
caller, so the caller is where it is read from. For each such helper this tool finds the declaration, the
position of the type parameter, and every call to it in the scan root, then takes the concrete lane from
either an explicit type argument - `RegisterLegacyLane<ImpactSignal>(...)` - or the DECLARED TYPE of the
argument passed in the type parameter's position - `TryPushSignalTrackedBestEffort(in status)` where `status`
is a local declared `SaveStatusSignal`.

Two rules keep this from replacing one lie with another:
  * THE CALLER'S COMPILE-DEAD STATE DECIDES, NOT THE HELPER'S. Measured here: all 34 callers of
    GlobalSignals.OpenSignalWriterForProducerPhase and all 116 callers of GlobalSignals.TryPushLegacy sit
    inside [Obsolete(..., true)] members. Crediting them would have handed ~40 lanes a producer that cannot
    be called at all (CS0619) and quietly moved orphans into LIVE. They are resolved, named, and credited to
    NOBODY.
  * TYPE INFERENCE IS SCOPED TO THE ENCLOSING MEMBER. An unscoped backwards search for `SomeType ident` finds
    a declaration in a DIFFERENT method: on the first attempt here, all five callers of
    CoreDeterminismSignals.TryConsumeLane resolved to InputSignal because
    CoreDeterminismSignals.cs:60 declares `InputSignal signal` in an unrelated publisher. Four of the five
    were wrong, which would have credited InputSignal with five readers and left StateCorrectionSignal,
    DesyncDetectedSignal, SyncFenceSignal and KccVelocitySignal orphaned. A parameter of the enclosing member
    wins; otherwise only locals declared INSIDE that member's own body are considered.
Lifecycle resolutions (Configure/EnsureInitialized through RegisterLegacyLane<T>) stay NEITHER, exactly like
a textual Configure: they can add lane MEMBERSHIP but can never move a lane out of an orphan class. Measured
on this tree they added zero lanes, since all 61 lanes so registered were already visible.

WHAT THE RESOLVER STILL CANNOT DO - the residue is REPORTED, never dropped
  * ONE STEP ONLY. A generic helper called from another generic helper forwards a type parameter, not a
    lane, so it stays unresolved. No recursion, no transitive walk.
  * A CALLER OUTSIDE THE SCAN ROOT IS INVISIBLE. SignalGhostFiltering.ApplyAliveMask<T> at
    SignalWardenRuntime.cs:1343 is public and has ZERO callers in the root, so its FilterSnapshot could be
    reading any entity-addressed lane from a test, an editor tool or another assembly. It remains unresolved,
    and because it is a CONSUME, PUBLISHED-NEVER-CONSUMED and DEAD AT BOTH ENDS remain UPPER BOUNDS. The
    report derives that sentence from which KINDS are still unresolved rather than asserting it.
  * `var`, a field, a property or any expression in the type parameter's argument position does not yield a
    declared type here and is reported as unresolved.
  * A call it cannot prove belongs to the same method - a qualifier that is not the declaring type, or an
    unqualified call in a file that does not declare that type - is counted and named, not guessed at.

MANDATORY POSITIVE CONTROLS
---------------------------
Every run resolves two lanes KNOWN to be fully wired, and asserts the publish AND the consume side at an
exact expected file:line. Both are wrapped in an owner facade (TryPublish / TryPushTracked / TryDequeue /
TryConsumeFrame helpers) on purpose: most lanes in this project are used that way, so the controls prove the
scan sees THROUGH a facade rather than only finding bare call sites. Two more controls cover FILTER 4: one
asserts SaveStatusSignal is credited a PUBLISH through the SaveManager generic helper and lands in a
NON-ORPHAN class, the other asserts the compile-dead containment rule above still rejects the legacy writer
callers. If any control fails, the classifier or the resolver is broken and every negative result in the run
is MEANINGLESS - the run says so in those words and exits non-zero, instead of letting a parse failure look
like a finding. This is not ceremony: the sibling tool Tools/AuditGuidReachability.py exists because a
text-only search silently under-reported, and its own first control choice was wrong in a way only a control
caught.

WHAT THIS TOOL CANNOT KNOW
--------------------------
A zero here is a QUESTION, not a verdict.
  * A write-only lane can be CORRECT BY DESIGN. Telemetry, black-box and crash-forensics lanes are
    published so an external reader or a post-mortem dump can pick them up; having no in-tree consumer is
    the point. This tool cannot tell those apart from an abandoned lane, and it does not try.
  * A read-only lane can be CORRECT. A lane written from a Burst job through OpenParallelWriter /
    ParallelWriter hands a writer handle to code this scan cannot follow, and a lane fed by an editor tool,
    a test harness, a mod through HectonEventBus, or generated code has a producer outside the scan root.
    Run --outside to see what lives beyond the root instead of guessing.
  * #if-disabled branches are counted as LIVE code. Scenes, prefabs, ScriptableObjects, other assemblies
    and reflection are not read at all.
  * The owner type/member printed against each site is best-effort text attribution for triage, not a parse.
So this tool says WHERE TO LOOK. It never says a lane is dead. The last time something in this repo was
declared dead on a static reachability walk, a tool deleted a node from the authored world graph.

USAGE, from the repo root:

    python -B Tools/AuditSignalLaneWiring.py              # full report
    python -B Tools/AuditSignalLaneWiring.py --check      # summary + drift only, non-zero exit on drift
    python -B Tools/AuditSignalLaneWiring.py --lane NAME  # every site for one lane, for triage
    python -B Tools/AuditSignalLaneWiring.py --outside    # producers/consumers OUTSIDE the scan root
"""
import bisect
import os
import re
import sys

SCAN_ROOT = os.path.join("Assets", "_Project", "Scripts")
OUTSIDE_ROOT = "Assets"

# Real member names, read out of Core/Signals/SignalBusRuntime.cs. Line numbers are the declarations.
PUBLISH_MEMBERS = {
    "Push": "SignalBusRuntime.cs:669",
    "TryPush": "SignalBusRuntime.cs:678",
    "TryPushTracked": "SignalBusRuntime.cs:722",
    "TryEnqueueBounded": "SignalBusRuntime.cs:733",
    # Writer-handle accessors: weaker evidence than a push. They hand a ring writer to a job and the real
    # enqueue happens where this scan cannot follow it. Counted as a producer, flagged in the lane lists.
    "OpenParallelWriter": "SignalBusRuntime.cs:480",
    "ParallelWriter": "SignalBusRuntime.cs:497",
    "RingParallelWriter": "SignalBusRuntime.cs:506",
    "ParallelWriterBudget": "SignalBusRuntime.cs:487",
}
WRITER_HANDLE_MEMBERS = frozenset(
    ("OpenParallelWriter", "ParallelWriter", "RingParallelWriter", "ParallelWriterBudget"))

CONSUME_MEMBERS = {
    "GetFrameSnapshot": "SignalBusRuntime.cs:773",
    "GetSignals": "SignalBusRuntime.cs:786",
    "GetFrameSnapshotArray": "SignalBusRuntime.cs:792",
    "TryConsumeFrame": "SignalBusRuntime.cs:806",
    "TryGetLatest": "SignalBusRuntime.cs:765",
    "TransformSnapshot": "SignalBusRuntime.cs:820",
    "FilterSnapshot": "SignalBusRuntime.cs:848",
}

# NEITHER. Lifecycle and telemetry touch a lane without moving a payload through it. That is the exact state
# this tool exists to expose, so they must never launder a lane into "wired".
LIFECYCLE_MEMBERS = frozenset((
    "Configure", "ConfigureCacheLineCritical", "EnsureInitialized", "Dispose", "FlushPostSimulation",
))
TELEMETRY_MEMBERS = frozenset((
    "LaneHash", "SnapshotCount", "SnapshotGeneration", "DroppedLastFlush", "LoadShedTotal",
    "CorruptedSignalTotal", "PeakQueuedLastFlush", "HasNativeStorage",
))

CLASS_LIVE = "LIVE (producer and consumer)"
CLASS_PUBLISH_ONLY = "PUBLISHED, NEVER CONSUMED"
CLASS_CONSUME_ONLY = "CONSUMED, NEVER PUBLISHED"
CLASS_DEAD = "DEAD AT BOTH ENDS"
CLASS_ORDER = (CLASS_LIVE, CLASS_PUBLISH_ONLY, CLASS_CONSUME_ONLY, CLASS_DEAD)

# Regression guard. These are THIS TOOL's own measurements on 2026-07-29, not somebody's recon: a baseline
# the tool cannot reproduce is an alarm that fires every run and therefore guards nothing. Update it
# deliberately, in the same commit as the wiring change that moved it, with the reason in the message.
# Moved 2026-07-29 by FILTER 4 (generic dispatch resolved one call-graph step). NOT a wiring change: no .cs
# file moved, the tool simply stopped dropping sites whose lane a caller supplies. Nine lanes were
# reclassified, all in the direction of MORE wiring - see BASELINE_PRE_RESOLVER for the exact deltas.
BASELINE = {
    "lanes": 296,
    CLASS_LIVE: 162,
    CLASS_PUBLISH_ONLY: 112,
    CLASS_CONSUME_ONLY: 7,
    CLASS_DEAD: 15,
    "compile_dead_sites": 93,
    "resolved_credited": 135,
    "resolved_rejected_dead": 150,
    "unresolved_sites": 1,
}

# The previous baseline, kept so the resolver's effect stays visible instead of being silently overwritten by
# the new numbers. Every difference below is a MEASUREMENT CHANGE in this tool, never a source change:
#   SaveStatusSignal     CONSUMED-NEVER-PUBLISHED -> LIVE            (SaveManager.cs:3542/:3593)
#   SaveLifecycleSignal  DEAD AT BOTH ENDS        -> PUBLISHED ONLY  (SaveManager.cs:3560/:3607)
#   SaveCompletedSignal  DEAD AT BOTH ENDS        -> PUBLISHED ONLY  (SaveManager.cs:3653)
#   InputSignal, StateCorrectionSignal, DesyncDetectedSignal, SyncFenceSignal
#                        PUBLISHED-NEVER-CONSUMED -> LIVE            (CoreDeterminismSignals.cs:161-169)
# HUDNotificationSignal and KccVelocitySignal also gained resolved sites but were already LIVE.
BASELINE_PRE_RESOLVER = {
    "lanes": 296,
    CLASS_LIVE: 157,
    CLASS_PUBLISH_ONLY: 114,
    CLASS_CONSUME_ONLY: 8,
    CLASS_DEAD: 17,
    "compile_dead_sites": 93,
    "resolved_credited": 0,
    "resolved_rejected_dead": 0,
    "unresolved_sites": 7,
}

# The 2026-07-29 hand recon, kept so the difference is on the record instead of being quietly overwritten.
# Reproduced exactly: the 296-lane universe. Contradicted with a mechanism: its 186 compile-dead sites are a
# TEXT count - 93 of those 186 are inside [Obsolete("...")] message strings, not call sites (see PHANTOMS in
# the report, which recomputes both numbers every run).
#
# FILTER 4 closed most of the class gap and EXPLAINED it. Before the resolver this tool read LIVE -2 /
# PUBLISHED-ONLY +1 / CONSUMED-ONLY +1 / DEAD +2 against the recon; the recon had resolved the three
# SaveManager lanes by hand and this tool had dropped them. With the resolver, CONSUMED-NEVER-PUBLISHED and
# DEAD AT BOTH ENDS now AGREE with the recon exactly. What remains is LIVE +3 / PUBLISHED-ONLY -3: this tool
# now sees three readers the recon did not, and they are nameable - StateCorrectionSignal,
# DesyncDetectedSignal and SyncFenceSignal are consumed through CoreDeterminismSignals.TryConsumeLane<T> at
# CoreDeterminismSignals.cs:163/:165/:167, a generic CONSUME a hand pass reading only concrete
# SignalBus<X> text cannot see. Confirm with --lane before acting; the residual is now explained, not blind.
RECON_2026_07_29 = {
    "lanes": 296,
    CLASS_LIVE: 159,
    CLASS_PUBLISH_ONLY: 115,
    CLASS_CONSUME_ONLY: 7,
    CLASS_DEAD: 15,
    "compile_dead_sites": 186,
}

# lane, file, expected publish line, expected consume line.
CONTROLS = (
    ("WorldChunkPhysicsBakedSignal",
     "Assets/_Project/Scripts/World/WorldChunkPhysicsBakedEvents.cs", 79, 92),
    ("TerrainChunkGeneratedSignal",
     "Assets/_Project/Scripts/TerrainChunkGeneratedEvents.cs", 45, 59),
)

# FILTER 4's control. SaveStatusSignal is the lane the missing resolver actually got wrong: it was reported
# CONSUMED-NEVER-PUBLISHED while SaveManager.PublishSaveStatus pushes it through a generic helper and
# ShinobuRespawnReconciliationRuntime reads it. Four legs, because each is a separate way for the resolver to
# break: no credited PUBLISH means the resolution never reached classify(); a `via` that does not name the
# helper means the credit came from somewhere other than FILTER 4; a `how` that is not a local declaration
# means the inference path degenerated to explicit type arguments only, which would silently drop this lane
# again; an orphan verdict means the merge in classify() dropped it.
#
# NO LINE NUMBER IS PINNED INSIDE SaveManager.cs, and that is deliberate rather than lazy. Measured while
# writing this: the file was rewritten at 14:43 during the audit itself and every caller below :3446 moved by
# +50 lines (the helper declaration went 4428 -> 4478). A control pinned to a line in a file under active
# concurrent edit fails on somebody else's unrelated insertion, which is exactly the "alarm that fires every
# run and therefore guards nothing" this file warns about for BASELINE. The consumer's line IS pinned, since
# ShinobuRespawnReconciliationRuntime.cs has not been touched since June and a stale pin there is a real
# signal. The caller lines actually found are PRINTED every run, so drift stays visible without crying wolf.
RESOLVER_CONTROL = {
    "lane": "SaveStatusSignal",
    "caller_file": "Assets/_Project/Scripts/SaveManager.cs",
    "helper_member": "TryPushSignalTrackedBestEffort",
    "how_contains": "local declared",
    "consumer": "Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs",
    "consumer_line": 299,
    "verdict": CLASS_LIVE,
}

# FILTER 4's containment control - the other half of the resolver's correctness. Every caller of this helper
# is inside an [Obsolete(..., true)] property, so a resolver that ignored the caller's compile-dead state
# would hand 34 lanes an uncallable producer and look like it had FIXED 34 orphans. The control asserts the
# rejection still happens AND that not one of those lanes was credited a producer by it.
DEAD_CONTAINMENT_CONTROL = {
    "helper_member": "OpenSignalWriterForProducerPhase",
    "min_rejected": 34,
}

SIGNALBUS_SITE = re.compile(r"\bSignalBus\s*<\s*([A-Za-z0-9_.]+)\s*>\s*\.\s*([A-Za-z0-9_]+)")
USING_ALIAS = re.compile(r"^\s*using\s+([A-Za-z0-9_]+)\s*=\s*([A-Za-z0-9_.:]+)\s*;", re.MULTILINE)
STRUCT_DECL = re.compile(r"\b(?:readonly\s+|partial\s+|unsafe\s+|ref\s+)*struct\s+([A-Za-z0-9_]+)")
OBSOLETE_ATTR = re.compile(r"\bObsolete(?:Attribute)?\s*\(")
TYPE_DECL = re.compile(r"\b(?:class|struct|interface|record)\s+([A-Za-z0-9_]+)")
MEMBER_HEADER = re.compile(r"([A-Za-z0-9_]+)\s*(?:<[^<>]*>)?\s*\(.*\)\s*(?:where\b[^{]*)?$", re.DOTALL)
MEMBER_ARROW = re.compile(r"([A-Za-z0-9_]+)\s*(?:<[^<>]*>)?\s*\([^()]*\)\s*=>")
PROPERTY_ARROW = re.compile(r"([A-Za-z0-9_]+)\s*=>\s*$")
PROPERTY_HEADER = re.compile(r"([A-Za-z0-9_]+)\s*$")
NOT_A_MEMBER_HEADER = re.compile(r"\b(?:namespace|enum)\b")
MODIFIER_OR_TYPE = re.compile(
    r"\b(?:public|private|protected|internal|static|override|virtual|sealed|abstract|readonly|extern"
    r"|partial|unsafe|ref|bool|int|uint|float|byte|short|long|ushort|ulong|double|string|void)\b")
NOT_A_MEMBER = frozenset((
    "if", "for", "foreach", "while", "do", "else", "switch", "case", "catch", "try", "finally", "using",
    "lock", "fixed", "unsafe", "checked", "unchecked", "return", "get", "set", "add", "remove", "new",
    "nameof", "typeof", "sizeof", "default", "await", "yield", "throw", "when", "select", "where",
))

# FILTER 4 helpers. DECL_MODIFIER is what separates `void Helper<TSignal>(...)` the DECLARATION from
# `Helper<TSignal>(x)` the forwarding CALL inside another generic method - without it the resolver would treat
# a forwarding call as the declaration and resolve nothing.
DECL_MODIFIER = re.compile(r"\b(?:private|public|internal|protected|static)\b")
PARAM_MODIFIER = frozenset(("in", "out", "ref", "params", "this", "scoped", "readonly"))
ARG_MODIFIER = re.compile(r"^(?:in|out|ref)\s+")
BARE_IDENT = re.compile(r"[A-Za-z_][A-Za-z0-9_]*\Z")
ATTRIBUTE_IN_PARAM = re.compile(r"\[[^\]]*\]")
# Identifiers that are never a lane type, so never a resolved declared type.
NOT_A_TYPE = frozenset((
    "var", "return", "new", "is", "as", "case", "if", "while", "default", "null", "true", "false", "void",
    "else", "do", "throw", "yield", "await", "out", "in", "ref", "using", "when", "and", "or", "not",
))


def blank_noncode(text):
    """Replace comment and string-literal content with spaces, preserving length and line numbers.

    Load-bearing, not hygiene. Without it a commented-out `// SignalBus<X>.TryConsumeFrame(...)` and the
    prose inside `[Obsolete("... Use SignalBus<T>.TryConsumeFrame ...", true)]` both read as live consumers.
    That single mistake is worth 93 phantom sites in one file here.
    """
    out = list(text)
    length = len(text)
    i = 0
    while i < length:
        ch = text[i]
        if ch == "/" and i + 1 < length and text[i + 1] == "/":
            end = text.find("\n", i)
            end = length if end < 0 else end
            for k in range(i, end):
                out[k] = " "
            i = end
            continue
        if ch == "/" and i + 1 < length and text[i + 1] == "*":
            end = text.find("*/", i + 2)
            end = length if end < 0 else end + 2
            for k in range(i, end):
                if out[k] != "\n":
                    out[k] = " "
            i = end
            continue
        if ch == '"' and text.startswith('"""', i):
            close = text.find('"""', i + 3)
            if close >= 0:
                for k in range(i, close + 3):
                    if out[k] != "\n":
                        out[k] = " "
                i = close + 3
                continue
        if ch == "@" and i + 1 < length and text[i + 1] == '"':
            end = i + 2
            while end < length:
                if text[end] == '"':
                    if end + 1 < length and text[end + 1] == '"':
                        end += 2
                        continue
                    end += 1
                    break
                end += 1
            for k in range(i, min(end, length)):
                if out[k] != "\n":
                    out[k] = " "
            i = end
            continue
        if ch in '"\'':
            quote = ch
            end = i + 1
            while end < length:
                if text[end] == "\\":
                    end += 2
                    continue
                if text[end] == quote or text[end] == "\n":
                    end += 1
                    break
                end += 1
            for k in range(i, min(end, length)):
                if out[k] != "\n":
                    out[k] = " "
            i = end
            continue
        i += 1
    return "".join(out)


def match_bracket(text, pos, opener, closer):
    """Index just past the closer matching the opener at pos, or -1 when unbalanced."""
    depth = 0
    length = len(text)
    while pos < length:
        if text[pos] == opener:
            depth += 1
        elif text[pos] == closer:
            depth -= 1
            if depth == 0:
                return pos + 1
        pos += 1
    return -1


def split_top_level(args):
    """Split an attribute argument list on commas at depth 0."""
    parts = []
    depth = 0
    start = 0
    for i, ch in enumerate(args):
        if ch in "([{<":
            depth += 1
        elif ch in ")]}>":
            depth -= 1
        elif ch == "," and depth == 0:
            parts.append(args[start:i])
            start = i + 1
    parts.append(args[start:])
    return parts


def skip_attribute_groups(text, pos):
    """Advance past whitespace and any further [Attribute] groups after an attribute's closing bracket."""
    length = len(text)
    while pos < length:
        while pos < length and text[pos].isspace():
            pos += 1
        if pos < length and text[pos] == "[":
            end = match_bracket(text, pos, "[", "]")
            if end < 0:
                return pos
            pos = end
            continue
        return pos
    return pos


def member_span_after(text, pos):
    """(start, end) of the member declared at pos: block body, expression body, or bare declaration."""
    length = len(text)
    depth = 0
    while pos < length:
        ch = text[pos]
        if ch in "([":
            depth += 1
        elif ch in ")]":
            depth = max(0, depth - 1)
        elif depth == 0:
            if ch == "{":
                end = match_bracket(text, pos, "{", "}")
                return None if end < 0 else (pos, end)
            if ch == ";":
                return (pos, pos + 1)
            if ch == "=" and pos + 1 < length and text[pos + 1] == ">":
                scan = pos + 2
                inner = 0
                while scan < length:
                    if text[scan] in "([{":
                        inner += 1
                    elif text[scan] in ")]}":
                        inner -= 1
                    elif text[scan] == ";" and inner <= 0:
                        return (pos, scan + 1)
                    scan += 1
                return None
        pos += 1
    return None


def compile_dead_spans(code):
    """Spans of members marked [Obsolete(..., true)]. A call to one is CS0619, so the body is not code."""
    spans = []
    failures = 0
    for match in OBSOLETE_ATTR.finditer(code):
        open_paren = match.end() - 1
        args_end = match_bracket(code, open_paren, "(", ")")
        if args_end < 0:
            failures += 1
            continue
        args = split_top_level(code[open_paren + 1:args_end - 1])
        if len(args) < 2:
            continue  # [Obsolete] / [Obsolete("msg")] - a warning only, so the call site is REAL
        if not any(re.fullmatch(r"\s*(?:error\s*:\s*)?true\s*", arg) for arg in args[1:]):
            continue  # error:false - still compiles, still a real call site
        bracket_end = code.find("]", args_end - 1)
        if bracket_end < 0:
            failures += 1
            continue
        span = member_span_after(code, skip_attribute_groups(code, bracket_end + 1))
        if span is None:
            failures += 1
            continue
        spans.append((match.start(), span[1]))

    spans.sort()
    merged = []
    for start, end in spans:
        if merged and start <= merged[-1][1]:
            merged[-1] = (merged[-1][0], max(merged[-1][1], end))
        else:
            merged.append((start, end))
    return merged, failures


def brace_scopes(code):
    """(body_start, body_end, header) for every brace body, header = the declaration text preceding it."""
    scopes = []
    stack = []
    boundary = 0
    for i, ch in enumerate(code):
        if ch == "{":
            stack.append((i, code[boundary:i]))
            boundary = i + 1
        elif ch == "}":
            if stack:
                start, header = stack.pop()
                scopes.append((start, i, header))
            boundary = i + 1
        elif ch == ";":
            boundary = i + 1
    return scopes


def header_member_name(header):
    """Member name from a declaration header, or None when the header is not a member declaration."""
    stripped = header.strip()
    if not stripped or stripped.endswith(("=", "=>", ",")):
        return None
    if TYPE_DECL.search(stripped) or NOT_A_MEMBER_HEADER.search(stripped):
        return None
    match = MEMBER_HEADER.search(stripped)
    if match and match.group(1) not in NOT_A_MEMBER:
        return match.group(1)
    if "(" in stripped or "=" in stripped:
        return None
    if not MODIFIER_OR_TYPE.search(stripped):
        return None  # a bare block, a label, an accessor - not a property declaration
    match = PROPERTY_HEADER.search(stripped)
    if match and match.group(1) not in NOT_A_MEMBER:
        return match.group(1)  # property or indexer body
    return None


def attribute_site(code, offset, scopes, boundaries):
    """(owner_type, owner_member) for the site at offset. Best-effort, for triage only."""
    owner_type = None
    owner_member = None
    best_type_start = -1
    best_member_start = -1
    for start, end, header in scopes:
        if not (start < offset < end):
            continue
        type_match = TYPE_DECL.search(header)
        if type_match and start > best_type_start:
            best_type_start, owner_type = start, type_match.group(1)
        name = header_member_name(header)
        if name and start > best_member_start:
            best_member_start, owner_member = start, name

    if owner_member is None:
        # Expression-bodied member: no brace body, so the header is in the current statement fragment.
        index = bisect.bisect_left(boundaries, offset) - 1
        fragment = code[boundaries[index] + 1:offset] if index >= 0 else code[:offset]
        arrow = None
        for arrow in MEMBER_ARROW.finditer(fragment):
            pass
        if arrow and arrow.group(1) not in NOT_A_MEMBER:
            owner_member = arrow.group(1)
        else:
            arrow = PROPERTY_ARROW.search(fragment)  # public static int Foo => SignalBus<T>.SnapshotCount;
            if arrow and arrow.group(1) not in NOT_A_MEMBER:
                owner_member = arrow.group(1)
    return owner_type, owner_member


class Site(object):
    __slots__ = ("path", "line", "lane", "raw_lane", "member", "kind", "owner_type", "owner_member", "dead",
                 "via", "via_line", "how")

    def __init__(self, path, line, lane, raw_lane, member, kind, owner_type, owner_member, dead,
                 via=None, via_line=0, how=None):
        self.path = path
        self.line = line
        self.lane = lane
        self.raw_lane = raw_lane
        self.member = member
        self.kind = kind
        self.owner_type = owner_type
        self.owner_member = owner_member
        self.dead = dead
        # FILTER 4 only. `via` is the generic helper that carried this lane, `via_line` the SignalBus<T> line
        # inside it, `how` the evidence the lane identity came from. A resolved site is anchored at the CALLER
        # because that is where the lane is chosen; without via/how a reader could not audit the inference.
        self.via = via
        self.via_line = via_line
        self.how = how

    @property
    def resolved(self):
        return self.via is not None

    @property
    def owner(self):
        return "%s.%s" % (self.owner_type or "?", self.owner_member or "?")

    @property
    def where(self):
        return "%s:%d" % (self.path, self.line)


class FileScan(object):
    __slots__ = ("sites", "declared", "aliases", "parse_failures", "phantoms")

    def __init__(self):
        self.sites = []
        self.declared = ()
        self.aliases = {}
        self.parse_failures = 0
        self.phantoms = 0


def classify_member(member):
    if member in PUBLISH_MEMBERS:
        return "PUBLISH"
    if member in CONSUME_MEMBERS:
        return "CONSUME"
    if member in LIFECYCLE_MEMBERS:
        return "LIFECYCLE"
    if member in TELEMETRY_MEMBERS:
        return "TELEMETRY"
    return "UNKNOWN"


def scan_file(path):
    result = FileScan()
    try:
        raw = open(path, encoding="utf-8", errors="replace").read()
    except OSError:
        return result

    if "SignalBus" not in raw:
        result.declared = STRUCT_DECL.findall(raw)  # lane payloads are declared in files that never use them
        return result

    code = blank_noncode(raw)
    result.declared = STRUCT_DECL.findall(code)
    result.phantoms = (len(SIGNALBUS_SITE.findall(raw)) - len(SIGNALBUS_SITE.findall(code)))
    for match in USING_ALIAS.finditer(code):
        result.aliases[match.group(1)] = match.group(2).replace("global::", "").split(".")[-1]

    dead_spans, result.parse_failures = compile_dead_spans(code)
    dead_starts = [span[0] for span in dead_spans]
    scopes = brace_scopes(code)
    boundaries = [i for i, ch in enumerate(code) if ch in ";{}"]
    newlines = [i for i, ch in enumerate(code) if ch == "\n"]
    rel = path.replace(os.sep, "/")

    for match in SIGNALBUS_SITE.finditer(code):
        offset = match.start()
        index = bisect.bisect_right(dead_starts, offset) - 1
        dead = index >= 0 and offset < dead_spans[index][1]
        raw_lane = match.group(1).split(".")[-1]
        owner_type, owner_member = attribute_site(code, offset, scopes, boundaries)
        result.sites.append(Site(
            rel, bisect.bisect_right(newlines, offset) + 1,
            result.aliases.get(raw_lane, raw_lane), raw_lane, match.group(2),
            classify_member(match.group(2)), owner_type, owner_member, dead))
    return result


def scan_tree(root, skip_prefix=None):
    sites = []
    declared_structs = set()
    aliases = {}
    parse_failures = {}
    phantoms = {}
    files = 0
    for directory, subdirs, filenames in os.walk(root):
        subdirs[:] = [name for name in subdirs if name not in ("Library", "Temp", "obj", ".git")]
        posix_dir = directory.replace(os.sep, "/")
        if skip_prefix and posix_dir.startswith(skip_prefix):
            continue
        for filename in sorted(filenames):
            if not filename.endswith(".cs"):
                continue
            files += 1
            path = os.path.join(directory, filename)
            scan = scan_file(path)
            sites += scan.sites
            declared_structs.update(scan.declared)
            rel = path.replace(os.sep, "/")
            if scan.parse_failures:
                parse_failures[rel] = scan.parse_failures
            if scan.phantoms:
                phantoms[rel] = scan.phantoms
            for name, target in scan.aliases.items():
                aliases.setdefault(name, (target, rel))
    return sites, declared_structs, aliases, parse_failures, phantoms, files


class GenericHelper(object):
    """One generic method that reaches SignalBus<its own type parameter>, plus every caller resolution."""

    __slots__ = ("path", "member", "owner_type", "tparam", "tpindex", "argindex", "decl_line", "decl_start",
                 "bus_sites", "resolved", "rejected", "note")

    def __init__(self, path, member, owner_type, tparam):
        self.path = path
        self.member = member
        self.owner_type = owner_type
        self.tparam = tparam
        self.tpindex = 0
        self.argindex = None
        self.decl_line = 0
        self.decl_start = -1
        self.bus_sites = []
        self.resolved = []
        self.rejected = []
        self.note = None

    @property
    def label(self):
        return "%s.%s<%s>" % (self.owner_type or "?", self.member, self.tparam)

    @property
    def credited(self):
        return [site for site in self.resolved if not site.dead]

    @property
    def dead_credited(self):
        return [site for site in self.resolved if site.dead]


def find_generic_decl(code, helper):
    """Fill helper's declaration facts from code, or leave a note saying why it could not be read.

    The type parameter POSITION matters, not just its name: `Helper<TKey, TSignal>` resolved from a caller's
    first type argument would name the wrong lane. So does the parameter position - the lane is inferred from
    the argument in the slot whose declared type IS the type parameter, and nothing else.
    """
    for match in re.finditer(r"\b" + re.escape(helper.member) + r"\s*<([^<>]*)>\s*\(", code):
        params = [part.strip() for part in split_top_level(match.group(1))]
        if helper.tparam not in params:
            continue  # a CALL that passes a concrete type, not the declaration
        fragment_start = max(code.rfind(";", 0, match.start()), code.rfind("{", 0, match.start()),
                             code.rfind("}", 0, match.start()))
        if not DECL_MODIFIER.search(code[fragment_start + 1:match.start()]):
            continue  # a forwarding call inside another generic method: `Helper<TSignal>(x)`
        open_paren = match.end() - 1
        close_paren = match_bracket(code, open_paren, "(", ")")
        if close_paren < 0:
            helper.note = "parameter list of the declaration is unbalanced"
            return False
        helper.tpindex = params.index(helper.tparam)
        helper.decl_start = match.start()
        helper.decl_line = code.count("\n", 0, match.start()) + 1
        inner = code[open_paren + 1:close_paren - 1]
        if inner.strip():
            for index, param in enumerate(split_top_level(inner)):
                tokens = [token for token in ATTRIBUTE_IN_PARAM.sub("", param).split("=")[0].split()
                          if token not in PARAM_MODIFIER]
                if len(tokens) >= 2 and tokens[-2] == helper.tparam:
                    helper.argindex = index
        return True
    helper.note = "no declaration of %s<%s> found in this file" % (helper.member, helper.tparam)
    return False


def find_calls(code, member):
    """(offset, type_args_or_None, arg_open_or_negative) for every use of member. -1 unbalanced, -2 not a call.

    A method GROUP - `Register(Helper)` or a delegate assignment - is returned with -2 rather than skipped: it
    is a real way for a lane to be chosen where this scan cannot see it, so it must be reported, not dropped.
    """
    calls = []
    length = len(code)
    for match in re.finditer(r"\b" + re.escape(member) + r"\b", code):
        pos = match.end()
        while pos < length and code[pos].isspace():
            pos += 1
        type_args = None
        if pos < length and code[pos] == "<":
            end = match_bracket(code, pos, "<", ">")
            if end < 0:
                calls.append((match.start(), None, -1))
                continue
            type_args = code[pos + 1:end - 1]
            pos = end
            while pos < length and code[pos].isspace():
                pos += 1
        if pos < length and code[pos] == "(":
            calls.append((match.start(), type_args, pos))
        else:
            calls.append((match.start(), type_args, -2))
    return calls


def call_qualifier(code, start):
    """The last segment of a call's qualifier, or None when the call is unqualified."""
    index = start - 1
    while index >= 0 and code[index].isspace():
        index -= 1
    if index < 0 or code[index] != ".":
        return None
    scan = index - 1
    while scan >= 0 and (code[scan].isalnum() or code[scan] in "_.:"):
        scan -= 1
    qualifier = code[scan + 1:index].replace("global::", "").strip(".").split(".")[-1]
    return qualifier or "?"  # `foo?.Member(...)` - a qualifier this scan cannot attribute


def enclosing_member(code, offset, scopes):
    """(header text, body start) of the innermost MEMBER declaration containing offset.

    Falls back to the current statement fragment so an expression-bodied member is still found:
    `public static bool TryDequeueInput(out InputSignal signal) => TryConsumeLane(out signal);` has no brace
    body, and that is the exact form that supplies four of the five determinism lanes.
    """
    best = None
    for start, end, header in scopes:
        if start < offset < end and header_member_name(header) and (best is None or start > best[1]):
            best = (header, start)
    if best is not None:
        return best
    boundary = max(code.rfind(";", 0, offset), code.rfind("{", 0, offset), code.rfind("}", 0, offset))
    return code[boundary + 1:offset], boundary + 1


def parameter_type(header, ident):
    """Declared type of ident in any parameter list in header, or None."""
    open_paren = header.find("(")
    while open_paren >= 0:
        close_paren = match_bracket(header, open_paren, "(", ")")
        if close_paren < 0:
            return None
        inner = header[open_paren + 1:close_paren - 1]
        if inner.strip():
            for param in split_top_level(inner):
                tokens = [token for token in ATTRIBUTE_IN_PARAM.sub("", param).split("=")[0].split()
                          if token not in PARAM_MODIFIER]
                if len(tokens) >= 2 and tokens[-1] == ident:
                    name = tokens[-2].replace("global::", "").split(".")[-1]
                    return None if name in NOT_A_TYPE else name
        open_paren = header.find("(", close_paren)
    return None


def infer_argument_type(code, ident, offset, scopes):
    """(declared type, evidence) for ident as seen at offset. SCOPED TO THE ENCLOSING MEMBER, deliberately.

    An unscoped backwards search reads a declaration out of a neighbouring method. Measured: it resolved all
    five callers of CoreDeterminismSignals.TryConsumeLane to InputSignal because line 60 of that file declares
    `InputSignal signal` in an unrelated publisher, so four lanes would have been credited to the wrong one.
    """
    header, body = enclosing_member(code, offset, scopes)
    found = parameter_type(header, ident)
    if found:
        return found, "parameter of the enclosing member"
    best = None
    pattern = re.compile(r"(?<![.\w])([A-Za-z_][A-Za-z0-9_.]*)\s+" + re.escape(ident) + r"\s*(?==[^=]|;)")
    for match in pattern.finditer(code[body:offset]):
        name = match.group(1).replace("global::", "").split(".")[-1]
        if name not in NOT_A_TYPE:
            best = (name, body + match.start())
    if best is not None:
        return best[0], "local declared at line %d" % (code.count("\n", 0, best[1]) + 1)
    return None, "no declaration of `%s` inside the enclosing member" % ident


def resolve_generic_dispatch(generic_sites, declared_structs, root):
    """Resolve SignalBus<TypeParameter> sites by reading the concrete lane out of each caller. ONE step.

    Returns (helpers, resolved sites, still-unresolved sites). A resolved site is anchored at the CALLER and
    carries via/how so the inference is auditable. A resolution whose CALLER is compile-dead is returned with
    dead=True and is never credited to a lane - see FILTER 4 in the module docstring for why that rule is the
    difference between resolving three save lanes and inventing forty producers that cannot compile.
    """
    helpers = {}
    orphan_sites = []
    for site in generic_sites:
        if not site.owner_member:
            orphan_sites.append(site)  # enclosing member not identified: cannot look for callers
            continue
        key = (site.path, site.owner_type, site.owner_member, site.raw_lane)
        helper = helpers.get(key)
        if helper is None:
            helper = helpers[key] = GenericHelper(site.path, site.owner_member, site.owner_type,
                                                  site.raw_lane)
        helper.bus_sites.append(site)

    by_path = {}
    for helper in helpers.values():
        by_path.setdefault(helper.path, []).append(helper)
    for path, group in by_path.items():
        try:
            code = blank_noncode(open(path, encoding="utf-8", errors="replace").read())
        except OSError:
            for helper in group:
                helper.note = "declaring file could not be read"
            continue
        for helper in group:
            find_generic_decl(code, helper)

    wanted = sorted({helper.member for helper in helpers.values() if helper.decl_start >= 0})
    if wanted:
        _scan_callers(root, wanted, helpers, declared_structs)

    resolved = []
    still_unresolved = list(orphan_sites)
    for helper in helpers.values():
        if helper.resolved:
            resolved += helper.resolved
        else:
            still_unresolved += helper.bus_sites
    return helpers, resolved, still_unresolved


def _scan_callers(root, wanted, helpers, declared_structs):
    """Walk root once and attribute every call of a wanted helper name to a concrete lane, or to a reason."""
    by_member = {}
    for helper in helpers.values():
        if helper.decl_start >= 0:
            by_member.setdefault(helper.member, []).append(helper)

    for directory, subdirs, filenames in os.walk(root):
        subdirs[:] = [name for name in subdirs if name not in ("Library", "Temp", "obj", ".git")]
        for filename in sorted(filenames):
            if not filename.endswith(".cs"):
                continue
            path = os.path.join(directory, filename)
            try:
                raw = open(path, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            if not any(member in raw for member in wanted):
                continue  # cheap substring gate: blank_noncode is the expensive part, skip it for 3k files
            rel = path.replace(os.sep, "/")
            code = blank_noncode(raw)
            dead_spans, _ = compile_dead_spans(code)
            dead_starts = [span[0] for span in dead_spans]
            scopes = brace_scopes(code)
            boundaries = [i for i, ch in enumerate(code) if ch in ";{}"]
            newlines = [i for i, ch in enumerate(code) if ch == "\n"]
            aliases = {}
            for match in USING_ALIAS.finditer(code):
                aliases[match.group(1)] = match.group(2).replace("global::", "").split(".")[-1]
            for member, group in by_member.items():
                if member not in raw:
                    continue
                calls = find_calls(code, member)
                for helper in group:
                    _attribute_calls(helper, rel, code, calls, dead_starts, dead_spans, scopes, boundaries,
                                     newlines, aliases, declared_structs)


def _attribute_calls(helper, rel, code, calls, dead_starts, dead_spans, scopes, boundaries, newlines,
                     aliases, declared_structs):
    declares_owner = bool(helper.owner_type) and bool(
        re.search(r"\b(?:class|struct|interface|record)\s+" + re.escape(helper.owner_type) + r"\b", code))
    for offset, type_args, arg_open in calls:
        if rel == helper.path and offset == helper.decl_start:
            continue
        line = bisect.bisect_right(newlines, offset) + 1
        where = "%s:%d" % (rel, line)
        index = bisect.bisect_right(dead_starts, offset) - 1
        caller_dead = index >= 0 and offset < dead_spans[index][1]

        qualifier = call_qualifier(code, offset)
        if qualifier is not None:
            if qualifier != helper.owner_type:
                helper.rejected.append((where, None, "qualified with `%s`, not %s - cannot prove it is the "
                                                     "same method" % (qualifier, helper.owner_type)))
                continue
        elif not declares_owner:
            helper.rejected.append((where, None, "unqualified call in a file that does not declare %s"
                                    % helper.owner_type))
            continue

        if arg_open == -1:
            helper.rejected.append((where, None, "unbalanced type argument list"))
            continue
        if arg_open == -2:
            helper.rejected.append((where, None, "not a call - method group or name reference, so the lane "
                                                 "is chosen where this scan cannot see it"))
            continue

        how = None
        raw_type = None
        if type_args is not None:
            args = [part.strip() for part in split_top_level(type_args)]
            if helper.tpindex >= len(args):
                helper.rejected.append((where, None, "explicit type argument list has no slot %d"
                                        % helper.tpindex))
                continue
            raw_type = args[helper.tpindex].replace("global::", "").split(".")[-1]
            how = "explicit type argument"
        elif helper.argindex is None:
            helper.rejected.append((where, None, "no explicit type argument and %s appears in no parameter, "
                                                 "so the caller supplies no readable lane" % helper.tparam))
            continue
        else:
            close_paren = match_bracket(code, arg_open, "(", ")")
            if close_paren < 0:
                helper.rejected.append((where, None, "unbalanced argument list"))
                continue
            inner = code[arg_open + 1:close_paren - 1]
            args = [part.strip() for part in split_top_level(inner)] if inner.strip() else []
            if helper.argindex >= len(args):
                helper.rejected.append((where, None, "argument list has no slot %d, so this is a different "
                                                     "overload" % helper.argindex))
                continue
            ident = ARG_MODIFIER.sub("", args[helper.argindex]).strip()
            if not BARE_IDENT.match(ident):
                helper.rejected.append((where, None, "argument in the lane slot is an expression, not a "
                                                     "declared variable"))
                continue
            raw_type, how = infer_argument_type(code, ident, offset, scopes)
            if raw_type is None:
                helper.rejected.append((where, None, how))
                continue

        lane = aliases.get(raw_type, raw_type)
        if lane not in declared_structs:
            helper.rejected.append((where, lane, "`%s` is not a signal struct declared in the scan root - a "
                                                 "forwarded type parameter or an out-of-root type" % lane))
            continue

        owner_type, owner_member = attribute_site(code, offset, scopes, boundaries)
        for bus in helper.bus_sites:
            helper.resolved.append(Site(
                rel, line, lane, raw_type, bus.member, bus.kind, owner_type, owner_member,
                caller_dead or bus.dead, via=helper.label, via_line=bus.line, how=how))


def print_resolved(helpers):
    """Name every generic resolution and every rejection. A count with no file:line is not evidence."""
    if not helpers:
        return
    credited = sum(len(helper.credited) for helper in helpers.values())
    dead = sum(len(helper.dead_credited) for helper in helpers.values())
    rejected = sum(len(helper.rejected) for helper in helpers.values())
    print("FILTER 4 - GENERIC DISPATCH: SignalBus<TypeParameter> resolved from its CALLERS, one step")
    print("  %d generic helper(s); %d resolution(s) credited, %d rejected as compile-dead, %d not resolvable"
          % (len(helpers), credited, dead, rejected))
    for key in sorted(helpers, key=lambda k: (-len(helpers[k].credited), k)):
        helper = helpers[key]
        members = sorted({site.member for site in helper.bus_sites})
        kinds = sorted({site.kind for site in helper.bus_sites})
        print("    %s  %s:%d  ->  SignalBus<%s>.%s  (%s)"
              % (helper.label, os.path.basename(helper.path), helper.decl_line, helper.tparam,
                 "/".join(members), "/".join(kinds)))
        if helper.note:
            print("        *** NOT RESOLVED: %s" % helper.note)
        by_lane = {}
        for site in helper.credited:
            by_lane.setdefault(site.lane, []).append(site)
        for lane in sorted(by_lane)[:8]:
            group = by_lane[lane]
            first = group[0]
            print("        CREDIT %-9s %-34s %s  (%s)"
                  % (first.kind, lane, ", ".join("%d" % site.line for site in group[:4]), first.how))
        if len(by_lane) > 8:
            print("        CREDIT ... and %d more lane(s) from this helper, all in %s"
                  % (len(by_lane) - 8, os.path.basename(helper.path)))
        if helper.dead_credited:
            dead_lanes = sorted({site.lane for site in helper.dead_credited})
            print("        REJECTED, CREDITED TO NOBODY: %d caller(s) across %d lane(s) are inside "
                  "[Obsolete(...,true)]" % (len(helper.dead_credited), len(dead_lanes)))
            print("            calling one is CS0619, so this helper gives those lanes NO producer/consumer:")
            print("            %s%s" % (", ".join(dead_lanes[:6]),
                                        " (+%d more)" % (len(dead_lanes) - 6) if len(dead_lanes) > 6 else ""))
        reasons = {}
        for where, _, reason in helper.rejected:
            reasons.setdefault(reason, []).append(where)
        for reason in sorted(reasons, key=lambda r: (-len(reasons[r]), r)):
            print("        UNRESOLVED %d: %s" % (len(reasons[reason]), reason))
            print("            e.g. %s" % ", ".join(reasons[reason][:3]))
    print()


def print_unresolved(unresolved):
    """Name the sites STILL unresolved after FILTER 4, and derive which orphan classes remain UPPER BOUNDS.

    This section exists because its absence produced three wrong verdicts, and the upper-bound sentence is
    computed from the KINDS still unresolved rather than asserted: an unresolved PUBLISH can only inflate
    CONSUMED-NEVER-PUBLISHED and DEAD, an unresolved CONSUME can only inflate PUBLISHED-NEVER-CONSUMED and
    DEAD. Saying "everything is an upper bound" when only one CONSUME site remains would be its own small lie.
    """
    if not unresolved:
        print("UNRESOLVED LANE ARGUMENTS - none. Every SignalBus site resolved to a declared lane, so no")
        print("  orphan count below is inflated by generic dispatch. The other caveats still apply.")
        print()
        return

    by_arg = {}
    for site in unresolved:
        by_arg.setdefault(site.raw_lane, []).append(site)
    kinds = {site.kind for site in unresolved}

    print("UNRESOLVED LANE ARGUMENTS - generic dispatch that survived FILTER 4")
    print("  %d site(s) across %d distinct type argument(s). Each is a real SignalBus call whose lane a caller"
          % (len(unresolved), len(by_arg)))
    print("  chooses, and FILTER 4 could not read that caller.")
    inflated = []
    if "PUBLISH" in kinds:
        inflated += [CLASS_CONSUME_ONLY, CLASS_DEAD]
    if "CONSUME" in kinds:
        inflated += [CLASS_PUBLISH_ONLY, CLASS_DEAD]
    inflated = sorted(set(inflated))
    if inflated:
        print("  UPPER BOUNDS, derived from the kinds above: %s." % ", ".join(inflated))
        print("  The other classes are NOT inflated by generic dispatch: no unresolved site of the opposite")
        print("  kind remains that could move a lane into them.")
    else:
        print("  None of these is a PUBLISH or a CONSUME (lifecycle/telemetry are NEITHER), so no orphan")
        print("  count below is inflated by them.")
    for arg in sorted(by_arg, key=lambda a: (-len(by_arg[a]), a)):
        group = by_arg[arg]
        print("    %-24s %d site(s)" % (arg, len(group)))
        for site in group[:3]:
            print("        %s:%d  %s  (%s)  in %s" % (site.path, site.line, site.member, site.kind,
                                                      site.owner))
        if len(group) > 3:
            print("        ... and %d more" % (len(group) - 3))
    print("  To resolve one by hand, read the helper and find its callers. FILTER 4 above already did that")
    print("  wherever the callers are in this scan root; what is left needs a caller it cannot see.")
    print()


def classify(sites, declared_structs, resolved=()):
    """Group sites per lane. A lane exists when its type argument resolves to a struct declared in the tree.

    Sites whose type argument does NOT resolve are returned separately rather than dropped. That distinction
    is load-bearing: this function used to `continue` past them with a comment acknowledging they were generic
    parameters, which made every orphan count a silent under-report.

    MEASURED CONSEQUENCE, 2026-07-29. SaveManager.cs:4433 publishes through
    `SignalBus<TSignal>.TryPushTracked` inside a generic helper, reached from
    TryPushSignalTrackedBestEffort at :3446/:3542/:3560/:3593/:3607/:3653. `TSignal` is not a declared lane,
    so all of those pushes were invisible and three lanes were misclassified:
      SaveStatusSignal    reported CONSUMED-NEVER-PUBLISHED - it has a real publisher and a real consumer
                          (ShinobuRespawnReconciliationRuntime.cs:299)
      SaveLifecycleSignal reported DEAD AT BOTH ENDS - actually published, never consumed
      SaveCompletedSignal reported DEAD AT BOTH ENDS - actually published, never consumed
    FILTER 4 now resolves exactly those callers and all three lanes are classified from real evidence. Four
    more moved the other way: InputSignal, StateCorrectionSignal, DesyncDetectedSignal and SyncFenceSignal were
    PUBLISHED-NEVER-CONSUMED because their reader is a generic CONSUME
    (CoreDeterminismSignals.TryConsumeLane<T>, CoreDeterminismSignals.cs:296).

    `resolved` carries FILTER 4's output. Two rules here, both deliberate:
      * ONLY CREDITED (non-dead) resolutions enter a lane record. A resolution whose caller sits in an
        [Obsolete(..., true)] member is dropped at this boundary, so it can never move a verdict. 150 of them
        exist in this tree and crediting them would have "fixed" about forty orphans with uncallable code.
      * The lane UNIVERSE is still defined by textual sites plus credited resolutions. Measured here that adds
        no lane at all - every one of the 61 lanes registered through RegisterLegacyLane<T> was already
        visible - but if it ever does, a lane appearing only through a resolved lifecycle call is a lane that
        registers and drains to nobody, which is a finding and not noise.
    """
    lanes = {}
    unresolved = []
    for site in list(sites) + [site for site in resolved if not site.dead]:
        if site.lane not in declared_structs:
            # A generic type parameter (T, TSignal) or a type declared outside the scan. Recorded, not dropped.
            unresolved.append(site)
            continue
        record = lanes.setdefault(site.lane, {"PUBLISH": [], "CONSUME": [], "OTHER": [], "dead": []})
        if site.dead:
            record["dead"].append(site)
        elif site.kind in ("PUBLISH", "CONSUME"):
            record[site.kind].append(site)
        else:
            record["OTHER"].append(site)

    verdicts = {}
    for lane, record in lanes.items():
        if record["PUBLISH"] and record["CONSUME"]:
            verdicts[lane] = CLASS_LIVE
        elif record["PUBLISH"]:
            verdicts[lane] = CLASS_PUBLISH_ONLY
        elif record["CONSUME"]:
            verdicts[lane] = CLASS_CONSUME_ONLY
        else:
            verdicts[lane] = CLASS_DEAD
    return lanes, verdicts, unresolved


def print_controls(lanes, verdicts, helpers):
    print("POSITIVE CONTROLS - two lanes known to be fully wired, both through an owner facade")
    passed = 0
    for lane, path, publish_line, consume_line in CONTROLS:
        record = lanes.get(lane)
        publish_ok = bool(record) and any(
            site.path == path and site.line == publish_line for site in record["PUBLISH"])
        consume_ok = bool(record) and any(
            site.path == path and site.line == consume_line for site in record["CONSUME"])
        passed += 1 if publish_ok and consume_ok else 0
        print("  %-30s publish %s:%-4d %-8s consume %s:%-4d %s"
              % (lane, os.path.basename(path), publish_line, "FOUND" if publish_ok else "MISSING",
                 os.path.basename(path), consume_line, "FOUND" if consume_ok else "MISSING"))

    resolver_ok, resolver_lines = check_resolver_control(lanes, verdicts)
    containment_ok, containment_lines = check_dead_containment_control(helpers)
    passed += (1 if resolver_ok else 0) + (1 if containment_ok else 0)
    total = len(CONTROLS) + 2
    print("  FILTER 4 CONTROL - the lane the missing resolver actually got wrong")
    for line in resolver_lines:
        print("    %s" % line)
    print("  FILTER 4 CONTAINMENT CONTROL - a compile-dead caller must credit NOBODY")
    for line in containment_lines:
        print("    %s" % line)

    print("  CONTROLS: %d/%d PASS" % (passed, total))
    if not resolver_ok or not containment_ok:
        print("  *** THE GENERIC RESOLVER IS BROKEN, so ITS NUMBERS ARE UNTRUSTWORTHY. Do not read the class")
        print("  *** counts below as resolved: they are back to being UPPER BOUNDS at best, and if the")
        print("  *** containment control is the one that failed they are worse than that - compile-dead")
        print("  *** callers may have been credited as producers. Fix the tool, not the code.")
    if passed != total:
        print("  *** THE METHOD IS BROKEN. Every negative result below is MEANINGLESS: a lane reported with")
        print("  *** no consumer may simply be a lane this run failed to parse. Fix the tool, not the code.")
    print()
    return passed == total


def check_resolver_control(lanes, verdicts):
    """SaveStatusSignal must be credited a PUBLISH through the SaveManager generic helper, and be non-orphan."""
    control = RESOLVER_CONTROL
    lane = control["lane"]
    record = lanes.get(lane)
    hits = [] if not record else [
        site for site in record["PUBLISH"]
        if site.resolved and site.path == control["caller_file"]
        and control["helper_member"] in (site.via or "")]
    inferred = [site for site in hits if control["how_contains"] in (site.how or "")]
    verdict = verdicts.get(lane, "NO SUCH LANE")
    non_orphan = verdict == control["verdict"]
    consume_ok = bool(record) and any(
        site.path == control["consumer"] and site.line == control["consumer_line"]
        for site in record["CONSUME"])
    lines = [
        "%-22s PUBLISH credited via %s   %s%s"
        % (lane, control["helper_member"], "FOUND" if hits else "MISSING",
           "" if not hits else "  at %s:%s"
           % (os.path.basename(control["caller_file"]),
              "/".join("%d" % site.line for site in sorted(hits, key=lambda s: s.line)))),
        "%-22s lane came from an INFERRED local, not an explicit type argument  %s"
        % ("", "YES" if inferred else "NO - the inference path is dead"),
        "%-22s textual CONSUME at %s:%d  %s"
        % ("", os.path.basename(control["consumer"]), control["consumer_line"],
           "FOUND" if consume_ok else "MISSING"),
        "%-22s verdict %-32s %s"
        % ("", verdict, "NON-ORPHAN" if non_orphan else "STILL ORPHANED - the credit never reached classify()"),
    ]
    return bool(hits) and bool(inferred) and consume_ok and non_orphan, lines


def check_dead_containment_control(helpers):
    """The legacy writer helper must still be rejected wholesale, and must credit no lane a producer."""
    control = DEAD_CONTAINMENT_CONTROL
    matches = [helper for helper in helpers.values() if helper.member == control["helper_member"]]
    rejected = sum(len(helper.dead_credited) for helper in matches)
    credited = sum(len(helper.credited) for helper in matches)
    lines = [
        "%-22s helper found                          %s"
        % (control["helper_member"], "YES" if matches else "NO - control cannot run"),
        "%-22s compile-dead callers rejected  %4d    %s"
        % ("", rejected, "OK (>= %d)" % control["min_rejected"]
           if rejected >= control["min_rejected"] else "TOO FEW - the dead filter is not engaged"),
        "%-22s producers credited from them   %4d    %s"
        % ("", credited, "OK - none" if credited == 0 else "LAUNDERED CS0619 CODE INTO WIRING"),
    ]
    return bool(matches) and rejected >= control["min_rejected"] and credited == 0, lines


def print_filters(sites, aliases, declared_structs, parse_failures, phantoms):
    dead_sites = [site for site in sites if site.dead]
    per_file = {}
    for site in dead_sites:
        per_file.setdefault(site.path, []).append(site)

    print("FILTER 1 - PHANTOMS: SignalBus<X>.Member inside a comment or string literal is NOT a call site")
    print("  textual occurrences discarded: %d" % sum(phantoms.values()))
    for path, count in sorted(phantoms.items(), key=lambda kv: (-kv[1], kv[0]))[:5]:
        print("    %-62s %4d" % (path, count))
    print("    (a text-only count reports these as real wiring; most are [Obsolete] message prose)")
    print()

    print("FILTER 2 - COMPILE-DEAD: [Obsolete(..., true)] member bodies, calling one is CS0619")
    print("  real call sites excluded: %d, in %d file(s)" % (len(dead_sites), len(per_file)))
    for path, group in sorted(per_file.items(), key=lambda kv: (-len(kv[1]), kv[0])):
        kinds = {}
        for site in group:
            kinds[site.kind] = kinds.get(site.kind, 0) + 1
        print("    %-58s %4d  (%s)" % (path, len(group),
                                       ", ".join("%s %d" % kv for kv in sorted(kinds.items()))))
    if parse_failures:
        print("  *** %d [Obsolete(...,true)] attribute(s) whose member body could not be delimited. Their"
              % sum(parse_failures.values()))
        print("  *** call sites are counted as LIVE, so an orphan may be HIDDEN. Fix these first: %s"
              % ", ".join(sorted(parse_failures)))
    print()

    print("FILTER 3 - USING-ALIASES: SignalBus<alias> is the aliased lane, not a lane of its own")
    used = sorted({site.raw_lane for site in sites if site.raw_lane != site.lane})
    for name in used:
        target, path = aliases.get(name, ("?", "?"))
        print("    %-38s -> %-34s %s" % (name, target, path))
    if not used:
        print("    none in use")
    print()


def print_summary(lanes, verdicts, sites, files, resolved=(), unresolved=()):
    counts = {name: 0 for name in CLASS_ORDER}
    for verdict in verdicts.values():
        counts[verdict] += 1
    live_sites = [site for site in sites if not site.dead]
    unknown = [site for site in sites if site.kind == "UNKNOWN"]
    credited = [site for site in resolved if not site.dead]
    rejected_dead = [site for site in resolved if site.dead]

    print("SUMMARY")
    print("  .cs files scanned                    %d" % files)
    print("  real SignalBus<T> call sites         %d" % len(sites))
    print("    compile-dead, excluded             %d" % (len(sites) - len(live_sites)))
    print("    PUBLISH                            %d"
          % len([s for s in live_sites if s.kind == "PUBLISH"]))
    print("    CONSUME                            %d"
          % len([s for s in live_sites if s.kind == "CONSUME"]))
    print("    lifecycle/telemetry - NEITHER      %d"
          % len([s for s in live_sites if s.kind in ("LIFECYCLE", "TELEMETRY")]))
    print("    UNKNOWN member, NOT classified     %d%s"
          % (len(unknown), "" if not unknown else "   <-- see below, the counts are WRONG until fixed"))
    print("  generic sites resolved (FILTER 4)")
    print("    credited to a concrete lane        %d   (PUBLISH %d, CONSUME %d, NEITHER %d)"
          % (len(credited),
             len([s for s in credited if s.kind == "PUBLISH"]),
             len([s for s in credited if s.kind == "CONSUME"]),
             len([s for s in credited if s.kind in ("LIFECYCLE", "TELEMETRY")])))
    print("    rejected, caller is compile-dead   %d   (credited to NOBODY - calling one is CS0619)"
          % len(rejected_dead))
    print("    still unresolved                   %d%s"
          % (len(unresolved), "" if not unresolved else "   <-- orphan classes above stay UPPER BOUNDS"))
    print("  distinct lanes                       %d" % len(verdicts))
    for name in CLASS_ORDER:
        print("    %-34s %d" % (name, counts[name]))
    if unknown:
        print()
        print("  *** SignalBus members this tool cannot classify. Until they are added to PUBLISH_MEMBERS /")
        print("  *** CONSUME_MEMBERS / LIFECYCLE_MEMBERS / TELEMETRY_MEMBERS every count above is suspect:")
        for member in sorted({site.member for site in unknown}):
            example = next(site for site in unknown if site.member == member)
            print("        %-32s e.g. %s" % (member, example.where))
    print()
    counts["lanes"] = len(verdicts)
    counts["compile_dead_sites"] = len(sites) - len(live_sites)
    counts["resolved_credited"] = len(credited)
    counts["resolved_rejected_dead"] = len(rejected_dead)
    counts["unresolved_sites"] = len(unresolved)
    return counts


def print_drift(counts):
    keys = ("lanes",) + CLASS_ORDER + ("compile_dead_sites",)
    guard_keys = keys + ("resolved_credited", "resolved_rejected_dead", "unresolved_sites")
    print("REGRESSION GUARD - vs this tool's own baseline, WITH the generic resolver (FILTER 4)")
    drift = False
    for key in guard_keys:
        delta = counts[key] - BASELINE[key]
        drift = drift or bool(delta)
        print("    %-34s now %4d   baseline %4d   %s"
              % (key, counts[key], BASELINE[key], "same" if delta == 0 else "%+d  DRIFT" % delta))
    if drift:
        print("  A lane moving PUBLISHED-NEVER-CONSUMED -> LIVE is a fix. The reverse is a REGRESSION:")
        print("  somebody deleted the last reader of a lane and nothing anywhere failed. Diff the per-class")
        print("  lists below against the previous run to see which lane moved. resolved_credited moving is a")
        print("  TOOL change, not a wiring change: check FILTER 4 before you read anything into the classes.")
    print()

    print("WHAT FILTER 4 MOVED - vs the PRE-RESOLVER baseline, kept so the gain stays visible")
    for key in guard_keys:
        delta = counts[key] - BASELINE_PRE_RESOLVER[key]
        print("    %-34s now %4d   pre-resolver %4d   %s"
              % (key, counts[key], BASELINE_PRE_RESOLVER[key], "same" if delta == 0 else "%+d" % delta))
    print("  Nine lanes were reclassified and NO .cs file changed - the tool stopped dropping sites whose")
    print("  lane a caller supplies. SaveStatusSignal CONSUMED-NEVER-PUBLISHED -> LIVE, SaveLifecycleSignal")
    print("  and SaveCompletedSignal DEAD -> PUBLISHED-ONLY, and InputSignal / StateCorrectionSignal /")
    print("  DesyncDetectedSignal / SyncFenceSignal PUBLISHED-ONLY -> LIVE through a generic CONSUME.")
    print("  HUDNotificationSignal and KccVelocitySignal gained resolved sites while already being LIVE.")
    print()

    print("CROSS-CHECK - vs the 2026-07-29 hand recon (see RECON_2026_07_29 in this file)")
    for key in keys:
        delta = counts[key] - RECON_2026_07_29[key]
        print("    %-34s here %4d   recon %4d   %s"
              % (key, counts[key], RECON_2026_07_29[key], "agree" if delta == 0 else "%+d" % delta))
    print("  The 296-lane universe is reproduced exactly. The recon's %d compile-dead sites is a TEXT count:"
          % RECON_2026_07_29["compile_dead_sites"])
    print("  93 of them are inside [Obsolete(\"... SignalBus<T>.TryConsumeFrame ...\")] message strings, which")
    print("  FILTER 1 above counts and discards. FILTER 4 closed the orphan-class gap: CONSUMED-NEVER-PUBLISHED")
    print("  and DEAD AT BOTH ENDS now agree with the recon exactly, because the recon had resolved the three")
    print("  SaveManager lanes by hand and this tool had dropped them. The remaining LIVE / PUBLISHED-ONLY")
    print("  difference is EXPLAINED and nameable: StateCorrectionSignal, DesyncDetectedSignal and")
    print("  SyncFenceSignal are read through CoreDeterminismSignals.TryConsumeLane<T> at")
    print("  CoreDeterminismSignals.cs:163/:165/:167 - a generic CONSUME a hand pass over concrete")
    print("  SignalBus<X> text cannot see. Confirm with --lane before acting.")
    print()
    return drift


def print_lane_lists(lanes, verdicts):
    for name in CLASS_ORDER:
        members = sorted(lane for lane, verdict in verdicts.items() if verdict == name)
        print("%s  (%d)" % (name, len(members)))
        for lane in members:
            record = lanes[lane]
            notes = []
            if name != CLASS_LIVE:
                if record["PUBLISH"] and all(
                        site.member in WRITER_HANDLE_MEMBERS for site in record["PUBLISH"]):
                    notes.append("producer is a JOB WRITER HANDLE only, the enqueue is unscannable")
                if record["dead"]:
                    notes.append("%d compile-dead site(s) excluded" % len(record["dead"]))
                if not record["PUBLISH"] and not record["CONSUME"] and record["OTHER"]:
                    notes.append("configured/flushed by %s, so it registers and drains to NOBODY"
                                 % record["OTHER"][0].owner)
            for bucket_name, label in (("PUBLISH", "producer"), ("CONSUME", "reader")):
                group = record[bucket_name]
                via = [site for site in group if site.resolved]
                if via and len(via) == len(group):
                    notes.append("every %s is a FILTER 4 resolution through %s" % (label, via[0].via))
                elif via:
                    notes.append("%d of %d %s(s) resolved through a generic helper"
                                 % (len(via), len(group), label))
            print("  %-52s %s" % (lane, "; ".join(notes)))
            bucket = "PUBLISH" if name == CLASS_PUBLISH_ONLY else "CONSUME" if name == CLASS_CONSUME_ONLY \
                else None
            if bucket:
                owners = sorted({site.owner for site in record[bucket]})
                print("      %s: %s%s"
                      % ("writers" if bucket == "PUBLISH" else "readers", ", ".join(owners[:4]),
                         " (+%d more)" % (len(owners) - 4) if len(owners) > 4 else ""))
        print()


def print_one_lane(lanes, verdicts, wanted):
    matches = sorted(lane for lane in lanes if wanted.lower() in lane.lower())
    if not matches:
        print("no lane matching %r." % wanted)
        print("A lane is the SignalBus<T> TYPE ARGUMENT - not the owner class, not the BufferID, and not")
        print("the internal DTO. Searching the DTO name has already nearly produced a false dead-code")
        print("report on a fully wired pipeline here.")
        return 1
    for lane in matches:
        record = lanes[lane]
        print("%s  ->  %s" % (lane, verdicts[lane]))
        for bucket in ("PUBLISH", "CONSUME", "OTHER", "dead"):
            label = "COMPILE-DEAD, excluded" if bucket == "dead" else bucket
            print("  %s (%d)" % (label, len(record[bucket])))
            for site in sorted(record[bucket], key=lambda s: (s.path, s.line)):
                print("      %-58s %-24s %s" % (site.where, site.member, site.owner))
                if site.resolved:
                    # The caller is the lane's real identity site; print the helper and the evidence so the
                    # resolution can be checked by hand instead of believed.
                    print("          via %s at :%d  (%s)" % (site.via, site.via_line, site.how))
        print()
    return 0


def print_outside(lanes, declared_structs):
    """Answer 'maybe the reader lives elsewhere' with data instead of a caveat."""
    scan_prefix = SCAN_ROOT.replace(os.sep, "/")
    sites, _, _, _, phantoms, files = scan_tree(OUTSIDE_ROOT, skip_prefix=scan_prefix)
    print("OUTSIDE THE SCAN ROOT - %d .cs files under %s/ excluding %s/"
          % (files, OUTSIDE_ROOT, scan_prefix))
    print("  textual occurrences discarded as comment/string: %d" % sum(phantoms.values()))
    real = [site for site in sites if site.lane in declared_structs and not site.dead]
    print("  real call sites: %d" % len(real))
    for site in sorted(real, key=lambda s: (s.path, s.line)):
        verdict = "lane not in scan root" if site.lane not in lanes else ""
        print("    %-58s %-22s %-20s %s" % (site.where, site.lane, site.member + " " + site.kind, verdict))
    rescued = sorted({site.lane for site in real
                      if site.kind == "PUBLISH" and site.lane in lanes and not lanes[site.lane]["PUBLISH"]})
    rescued_readers = sorted({site.lane for site in real
                              if site.kind == "CONSUME" and site.lane in lanes
                              and not lanes[site.lane]["CONSUME"]})
    print("  lanes whose ONLY producer is out here (a test-only producer is NOT gameplay wiring): %s"
          % (", ".join(rescued) or "none"))
    print("  lanes whose ONLY consumer is out here: %s" % (", ".join(rescued_readers) or "none"))
    print()


def main():
    args = sys.argv[1:]
    if any(arg in ("-h", "--help") for arg in args):
        print(__doc__)
        raise SystemExit(2)
    if not os.path.isdir(SCAN_ROOT):
        print("run me from the repo root: %s not found" % SCAN_ROOT)
        raise SystemExit(2)

    sites, declared_structs, aliases, parse_failures, phantoms, files = scan_tree(SCAN_ROOT)
    # Pass 1 finds the generic sites; FILTER 4 resolves their callers; pass 2 classifies with the credited
    # resolutions merged in. Two passes because the resolver needs to know WHICH sites are generic first.
    _, _, generic_sites = classify(sites, declared_structs)
    helpers, resolved, unresolved = resolve_generic_dispatch(generic_sites, declared_structs, SCAN_ROOT)
    lanes, verdicts, _ = classify(sites, declared_structs, resolved)

    print("SIGNAL LANE WIRING AUDIT - static, because SignalBus consumption is PULL-based and registers no")
    print("subscriber. A lane with no reader drains to nobody every frame and NOTHING LOGS IT.")
    print("scan root: %s" % SCAN_ROOT.replace(os.sep, "/"))
    print()
    controls_ok = print_controls(lanes, verdicts, helpers)

    if args and args[0] == "--lane":
        if len(args) < 2:
            print("--lane needs a lane name")
            raise SystemExit(2)
        code = print_one_lane(lanes, verdicts, args[1])
        raise SystemExit(code if controls_ok else 1)

    if args and args[0] == "--outside":
        print_outside(lanes, declared_structs)
        raise SystemExit(0 if controls_ok else 1)

    print_filters(sites, aliases, declared_structs, parse_failures, phantoms)
    print_resolved(helpers)
    print_unresolved(unresolved)
    counts = print_summary(lanes, verdicts, sites, files, resolved, unresolved)
    drift = print_drift(counts)

    if args and args[0] == "--check":
        raise SystemExit(1 if drift or not controls_ok else 0)

    print_lane_lists(lanes, verdicts)
    print("A zero is a QUESTION, not a verdict. Telemetry lanes are legitimately write-only, job producers")
    print("hand out a writer handle this scan cannot follow, and a producer may live outside the scan root")
    print("(run --outside). Read the header of this file before you delete anything.")
    raise SystemExit(0 if controls_ok else 1)


if __name__ == "__main__":
    main()
