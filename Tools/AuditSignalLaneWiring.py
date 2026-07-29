#!/usr/bin/env python
"""Which SignalBus lanes have a writer but no reader (or a reader but no writer)? Static, with two controls.

WHY THIS EXISTS
---------------
SignalBus consumption is strictly PULL-based. There is NO callback and NO subscription registration to
inspect at runtime: the per-lane dispatch table holds only Dispose/Flush/CopyTelemetry function pointers
(Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:308-333), so registering a lane proves nothing
about anybody reading it. A lane with no reader drains to nobody every frame; a lane with no writer hands
its reader an empty snapshot forever. Neither logs anything, neither throws, and the telemetry counters on
a never-pushed lane read exactly like the counters on a healthy idle lane. A static scan of the call sites
is therefore the only way to see the half-wired lanes at all.

WHAT COUNTS AS A SITE
---------------------
Member names are taken from the real API in SignalBusRuntime.cs, not guessed - the bus is not
Publish/Subscribe. Writers are Push / TryPush / TryPushTracked / TryEnqueueBounded plus the job-writer
handle accessors; readers are GetFrameSnapshot / GetSignals / GetFrameSnapshotArray / TryConsumeFrame /
TryGetLatest / TransformSnapshot / FilterSnapshot. Configure / EnsureInitialized / Dispose and the
telemetry counters are NEITHER - a lane that is configured and flushed and read by nobody is the exact
failure this tool is for, so counting Configure as wiring would hide it.

TWO EXCLUSIONS THAT CHANGE THE ANSWER
-------------------------------------
1. COMPILE-DEAD SITES. Members marked [Obsolete(..., true)] cannot be called at all - a call is CS0619, a
   compile ERROR. Their bodies are counted as absent. Without this, GlobalSignals.LegacyFacade.cs alone
   donates a fake TryConsumeFrame reader to dozens of lanes and orphaned lanes look healthy.
2. USING-ALIASES. `using CoreCombatDamageSignal = ...Signals.CombatDamageSignal;` means
   SignalBus<CoreCombatDamageSignal> is the CombatDamageSignal lane. Unresolved, one lane splits in two and
   both halves look half-wired.

MANDATORY POSITIVE CONTROLS
---------------------------
Every run resolves two lanes KNOWN to be fully wired through an owner facade (TryPublish/TryDequeue
wrappers) and asserts both the publish and the consume side at the expected file:line. If either control
fails, the classifier is broken and every negative result in the run is MEANINGLESS - the run says so and
exits non-zero rather than letting a false absence look like a finding. This is not ceremony: the sibling
tool Tools/AuditGuidReachability.py exists because a text-only search silently under-reported, and its
first control choice was itself wrong.

WHAT THIS TOOL CANNOT KNOW
--------------------------
A zero here is a QUESTION, not a verdict.
  * A write-only lane can be CORRECT. Telemetry, black-box and crash-forensics lanes are published so an
    external reader or a post-mortem dump can pick them up; "no consumer" is their design.
  * A read-only lane can be CORRECT. Lanes written from a Burst job through OpenParallelWriter /
    ParallelWriter hand a writer handle to code this scan cannot follow, and a lane fed by an editor tool,
    a test harness, a mod through HectonEventBus, or generated code has a producer outside the scan root.
  * The scan root is one folder of C#. It does not read scenes, prefabs, ScriptableObjects, other
    assemblies, #if-disabled branches (they are counted as live), or reflection.
  * Enclosing owner type/member on each site is best-effort text attribution for triage, not a parse.
So this tool tells you WHERE TO LOOK. It never says a lane is dead. The last time something in this repo
was declared dead on a static reachability walk, a tool deleted a node from the authored world graph.

USAGE, from the repo root:

    python -B Tools/AuditSignalLaneWiring.py                # full report
    python -B Tools/AuditSignalLaneWiring.py --check        # summary + drift vs baseline, non-zero on drift
    python -B Tools/AuditSignalLaneWiring.py --lane NAME    # every site for one lane, for triage
"""
import bisect
import os
import re
import sys

SCAN_ROOT = os.path.join("Assets", "_Project", "Scripts")

# Real member names, read out of Core/Signals/SignalBusRuntime.cs. Line numbers are the declarations.
PUBLISH_MEMBERS = {
    "Push": "SignalBusRuntime.cs:669",
    "TryPush": "SignalBusRuntime.cs:678",
    "TryPushTracked": "SignalBusRuntime.cs:722",
    "TryEnqueueBounded": "SignalBusRuntime.cs:733",
    # Writer-handle accessors. Weaker evidence than a push: they hand a ring writer to a job, and the
    # enqueue happens inside code this scan cannot follow. Counted as a producer, flagged in the report.
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

# Neither. Lifecycle and telemetry touch a lane without moving a payload through it, which is precisely the
# state this tool is built to expose, so they must not launder a lane into "wired".
LIFECYCLE_MEMBERS = frozenset((
    "Configure", "ConfigureCacheLineCritical", "EnsureInitialized", "Dispose",
    "FlushPostSimulation",
))
TELEMETRY_MEMBERS = frozenset((
    "LaneHash", "SnapshotCount", "SnapshotGeneration", "DroppedLastFlush", "LoadShedTotal",
    "CorruptedSignalTotal", "PeakQueuedLastFlush", "HasNativeStorage",
))

CLASS_LIVE = "LIVE"
CLASS_PUBLISH_ONLY = "PUBLISHED, NEVER CONSUMED"
CLASS_CONSUME_ONLY = "CONSUMED, NEVER PUBLISHED"
CLASS_DEAD = "DEAD AT BOTH ENDS"
CLASS_ORDER = (CLASS_LIVE, CLASS_PUBLISH_ONLY, CLASS_CONSUME_ONLY, CLASS_DEAD)

# Recon baseline, 2026-07-29. --check fails on any drift from this; update it deliberately, with a reason.
BASELINE = {
    "lanes": 296,
    CLASS_LIVE: 159,
    CLASS_PUBLISH_ONLY: 115,
    CLASS_CONSUME_ONLY: 7,
    CLASS_DEAD: 15,
}

# lane, expected publish file:line, expected consume file:line. Both are owner-facade wrapped on purpose:
# the controls prove the scan sees through a TryPublish/TryDequeue wrapper, which is how most lanes are used.
CONTROLS = (
    ("WorldChunkPhysicsBakedSignal",
     "Assets/_Project/Scripts/World/WorldChunkPhysicsBakedEvents.cs", 79, 92),
    ("TerrainChunkGeneratedSignal",
     "Assets/_Project/Scripts/TerrainChunkGeneratedEvents.cs", 45, 59),
)

SIGNALBUS_SITE = re.compile(r"\bSignalBus\s*<\s*([A-Za-z0-9_.]+)\s*>\s*\.\s*([A-Za-z0-9_]+)")
USING_ALIAS = re.compile(r"^\s*using\s+([A-Za-z0-9_]+)\s*=\s*([A-Za-z0-9_.:]+)\s*;", re.MULTILINE)
STRUCT_DECL = re.compile(r"\b(?:readonly\s+|partial\s+|unsafe\s+|ref\s+)*struct\s+([A-Za-z0-9_]+)")
OBSOLETE_ATTR = re.compile(r"(?:global::)?(?:System\s*\.\s*)?Obsolete\s*\(")
NOT_A_MEMBER = frozenset((
    "if", "for", "foreach", "while", "do", "else", "switch", "catch", "try", "finally", "using",
    "lock", "fixed", "unsafe", "return", "get", "set", "add", "remove", "new",
))


def blank_noncode(text):
    """Replace comment and string-literal content with spaces, preserving length and line numbers.

    Without this, a commented-out `// SignalBus<X>.TryConsumeFrame(...)` reads as a live consumer, which is
    the same class of mistake as counting a compile-dead call.
    """
    out = list(text)
    length = len(text)
    i = 0
    while i < length:
        ch = text[i]
        if ch == "/" and i + 1 < length and text[i + 1] == "/":
            j = text.find("\n", i)
            j = length if j < 0 else j
            for k in range(i, j):
                out[k] = " "
            i = j
            continue
        if ch == "/" and i + 1 < length and text[i + 1] == "*":
            j = text.find("*/", i + 2)
            j = length if j < 0 else j + 2
            for k in range(i, j):
                if out[k] != "\n":
                    out[k] = " "
            i = j
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
            j = i + 2
            while j < length:
                if text[j] == '"':
                    if j + 1 < length and text[j + 1] == '"':
                        j += 2
                        continue
                    j += 1
                    break
                j += 1
            for k in range(i, min(j, length)):
                if out[k] != "\n":
                    out[k] = " "
            i = j
            continue
        if ch in '"\'':
            quote = ch
            j = i + 1
            while j < length:
                if text[j] == "\\":
                    j += 2
                    continue
                if text[j] == quote or text[j] == "\n":
                    j += 1
                    break
                j += 1
            for k in range(i, min(j, length)):
                if out[k] != "\n":
                    out[k] = " "
            i = j
            continue
        i += 1
    return "".join(out)


def skip_attribute_groups(text, pos):
    """Advance past whitespace and any further [Attribute] groups following an attribute close bracket."""
    length = len(text)
    while pos < length:
        while pos < length and text[pos].isspace():
            pos += 1
        if pos < length and text[pos] == "[":
            depth = 0
            while pos < length:
                if text[pos] == "[":
                    depth += 1
                elif text[pos] == "]":
                    depth -= 1
                    if depth == 0:
                        pos += 1
                        break
                pos += 1
            continue
        return pos
    return pos


def match_bracket(text, pos, opener, closer):
    """Index just past the closer matching the opener at pos, or -1."""
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


def member_span_after(text, pos):
    """Span of the member declared at pos: block body, expression body, or bare declaration. None on failure."""
    length = len(text)
    depth = 0
    while pos < length:
        ch = text[pos]
        if ch in "([<":
            depth += 1
        elif ch in ")]>":
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
    """Spans of members marked [Obsolete(..., true)]. Calling one is CS0619, so their bodies are not code."""
    spans = []
    failures = []
    for match in OBSOLETE_ATTR.finditer(code):
        open_paren = code.index("(", match.end() - 1)
        args_end = match_bracket(code, open_paren, "(", ")")
        if args_end < 0:
            failures.append(match.start())
            continue
        args = code[open_paren + 1:args_end - 1]
        if not re.search(r"(?:^|,)\s*(?:error\s*:\s*)?true\s*$", args.split(",")[-1]) or "," not in args:
            continue  # [Obsolete] or [Obsolete("msg")] or error:false - still compiles, still a real call site
        bracket_end = code.find("]", args_end - 1)
        if bracket_end < 0:
            failures.append(match.start())
            continue
        member_start = skip_attribute_groups(code, bracket_end + 1)
        span = member_span_after(code, member_start)
        if span is None:
            failures.append(match.start())
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


def type_scopes(code):
    """(body_start, body_end, type_name) for every class/struct/interface/record body."""
    scopes = []
    stack = []
    header_start = 0
    decl = re.compile(r"\b(?:class|struct|interface|record)\s+([A-Za-z0-9_]+)")
    for i, ch in enumerate(code):
        if ch == "{":
            match = None
            for match in decl.finditer(code, header_start, i):
                pass
            stack.append((i, match.group(1) if match else None))
            header_start = i + 1
        elif ch == "}":
            if stack:
                start, name = stack.pop()
                if name:
                    scopes.append((start, i, name))
            header_start = i + 1
        elif ch == ";":
            header_start = i + 1
    return scopes


def member_starts(code):
    """Best-effort offsets and names of member declarations, for attributing a site to its owner member."""
    starts = []
    names = []
    pattern = re.compile(r"([A-Za-z0-9_]+)\s*(?:<[A-Za-z0-9_,\s]*>)?\s*\(")
    for match in pattern.finditer(code):
        name = match.group(1)
        if name in NOT_A_MEMBER:
            continue
        starts.append(match.start())
        names.append(name)
    return starts, names


class Site(object):
    __slots__ = ("path", "line", "lane", "member", "kind", "owner_type", "owner_member", "dead")

    def __init__(self, path, line, lane, member, kind, owner_type, owner_member, dead):
        self.path = path
        self.line = line
        self.lane = lane
        self.member = member
        self.kind = kind
        self.owner_type = owner_type
        self.owner_member = owner_member
        self.dead = dead

    @property
    def owner(self):
        return "%s.%s" % (self.owner_type or "?", self.owner_member or "?")

    @property
    def where(self):
        return "%s:%d" % (self.path, self.line)


def scan_file(path):
    try:
        raw = open(path, encoding="utf-8", errors="replace").read()
    except OSError:
        return [], [], {}, []

    code = blank_noncode(raw)
    aliases = {}
    for match in USING_ALIAS.finditer(code):
        aliases[match.group(1)] = match.group(2).replace("global::", "").split(".")[-1]

    dead_spans, parse_failures = compile_dead_spans(code)
    dead_starts = [span[0] for span in dead_spans]
    scopes = type_scopes(code)
    starts, names = member_starts(code)
    newlines = [i for i, ch in enumerate(code) if ch == "\n"]
    rel = path.replace(os.sep, "/")

    sites = []
    for match in SIGNALBUS_SITE.finditer(code):
        offset = match.start()
        index = bisect.bisect_right(dead_starts, offset) - 1
        dead = index >= 0 and offset < dead_spans[index][1]

        raw_lane = match.group(1).split(".")[-1]
        lane = aliases.get(raw_lane, raw_lane)
        member = match.group(2)
        if member in PUBLISH_MEMBERS:
            kind = "PUBLISH"
        elif member in CONSUME_MEMBERS:
            kind = "CONSUME"
        elif member in LIFECYCLE_MEMBERS:
            kind = "LIFECYCLE"
        elif member in TELEMETRY_MEMBERS:
            kind = "TELEMETRY"
        else:
            kind = "UNKNOWN"

        owner_type = None
        for start, end, name in scopes:
            if start < offset < end and (owner_type is None or start > owner_type[0]):
                owner_type = (start, name)
        member_index = bisect.bisect_right(starts, offset) - 1
        owner_member = names[member_index] if member_index >= 0 else None

        sites.append(Site(rel, bisect.bisect_right(newlines, offset) + 1, lane, member, kind,
                          owner_type[1] if owner_type else None, owner_member, dead))

    declared = set(STRUCT_DECL.findall(code))
    return sites, sorted(declared), aliases, [rel] * len(parse_failures)


def scan_tree(root):
    sites = []
    declared_structs = set()
    aliases = {}
    parse_failures = []
    files = 0
    for directory, _, filenames in os.walk(root):
        for filename in sorted(filenames):
            if not filename.endswith(".cs"):
                continue
            files += 1
            path = os.path.join(directory, filename)
            file_sites, declared, file_aliases, failures = scan_file(path)
            sites += file_sites
            declared_structs.update(declared)
            parse_failures += failures
            for name, target in file_aliases.items():
                aliases.setdefault(name, (target, path.replace(os.sep, "/")))
    return sites, declared_structs, aliases, parse_failures, files


def classify(sites, declared_structs):
    lanes = {}
    for site in sites:
        if site.lane not in declared_structs:
            continue  # generic parameter T/TSignal, or a type declared outside the scan root
        record = lanes.setdefault(site.lane, {"PUBLISH": [], "CONSUME": [], "OTHER": [], "dead": []})
        if site.dead:
            record["dead"].append(site)
            continue
        record[site.kind if site.kind in ("PUBLISH", "CONSUME") else "OTHER"].append(site)

    verdicts = {}
    for lane, record in lanes.items():
        has_publish = bool(record["PUBLISH"])
        has_consume = bool(record["CONSUME"])
        if has_publish and has_consume:
            verdicts[lane] = CLASS_LIVE
        elif has_publish:
            verdicts[lane] = CLASS_PUBLISH_ONLY
        elif has_consume:
            verdicts[lane] = CLASS_CONSUME_ONLY
        else:
            verdicts[lane] = CLASS_DEAD
    return lanes, verdicts


def run_controls(lanes):
    results = []
    for lane, path, publish_line, consume_line in CONTROLS:
        record = lanes.get(lane)
        publish_ok = consume_ok = False
        if record:
            publish_ok = any(s.path == path and s.line == publish_line for s in record["PUBLISH"])
            consume_ok = any(s.path == path and s.line == consume_line for s in record["CONSUME"])
        results.append((lane, path, publish_line, consume_line, publish_ok, consume_ok))
    return results


def print_controls(results):
    print("POSITIVE CONTROLS - two lanes known to be fully wired through an owner facade")
    passed = 0
    for lane, path, publish_line, consume_line, publish_ok, consume_ok in results:
        ok = publish_ok and consume_ok
        passed += 1 if ok else 0
        print("  %-30s publish %s:%-5d %-7s consume %s:%-5d %s"
              % (lane, os.path.basename(path), publish_line, "FOUND" if publish_ok else "MISSING",
                 os.path.basename(path), consume_line, "FOUND" if consume_ok else "MISSING"))
    print("  CONTROLS: %d/%d PASS" % (passed, len(results)))
    if passed != len(results):
        print("  *** THE METHOD IS BROKEN. Every negative result below is MEANINGLESS - a lane reported")
        print("  *** with no consumer may simply be a lane this run failed to parse. Fix the tool first.")
    print()
    return passed == len(results)


def print_exclusions(sites, aliases, declared_structs, parse_failures, files):
    dead_sites = [s for s in sites if s.dead]
    per_file = {}
    for site in dead_sites:
        per_file[site.path] = per_file.get(site.path, 0) + 1
    print("COMPILE-DEAD EXCLUSION - [Obsolete(..., true)] member bodies, a call is CS0619")
    print("  excluded call sites: %d, in %d file(s)" % (len(dead_sites), len(per_file)))
    for path, count in sorted(per_file.items(), key=lambda kv: (-kv[1], kv[0])):
        by_kind = {}
        for site in dead_sites:
            if site.path == path:
                by_kind[site.kind] = by_kind.get(site.kind, 0) + 1
        print("    %-58s %4d  (%s)" % (path, count,
              ", ".join("%s %d" % (k, v) for k, v in sorted(by_kind.items()))))
    if parse_failures:
        print("  *** %d [Obsolete(...,true)] attribute(s) whose member body could not be delimited;"
              % len(parse_failures))
        print("  *** their call sites are counted as LIVE, so an orphan may be hidden. Files: %s"
              % ", ".join(sorted(set(parse_failures))))
    print()

    used_aliases = {name: target for name, (target, _) in aliases.items()
                    if any(True for _ in ()) or True}
    del used_aliases
    print("USING-ALIAS RESOLUTION - SignalBus<alias> is the aliased lane, not a lane of its own")
    shown = 0
    for name in sorted(aliases):
        target, path = aliases[name]
        if name in declared_structs or target not in declared_structs:
            continue
        if not any(s.lane == target for s in sites):
            continue
        print("    %-38s -> %-38s %s" % (name, target, path))
        shown += 1
    if shown == 0:
        print("    none in use")
    print()


def print_summary(lanes, verdicts, sites, files):
    counts = {name: 0 for name in CLASS_ORDER}
    for verdict in verdicts.values():
        counts[verdict] += 1
    print("SUMMARY")
    print("  .cs files scanned                 %d" % files)
    print("  SignalBus<T> call sites total     %d" % len(sites))
    print("  ...compile-dead, excluded         %d" % len([s for s in sites if s.dead]))
    print("  ...live PUBLISH                   %d" % len([s for s in sites if not s.dead and s.kind == "PUBLISH"]))
    print("  ...live CONSUME                   %d" % len([s for s in sites if not s.dead and s.kind == "CONSUME"]))
    print("  ...lifecycle/telemetry, neither   %d"
          % len([s for s in sites if not s.dead and s.kind in ("LIFECYCLE", "TELEMETRY")]))
    unknown = [s for s in sites if s.kind == "UNKNOWN"]
    print("  ...UNKNOWN member, NOT classified %d%s"
          % (len(unknown), "" if not unknown else "   <-- classifier is out of date, see list below"))
    print("  distinct lanes                    %d" % len(verdicts))
    for name in CLASS_ORDER:
        print("    %-28s    %d" % (name, counts[name]))
    if unknown:
        print()
        print("  *** SignalBus members this tool does not know. Classify them in PUBLISH_MEMBERS /")
        print("  *** CONSUME_MEMBERS / LIFECYCLE_MEMBERS / TELEMETRY_MEMBERS or the counts above are wrong:")
        for member in sorted({s.member for s in unknown}):
            example = next(s for s in unknown if s.member == member)
            print("        %-32s e.g. %s" % (member, example.where))
    print()
    return counts


def print_baseline_drift(counts, lane_total):
    print("BASELINE DRIFT vs the 2026-07-29 recon")
    drift = False
    rows = [("lanes", lane_total, BASELINE["lanes"])]
    rows += [(name, counts[name], BASELINE[name]) for name in CLASS_ORDER]
    for name, actual, expected in rows:
        delta = actual - expected
        if delta:
            drift = True
        print("  %-28s now %4d   baseline %4d   %s"
              % (name, actual, expected, "same" if delta == 0 else "%+d  DRIFT" % delta))
    if drift:
        print("  A lane moving from PUBLISHED-NEVER-CONSUMED to LIVE is a fix. The reverse is a regression:")
        print("  somebody deleted the only reader and nothing failed. Diff the per-class lists to see which.")
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
                handle_only = (record["PUBLISH"] and
                               all(s.member in WRITER_HANDLE_MEMBERS for s in record["PUBLISH"]))
                if handle_only:
                    notes.append("producer is a JOB WRITER HANDLE only - real enqueue is unscannable")
                if record["dead"]:
                    notes.append("%d compile-dead site(s) excluded" % len(record["dead"]))
                if not record["PUBLISH"] and not record["CONSUME"] and record["OTHER"]:
                    notes.append("configured/flushed by %s" % record["OTHER"][0].owner)
            print("  %-52s %s" % (lane, "; ".join(notes)))
            if name == CLASS_PUBLISH_ONLY:
                owners = sorted({s.owner for s in record["PUBLISH"]})
                print("      writers: %s%s"
                      % (", ".join(owners[:4]), " (+%d more)" % (len(owners) - 4) if len(owners) > 4 else ""))
            elif name == CLASS_CONSUME_ONLY:
                owners = sorted({s.owner for s in record["CONSUME"]})
                print("      readers: %s%s"
                      % (", ".join(owners[:4]), " (+%d more)" % (len(owners) - 4) if len(owners) > 4 else ""))
        print()


def print_one_lane(lanes, verdicts, wanted):
    matches = sorted(lane for lane in lanes if wanted.lower() in lane.lower())
    if not matches:
        print("no lane matching %r. Note the lane is the SignalBus<T> TYPE ARGUMENT, not the owner class,"
              % wanted)
        print("not the BufferID and not the internal DTO.")
        return 1
    for lane in matches:
        record = lanes[lane]
        print("%s  ->  %s" % (lane, verdicts[lane]))
        for bucket in ("PUBLISH", "CONSUME", "OTHER", "dead"):
            label = "COMPILE-DEAD (excluded)" if bucket == "dead" else bucket
            print("  %s (%d)" % (label, len(record[bucket])))
            for site in sorted(record[bucket], key=lambda s: (s.path, s.line)):
                print("      %-62s %-24s %s" % (site.where, site.member, site.owner))
        print()
    return 0


def main():
    args = sys.argv[1:]
    if any(arg in ("-h", "--help") for arg in args):
        print(__doc__)
        raise SystemExit(2)
    if not os.path.isdir(SCAN_ROOT):
        print("run me from the repo root: %s not found" % SCAN_ROOT)
        raise SystemExit(2)

    sites, declared_structs, aliases, parse_failures, files = scan_tree(SCAN_ROOT)
    lanes, verdicts = classify(sites, declared_structs)
    control_results = run_controls(lanes)

    print("SIGNAL LANE WIRING AUDIT - static, because SignalBus consumption is PULL-based and registers")
    print("no subscriber. A lane with no reader drains to nobody and NOTHING LOGS IT.")
    print("scan root: %s" % SCAN_ROOT.replace(os.sep, "/"))
    print()

    controls_ok = print_controls(control_results)

    if args and args[0] == "--lane":
        if len(args) < 2:
            print("--lane needs a lane name")
            raise SystemExit(2)
        code = print_one_lane(lanes, verdicts, args[1])
        raise SystemExit(code if controls_ok else 1)

    print_exclusions(sites, aliases, declared_structs, parse_failures, files)
    counts = print_summary(lanes, verdicts, sites, files)
    drift = print_baseline_drift(counts, len(verdicts))

    if args and args[0] == "--check":
        if not controls_ok:
            raise SystemExit(1)
        raise SystemExit(1 if drift else 0)

    print_lane_lists(lanes, verdicts)
    print("A zero is a QUESTION, not a verdict. Telemetry lanes are legitimately write-only, job producers")
    print("hand out a writer handle this scan cannot follow, and a reader may live outside %s."
          % SCAN_ROOT.replace(os.sep, "/"))
    print("Read the header of this file before you delete anything.")
    raise SystemExit(0 if controls_ok else 1)


if __name__ == "__main__":
    main()
