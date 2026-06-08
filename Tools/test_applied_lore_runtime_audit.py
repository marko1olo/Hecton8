import csv
import contextlib
import io
import json
import sys
from test_temp_root import temporary_directory
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parent))

from AppliedLoreRuntimeAudit import (
    AuditFailure,
    CsvPacketRow,
    DATA_MONOLITH_BAKE_METHOD,
    DATA_MONOLITH_OUTPUT_RELATIVE_PATH,
    SectionEntry,
    collect_uncovered_scene_placement_samples,
    fnv1a32,
    has_text_integrity_issue,
    pda_unlock_free_slots,
    production_gate_json_payload,
    validate_applied_records,
    validate_evidence_graph,
    validate_binding_map,
    validate_import_outputs_current,
    validate_manual_binding_policy,
    validate_native_localization_gate,
    load_navigation_cluster_graph,
    validate_pda_event_queue_refusal_visibility,
    validate_pda_event_try_register_lifecycle,
    validate_pda_lore_signal_drop_visibility,
    validate_pda_runtime_capacity,
    validate_pda_logbook_save_load_bridge,
    validate_publication_outputs_current,
    validate_data_monolith_bake_cli_bridge,
    validate_route_cards,
    validate_scene_binding_targets,
    validate_scene_placement_plan,
    validate_scene_placement_commandline_apply,
    validate_scene_placement_runtime_coverage,
    validate_scene_placement_preflight_abort,
    validate_terminal_os_scene_runtime_keying,
    validate_terminal_preview_drop_visibility,
    main,
    run_production_gate,
)


def packet_row(
    packet_id: str,
    *,
    flags: int = 0,
    locale: str = "en_US",
    fields: dict[str, str] | None = None,
) -> CsvPacketRow:
    return CsvPacketRow(
        packet_id=packet_id,
        locale=locale,
        release_set_id="RS_TEST",
        article_id="test.article",
        unlock_id="unlock.test",
        surface_mask=63,
        fields=fields or {},
        poi_tags=(),
        biome_tags=(),
        flags=flags,
        line_number=2,
    )


def write_binding_map(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    headers = (
        "packet_id",
        "packet_hash_hex",
        "packet_hash_uint",
        "primary_component",
        "primary_field",
        "suggested_world_target",
        "unlock_moment",
    )
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=headers, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def write_scene_binding_targets(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    headers = (
        "packet_id",
        "packet_hash_hex",
        "packet_hash_decimal",
        "authoring_component",
        "serialized_field",
        "primary_target_candidates",
        "secondary_target_candidates",
        "unity_safe_action",
        "notes",
    )
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=headers, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def write_manual_binding_policy(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    headers = (
        "packet_id",
        "packet_hash_hex",
        "packet_hash_decimal",
        "manual_policy",
        "required_anchor_type",
        "approved_template_prefab",
        "authoring_component",
        "serialized_field",
        "discovery_id",
        "placement_rule",
        "reason",
    )
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=headers, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def write_scene_placement_plan(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    headers = (
        "packet_id",
        "packet_hash_hex",
        "packet_hash_decimal",
        "scene_path",
        "placement_root",
        "object_name",
        "source_prefab",
        "authoring_component",
        "serialized_field",
        "discovery_id",
        "display_name",
        "local_position",
        "local_euler",
        "local_scale",
        "depth_band",
        "zone_tag",
        "placement_note",
    )
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=headers, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def write_scene_placement_dependencies(root: Path) -> None:
    scene_path = root / "Assets" / "_Project" / "Scenes" / "Test.unity"
    scene_path.parent.mkdir(parents=True, exist_ok=True)
    scene_path.write_text("%YAML 1.1\n", encoding="utf-8")
    prefab_path = root / "Assets" / "_Project" / "Prefabs" / "Test" / "PFB_Test.prefab"
    prefab_path.parent.mkdir(parents=True, exist_ok=True)
    prefab_path.write_text("%YAML 1.1\n", encoding="utf-8")


def write_evidence_graph(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    headers = (
        "packet_id",
        "arc_id",
        "depth_band",
        "route_moment",
        "prereq_packet_ids",
        "next_packet_ids",
        "evidence_type",
        "truth_claim",
        "player_decision",
        "spoiler_tier",
        "primary_surface",
    )
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=headers, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def write_route_cards(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    headers = (
        "route_card_id",
        "phase_id",
        "depth_min_m",
        "depth_max_m",
        "packet_ids",
        "required_packet_ids",
        "primary_surface",
        "world_object_hint",
        "player_question",
        "truth_payload",
        "replay_axis",
        "ending_pressure",
    )
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=headers, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def write_data_monolith_bake_cli_bridge_sources(
    root: Path,
    *,
    include_project: bool = True,
    include_world_impact_compile: bool = True,
    include_xxhash_source: bool = True,
    stale_xxhash_include: bool = False,
    stale_compiler_include: bool = False,
    include_load_stress_probe: bool = True,
    include_unity_editor_stubs: bool = True,
    include_cli_failure_codes: bool = True,
    include_cli_exception_boundary: bool = True,
    include_runnable_project_contract: bool = True,
    duplicate_world_impact_in_runtime: bool = False,
) -> None:
    cli_dir = root / "Tools" / "DataMonolithBakeCli"
    cli_dir.mkdir(parents=True, exist_ok=True)

    if include_project:
        world_impact_include = (
            '    <Compile Include="..\\..\\Assets\\_Project\\Scripts\\Data\\Monolith\\H8AppliedLoreWorldImpactRecord.cs" />\n'
            if include_world_impact_compile
            else ""
        )
        xxhash_include = ""
        if include_xxhash_source:
            xxhash_package = "stale" if stale_xxhash_include else "hash"
            xxhash_include = (
                f'    <Compile Include="..\\..\\Library\\PackageCache\\com.unity.collections@{xxhash_package}\\Unity.Collections\\xxHash3.cs" />\n'
            )
        compiler_include = (
            "..\\..\\Assets\\_Project\\Scripts\\Editor\\DataMonolith\\MissingH8DataMonolithCompiler.cs"
            if stale_compiler_include
            else "..\\..\\Assets\\_Project\\Scripts\\Editor\\DataMonolith\\H8DataMonolithCompiler.cs"
        )
        runnable_properties = (
            "    <OutputType>Exe</OutputType>\n"
            "    <TargetFramework>net10.0</TargetFramework>\n"
            "    <SelfContained>false</SelfContained>\n"
            "    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>\n"
            if include_runnable_project_contract
            else ""
        )
        (cli_dir / "DataMonolithBakeCli.csproj").write_text(
            "<Project>\n"
            "  <PropertyGroup>\n"
            f"{runnable_properties}"
            "    <DefineConstants>$(DefineConstants);UNITY_EDITOR</DefineConstants>\n"
            "  </PropertyGroup>\n"
            "  <ItemGroup>\n"
            f"{world_impact_include}"
            '    <Compile Include="..\\..\\Assets\\_Project\\Scripts\\Data\\Monolith\\H8DataHash.cs" />\n'
            '    <Compile Include="..\\..\\Assets\\_Project\\Scripts\\Data\\Monolith\\H8DataMonolithTypes.cs" />\n'
            f'    <Compile Include="{compiler_include}" />\n'
            '    <Compile Include="..\\..\\Assets\\_Project\\Scripts\\Editor\\DataMonolith\\H8DataMonolithCorruptionFuzzer.cs" />\n'
            f"{xxhash_include}"
            "  </ItemGroup>\n"
            "</Project>\n",
            encoding="utf-8",
        )

    if include_xxhash_source:
        package_xxhash_path = (
            root
            / "Library"
            / "PackageCache"
            / "com.unity.collections@hash"
            / "Unity.Collections"
            / "xxHash3.cs"
        )
        package_xxhash_path.parent.mkdir(parents=True, exist_ok=True)
        package_xxhash_path.write_text("public static partial class xxHash3 { }\n", encoding="utf-8")

    (cli_dir / "UnityHashStubs.cs").write_text(
        "namespace Unity.Collections\n"
        "{\n"
        "    public static unsafe partial class xxHash3\n"
        "    {\n"
        "        public struct StreamingState\n"
        "        {\n"
        "            public Unity.Mathematics.uint2 DigestHash64()\n"
        "            {\n"
        "                fixed (byte* ptr = _buffer) return Hash64(ptr, _length);\n"
        "            }\n"
        "            private byte[] _buffer;\n"
        "            private int _length;\n"
        "        }\n"
        "        private static void Avx2HashLongInternalLoop(ulong* acc, byte* input, byte* dest, long length, byte* secret, int isHash64)\n"
        "        {\n"
        "            DefaultHashLongInternalLoop(acc, input, dest, length, secret, isHash64);\n"
        "        }\n"
        "    }\n"
        "}\n",
        encoding="utf-8",
    )

    if include_unity_editor_stubs:
        (cli_dir / "UnityEditorStubs.cs").write_text(
            "namespace Unity.Collections.LowLevel.Unsafe { public static unsafe class UnsafeUtility { } }\n"
            "namespace UnityEngine\n"
            "{\n"
            "    public static class Application { }\n"
            "    public static class Debug { }\n"
            "    public static class JsonUtility { }\n"
            "}\n"
            "namespace UnityEditor\n"
            "{\n"
            "    public sealed class MenuItem { }\n"
            "    public static class AssetDatabase { }\n"
            "    public static class EditorApplication { }\n"
            "}\n",
            encoding="utf-8",
        )

    (root / "Hecton8.slnx").write_text(
        "<Solution>\n"
        "  <Project Path=\"Tools\\DataMonolithBakeCli\\DataMonolithBakeCli.csproj\" />\n"
        "</Solution>\n",
        encoding="utf-8",
    )
    (root / ".gitignore").write_text(
        "*.csproj\n"
        "!Tools/DataMonolithBakeCli/DataMonolithBakeCli.csproj\n",
        encoding="utf-8",
    )

    if include_cli_failure_codes:
        run_body = (
            "public static class Program\n"
            "{\n"
            "    public static int Main(string[] args)\n"
            "    {\n"
        )
        if include_cli_exception_boundary:
            run_body += (
                "        try\n"
                "        {\n"
                "            return Run(args);\n"
                "        }\n"
                "        catch (Exception exception)\n"
                "        {\n"
                "            Console.Error.WriteLine(\"Data Monolith CLI crashed: \" + exception);\n"
                "            return 8;\n"
                "        }\n"
                "    }\n"
                "    private static int Run(string[] args)\n"
                "    {\n"
            )
        else:
            run_body += (
                "        return Run(args);\n"
                "    }\n"
                "    private static int Run(string[] args)\n"
                "    {\n"
            )
        program_source = (
            run_body +
            "        if (projectRoot == null) return 2;\n"
            "        Directory.SetCurrentDirectory(projectRoot);\n"
            "        UnityEngine.Application.dataPath = projectRoot;\n"
            "        UnityEngine.Application.version = ReadUnityBundleVersion(projectRoot);\n"
            "        if (!H8DataMonolithCompiler.BakeAll(logSummary: true)) return 1;\n"
            "        H8DataMonolithCompiler.TryValidateOutputBlob(out string error);\n"
            "        if (!H8DataMonolithCorruptionFuzzer.Run()) return 3;\n"
            "        if (!DataMonolithLoadStressProbe.Run(projectRoot)) return 4;\n"
            "        if (!DataMonolithFailClosedProbe.Run(projectRoot)) return 5;\n"
            "        if (!DataMonolithPlayerParserAbsenceProbe.Run(projectRoot)) return 6;\n"
            "        if (!DataMonolithSourceInventoryProbe.Run(projectRoot)) return 7;\n"
            "        return 0;\n"
            "    }\n"
            "}\n"
        )
    else:
        program_source = (
            "public static class Program\n"
            "{\n"
            "    public static int Main(string[] args)\n"
            "    {\n"
            "        try\n"
            "        {\n"
            "            Run(args);\n"
            "            return 0;\n"
            "        }\n"
            "        catch (Exception exception)\n"
            "        {\n"
            "            Console.Error.WriteLine(\"Data Monolith CLI crashed: \" + exception);\n"
            "            return 8;\n"
            "        }\n"
            "    }\n"
            "    private static void Run(string[] args)\n"
            "    {\n"
            "        Directory.SetCurrentDirectory(projectRoot);\n"
            "        UnityEngine.Application.dataPath = projectRoot;\n"
            "        UnityEngine.Application.version = ReadUnityBundleVersion(projectRoot);\n"
            "        H8DataMonolithCompiler.BakeAll(logSummary: true);\n"
            "        H8DataMonolithCompiler.TryValidateOutputBlob(out string error);\n"
            "        H8DataMonolithCorruptionFuzzer.Run();\n"
            "        DataMonolithLoadStressProbe.Run(projectRoot);\n"
            "        DataMonolithFailClosedProbe.Run(projectRoot);\n"
            "        DataMonolithPlayerParserAbsenceProbe.Run(projectRoot);\n"
            "        DataMonolithSourceInventoryProbe.Run(projectRoot);\n"
            "    }\n"
            "}\n"
        )
    (cli_dir / "Program.cs").write_text(program_source, encoding="utf-8")

    contract_path = root / "Assets" / "_Project" / "Scripts" / "Data" / "Monolith" / "H8AppliedLoreWorldImpactRecord.cs"
    contract_path.parent.mkdir(parents=True, exist_ok=True)
    contract_path.write_text(
        "using System.Runtime.InteropServices;\n"
        "namespace Hecton8.Data\n"
        "{\n"
        "    [StructLayout(LayoutKind.Explicit, Size = 24)]\n"
        "    public struct H8AppliedLoreWorldImpactRecord\n"
        "    {\n"
        "        public const int SizeBytes = 24;\n"
        "        [FieldOffset(20)] private uint _pad2;\n"
        "    }\n"
        "}\n",
        encoding="utf-8",
    )

    runtime_path = root / "Assets" / "_Project" / "Scripts" / "Data" / "Monolith" / "H8AppliedLoreRuntime.cs"
    runtime_text = (
        "namespace Hecton8.Data { public struct H8AppliedLoreWorldImpactRecord { } }\n"
        if duplicate_world_impact_in_runtime
        else "namespace Hecton8.Data { public static class H8AppliedLoreRuntime { } }\n"
    )
    runtime_path.write_text(runtime_text, encoding="utf-8")

    data_hash_path = root / "Assets" / "_Project" / "Scripts" / "Data" / "Monolith" / "H8DataHash.cs"
    data_hash_path.write_text("public static class H8DataHash { }\n", encoding="utf-8")

    data_types_path = root / "Assets" / "_Project" / "Scripts" / "Data" / "Monolith" / "H8DataMonolithTypes.cs"
    data_types_path.write_text("public static class H8DataMonolithTypes { }\n", encoding="utf-8")

    compiler_path = root / "Assets" / "_Project" / "Scripts" / "Editor" / "DataMonolith" / "H8DataMonolithCompiler.cs"
    compiler_path.parent.mkdir(parents=True, exist_ok=True)
    compiler_path.write_text(
        "public static class H8DataMonolithCompiler\n"
        "{\n"
        "    public static void BakeFromCommandLine() { }\n"
        "    public static bool BakeAll() => true;\n"
        "    private static void TryRunAppliedLoreImporter() { }\n"
        "    private static void TryRunAppliedLoreRouteCardExporter() { }\n"
        "}\n",
        encoding="utf-8",
    )

    fuzzer_path = root / "Assets" / "_Project" / "Scripts" / "Editor" / "DataMonolith" / "H8DataMonolithCorruptionFuzzer.cs"
    fuzzer_path.write_text("public static class H8DataMonolithCorruptionFuzzer { }\n", encoding="utf-8")

    if include_load_stress_probe:
        (cli_dir / "DataMonolithLoadStressProbe.cs").write_text(
            "internal static class DataMonolithLoadStressProbe\n"
            "{\n"
            "    public static bool Run(string projectRoot) => true;\n"
            "    private static void Probe()\n"
            "    {\n"
            "        NativeMemory.AlignedAlloc;\n"
            "        NativeMemory.AlignedFree;\n"
            "        TryReadFileToNative();\n"
            "        ValidateResidentBlob();\n"
            "        badChecksumRejected = true;\n"
            "        badOffsetRejected = true;\n"
            "        validationAllocatedBytes = 0;\n"
            "    }\n"
            "}\n",
            encoding="utf-8",
        )

    (cli_dir / "DataMonolithFailClosedProbe.cs").write_text(
        "internal static class DataMonolithFailClosedProbe\n"
        "{\n"
        "    private const int CaseCount = 13;\n"
        "    public static bool Run(string projectRoot) => true;\n"
        "    private static void Probe()\n"
        "    {\n"
        "        RunTruncatedCase(\"truncated_blob\");\n"
        "        MutatePayloadByte();\n"
        "        ValidateResidentBlob();\n"
        "        validationAllocated = 0;\n"
        "        bool passed = validationPasses == ValidationIterations;\n"
        "    }\n"
        "}\n",
        encoding="utf-8",
    )

    (cli_dir / "DataMonolithPlayerParserAbsenceProbe.cs").write_text(
        "internal static class DataMonolithPlayerParserAbsenceProbe\n"
        "{\n"
        "    public static bool Run(string projectRoot) => true;\n"
        "    private static void Probe()\n"
        "    {\n"
        "        Scan(projectRoot, developmentBuild: false);\n"
        "        Scan(projectRoot, developmentBuild: true);\n"
        "        IsPlayerLineActive();\n"
        "        IsAllowedRuntimePersistencePath();\n"
        "        PASS_PLAYER_STATIC_CONFIG_PARSER_ABSENCE.ToString();\n"
        "        DirectFileStreamReadByteCount = 0;\n"
        "    }\n"
        "}\n",
        encoding="utf-8",
    )

    (cli_dir / "DataMonolithSourceInventoryProbe.cs").write_text(
        "internal static class DataMonolithSourceInventoryProbe\n"
        "{\n"
        "    private const string Schema = \"DATA_MONOLITH_SOURCE_INVENTORY\";\n"
        "    private const string BlobRelativePath = \"Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin\";\n"
        "    public static bool Run(string projectRoot) => true;\n"
        "    private static void Probe()\n"
        "    {\n"
        "        ReadBlobInventory();\n"
        "        BuildCsvInventory();\n"
        "        HeaderValid = true;\n"
        "        SectionTableValid = true;\n"
        "    }\n"
        "}\n",
        encoding="utf-8",
    )


def write_pda_streamer_source(root: Path, capacity: int, word_count: int = 16) -> None:
    path = root / "Assets" / "_Project" / "Scripts" / "UI" / "PDAEncyclopediaStreamer.cs"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        "public sealed class PDAEncyclopediaStreamer\n"
        "{\n"
        f"    public const int UnlockBitCount = {capacity};\n"
        f"    public const int UnlockWordCount = {word_count};\n"
        "    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 224)]\n"
        "    public struct EncyclopediaStateDTO\n"
        "    {\n"
        "        public ulong Mask15;\n"
        "    }\n"
        "    private const int PdaLogEventReplayMaxPerFrame = 16;\n"
        "    private void RefreshPdaLogEventReplayState()\n"
        "    {\n"
        "        UIStateData pdaState = UIStateStore.GetPDAState();\n"
        "        uint version = pdaState.Version;\n"
        "    }\n"
        "    private void ReplayPersistedPdaLogEvents()\n"
        "    {\n"
        "        if (openDataMonolithAppliedLoreOnEnable && !_dataMonolithMetadataSeeded) return;\n"
        "        UIStateStore.TryGetPDALogEventHash(0, out uint eventHash);\n"
        "        if (!TryResolveLorePayloadForUnlock(eventHash)) return;\n"
        "        UnlockEntry(eventHash, default, 0u, 0u, false, validatePayload: false, wasNewUnlock: out _);\n"
        "        TryQueuePdaLogEventSelection(eventHash);\n"
        "    }\n"
        "}\n",
        encoding="utf-8",
    )


def write_pda_logbook_bridge_sources(
    root: Path,
    include_clear_history: bool = True,
    include_data_log_fallback: bool = True,
    include_data_log_detail_refresh: bool = True,
    include_replay_missing_payload_fault: bool = True,
    include_blackbox_write_failure_visible: bool = True,
    include_blackbox_write_failure_requeue: bool = False,
    include_rollback_ring_clear: bool = True,
) -> None:
    pda_streamer_path = root / "Assets" / "_Project" / "Scripts" / "UI" / "PDAEncyclopediaStreamer.cs"
    pda_streamer_path.parent.mkdir(parents=True, exist_ok=True)
    replay_missing_payload_block = (
        "        if (!TryResolveLorePayloadForUnlock(eventHash))\n"
        "        {\n"
        "            RejectLoreHash(eventHash);\n"
        "            continue;\n"
        "        }\n"
        if include_replay_missing_payload_fault
        else "        if (!TryResolveLorePayloadForUnlock(eventHash)) continue;\n"
    )
    blackbox_failure_block = (
        "    private const uint FaultBlackBoxWrite = 0x42424457u;\n"
        "    private const string BlackBoxDumpFileName = \"Dump_PDAEncyclopediaStreamer_BlackBox.bin\";\n"
        "    private uint _pendingTelemetryFaultHash;\n"
        "    private uint _pendingBlackBoxFaultHash;\n"
        "    private void MarkBlackBoxWriteFailure()\n"
        "    {\n"
        "        _pendingTelemetryFaultHash = FaultBlackBoxWrite;\n"
        "        _pendingBlackBoxFaultHash = FaultBlackBoxWrite;\n"
        f"{'        QueueBlackBoxDump();\n' if include_blackbox_write_failure_requeue else ''}"
        "    }\n"
        "    private bool DumpBlackBox()\n"
        "    {\n"
        "        try\n"
        "        {\n"
        "            WriteBlackBoxDump(Path.Combine(directory, BlackBoxDumpFileName));\n"
        "            return true;\n"
        "        }\n"
        "        catch (IOException)\n"
        "        {\n"
        "        }\n"
        "        MarkBlackBoxWriteFailure();\n"
        "        return false;\n"
        "    }\n"
        if include_blackbox_write_failure_visible
        else
        "    private const string BlackBoxDumpFileName = \"Dump_PDAEncyclopediaStreamer_BlackBox.bin\";\n"
        "    private bool DumpBlackBox()\n"
        "    {\n"
        "        try\n"
        "        {\n"
        "            WriteBlackBoxDump(Path.Combine(directory, BlackBoxDumpFileName));\n"
        "        }\n"
        "        catch (IOException)\n"
        "        {\n"
        "        }\n"
        "        return true;\n"
        "    }\n"
    )
    pda_streamer_path.write_text(
        "public sealed class PDAEncyclopediaStreamer\n"
        "{\n"
        "    private const int PdaLogEventReplayMaxPerFrame = 16;\n"
        "    private void RefreshPdaLogEventReplayState()\n"
        "    {\n"
        "        UIStateData pdaState = UIStateStore.GetPDAState();\n"
        "        uint version = pdaState.Version;\n"
        "    }\n"
        "    private void ReplayPersistedPdaLogEvents()\n"
        "    {\n"
        "        if (openDataMonolithAppliedLoreOnEnable && !_dataMonolithMetadataSeeded) return;\n"
        "        UIStateStore.TryGetPDALogEventHash(0, out uint eventHash);\n"
        f"{replay_missing_payload_block}"
        "        UnlockEntry(eventHash, default, 0u, 0u, false, validatePayload: false, wasNewUnlock: out _);\n"
        "        TryQueuePdaLogEventSelection(eventHash);\n"
        "    }\n"
        "    private void RejectLoreHash(uint hash) { }\n"
        f"{blackbox_failure_block}"
        "}\n",
        encoding="utf-8",
    )

    ui_state_path = root / "Assets" / "_Project" / "Scripts" / "Core" / "UIStateStore.cs"
    ui_state_path.parent.mkdir(parents=True, exist_ok=True)
    if include_clear_history:
        rollback_body = (
            "    public static bool TryRollbackPDAState(int framesBack)\n"
            "    {\n"
            "        UIStateData restored = default;\n"
            "        for (int i = 0; i < _pdaLogEventHashes.Length; i++)\n"
            "            _pdaLogEventHashes[i] = 0u;\n"
            "        for (int i = 0; i < _pdaLogEventTimestamps.Length; i++)\n"
            "            _pdaLogEventTimestamps[i] = 0f;\n"
            "        _pdaLogWriteIndex = 0;\n"
            "        _pdaLogCount = 0;\n"
            "        restored.LogEntryCount = 0u;\n"
            "        restored.LatestLogEventHash = 0u;\n"
            "        return true;\n"
            "    }\n"
            if include_rollback_ring_clear
            else
            "    public static bool TryRollbackPDAState(int framesBack)\n"
            "    {\n"
            "        UIStateData restored = default;\n"
            "        return true;\n"
            "    }\n"
        )
        ui_state_text = (
            "public static class UIStateStore\n"
            "{\n"
            "    internal static void ClearPDALogEventHistory()\n"
            "    {\n"
            "        _pdaLogWriteIndex = 0;\n"
            "        _pdaLogCount = 0;\n"
            "        state.LatestLogEventHash = 0u;\n"
            "    }\n"
            f"{rollback_body}"
            "}\n"
        )
    else:
        ui_state_text = "public static class UIStateStore { }\n"
    ui_state_path.write_text(ui_state_text, encoding="utf-8")

    logbook_path = root / "Assets" / "_Project" / "Scripts" / "PDA" / "PDALogbookManager.cs"
    logbook_path.parent.mkdir(parents=True, exist_ok=True)
    clear_call = "        UIStateStore.ClearPDALogEventHistory();\n" if include_clear_history else ""
    logbook_path.write_text(
        "public sealed class PDALogbookManager\n"
        "{\n"
        "    public void LoadFromSaveData(SaveData data)\n"
        "    {\n"
        f"{clear_call}"
        "        UIStateStore.AppendPDALogEventHash(1u, 0f);\n"
        "        PDAEvents.TryRaiseLogbookChanged(0, 0u);\n"
        "    }\n"
        "}\n",
        encoding="utf-8",
    )

    data_log_path = root / "Assets" / "_Project" / "Scripts" / "UI" / "PDADataLogTab.cs"
    data_log_path.parent.mkdir(parents=True, exist_ok=True)
    if include_data_log_fallback:
        detail_refresh_block = (
            "            RefreshDetail();\n"
            "            RefreshPlayButton();\n"
            if include_data_log_detail_refresh
            else ""
        )
        data_log_text = (
            "public sealed class PDADataLogTab\n"
            "{\n"
            "    private uint _observedPdaLogVersion;\n"
            "    private uint _observedPdaLogCount;\n"
            "    private uint _observedPdaLatestLogHash;\n"
            "    private bool _visualLateFrameDirty;\n"
            "    private bool _pdaEventsRegistered;\n"
            "    private void TryRegisterPDAEvents()\n"
            "    {\n"
            "        _pdaEventsRegistered = PDAEvents.TryRegister(this);\n"
            "    }\n"
            "    private void ResetObservedPdaLogState()\n"
            "    {\n"
            "        _observedPdaLogVersion = 0u;\n"
            "        _observedPdaLogCount = 0u;\n"
            "        _observedPdaLatestLogHash = 0u;\n"
            "    }\n"
            "    private void RefreshEventSourcedLogStateFromUIStore()\n"
            "    {\n"
            "        UIStateData pdaState = UIStateStore.GetPDAState();\n"
            "        if (_observedPdaLogVersion == pdaState.Version &&\n"
            "            _observedPdaLogCount == pdaState.LogEntryCount &&\n"
            "            _observedPdaLatestLogHash == pdaState.LatestLogEventHash) return;\n"
            "        UIStateStore.TryGetPDALogEvent(0, out uint latestHash, out float timestampSeconds);\n"
            "        _visualLateFrameDirty = true;\n"
            "    }\n"
            "    public void LateFrameTick()\n"
            "    {\n"
            "        RefreshEventSourcedLogStateFromUIStore();\n"
            "        if (_localizedPresentationDirty)\n"
            "        {\n"
            "            RefreshList();\n"
            "            RefreshDetail();\n"
            "            RefreshPlayButton();\n"
            "            _dirty = false;\n"
            "        }\n"
            "        else if (_dirty)\n"
            "        {\n"
            "            RefreshList();\n"
            f"{detail_refresh_block}"
            "            _dirty = false;\n"
            "        }\n"
            "    }\n"
            "    private void RefreshList() { }\n"
            "    private void RefreshDetail() { }\n"
            "    private void RefreshPlayButton() { }\n"
            "}\n"
        )
    else:
        data_log_text = "public sealed class PDADataLogTab { }\n"
    data_log_path.write_text(data_log_text, encoding="utf-8")


def write_terminal_preview_consumer_source(root: Path, include_drop_visibility: bool = True) -> None:
    path = root / "Assets" / "_Project" / "Scripts" / "UI" / "TerminalOS" / "TerminalOsRuntime.cs"
    path.parent.mkdir(parents=True, exist_ok=True)
    drop_expression = (
        "SignalBus<AppliedLoreTerminalPreviewSignal>.DroppedLastFlush > 0\n"
        "                ? FaultAppliedLorePreviewDrop\n"
        "                : 0u"
        if include_drop_visibility
        else "0u"
    )
    path.write_text(
        "public sealed class TerminalOsRuntime\n"
        "{\n"
        "    private const uint FaultAppliedLorePreviewMiss = 1u << 7;\n"
        "    private const uint FaultAppliedLorePreviewDrop = 1u << 8;\n"
        "    private uint _lastFaultFlags;\n"
        "    public void LateFrameTick()\n"
        "    {\n"
        "        int ownerFrame = 0;\n"
        "        int dirtyCount = 0;\n"
        "        int dispatchedCount = 0;\n"
        "        uint terminalPreviewFaultFlags = ConsumeAppliedLoreTerminalPreviewSignals();\n"
        "        uint faultFlags = _lastFaultFlags | terminalPreviewFaultFlags;\n"
        "        if (faultFlags != 0u) QueueTerminalBlackBoxDump(faultFlags);\n"
        "        RecordTelemetry(ownerFrame, dirtyCount, dispatchedCount, faultFlags);\n"
        "    }\n"
        "    private uint ConsumeAppliedLoreTerminalPreviewSignals()\n"
        "    {\n"
        f"        uint faultFlags = {drop_expression};\n"
        "        return faultFlags;\n"
        "    }\n"
        "    private void QueueTerminalBlackBoxDump(uint faultFlags) { }\n"
        "    private void RecordTelemetry(int frame, int dirtyCount, int dispatchedCount, uint faultFlags) { }\n"
        "}\n",
        encoding="utf-8",
    )


def write_pda_lore_signal_consumer_source(
    root: Path,
    include_typed_signal_drop_visibility: bool = True,
    include_queue_refusal_visibility: bool = True,
    include_drop_visibility: bool = True,
    include_counter_reset_guard: bool = True,
) -> None:
    path = root / "Assets" / "_Project" / "Scripts" / "UI" / "PDAEncyclopediaStreamer.cs"
    path.parent.mkdir(parents=True, exist_ok=True)

    def observed_counter_block(counter_name: str, source_expression: str, observed_field: str) -> str:
        if include_counter_reset_guard:
            return (
                f"        int {counter_name} = {source_expression};\n"
                f"        if ({counter_name} < {observed_field})\n"
                "        {\n"
                f"            {observed_field} = {counter_name};\n"
                "        }\n"
                f"        else if ({counter_name} != {observed_field})\n"
                "        {\n"
                f"            {observed_field} = {counter_name};\n"
                "            MarkTransientFault(FaultPdaEventSignalDrop);\n"
                "        }\n"
                "\n"
            )

        return (
            f"        int {counter_name} = {source_expression};\n"
            f"        if ({counter_name} != {observed_field})\n"
            "        {\n"
            f"            {observed_field} = {counter_name};\n"
            "            MarkTransientFault(FaultPdaEventSignalDrop);\n"
            "        }\n"
            "\n"
        )

    typed_signal_drop_block = (
        observed_counter_block(
            "pdaEventTypedSignalDrops",
            "PDAEvents.DroppedTypedSignalCount",
            "_observedPdaEventTypedSignalDropCount",
        )
        if include_typed_signal_drop_visibility
        else ""
    )
    queue_refusal_block = (
        observed_counter_block(
            "pdaEventQueueRefusals",
            "PDAEvents.RefusedQueuedEventCount",
            "_observedPdaEventQueueRefusalCount",
        )
        + observed_counter_block(
            "pdaEventListenerRegistrationRefusals",
            "PDAEvents.RefusedListenerRegistrationCount",
            "_observedPdaEventListenerRegistrationRefusalCount",
        )
        if include_queue_refusal_visibility
        else ""
    )
    drop_block = (
        "        if (SignalBus<PDAEventPayload>.DroppedLastFlush > 0)\n"
        "            MarkTransientFault(FaultPdaEventSignalDrop);\n"
        "\n"
        "        if (SignalBus<ScanCompleteSignal>.DroppedLastFlush > 0 ||\n"
        "            SignalBus<LoreFragmentScannedSignal>.DroppedLastFlush > 0)\n"
        "        {\n"
        "            MarkTransientFault(FaultLoreSignalDrop);\n"
        "        }\n"
        if include_drop_visibility
        else ""
    )
    path.write_text(
        "public sealed class PDAEncyclopediaStreamer\n"
        "{\n"
        "    private const uint FaultLoreSignalDrop = 0x4C445250u;\n"
        "    private const uint FaultPdaEventSignalDrop = 0x50445250u;\n"
        "    private int _observedPdaEventTypedSignalDropCount;\n"
        "    private int _observedPdaEventQueueRefusalCount;\n"
        "    private int _observedPdaEventListenerRegistrationRefusalCount;\n"
        "    private uint _lastFaultHash;\n"
        "    public void LateFrameTick()\n"
        "    {\n"
        "        RecordTelemetry(0u, 0L, 0L, faultHash: ConsumeTelemetryFaultHash());\n"
        "        uint telemetryFaultHash = ConsumeTelemetryFaultHash();\n"
        "        RecordTelemetry(charsRenderedThisFrame, decodeTicks, canvasTicks, unlockedCountSnapshot, hasRuntimeStateSnapshot, telemetryFaultHash);\n"
        "    }\n"
        "    private void ConsumeScanSignals()\n"
        "    {\n"
        f"{typed_signal_drop_block}"
        f"{queue_refusal_block}"
        f"{drop_block}"
        "    }\n"
        "    private void MarkTransientFault(uint faultHash) { }\n"
        "    private uint ConsumeTelemetryFaultHash() => 0u;\n"
        "    private uint ResolveBlackBoxFaultHash() => 0u;\n"
        "    private void RecordTelemetry(uint charsRenderedThisFrame, long decodeTicks, long canvasTicks, uint unlockedCountSnapshot = 0u, bool hasRuntimeStateSnapshot = false, uint faultHash = 0u)\n"
        "    {\n"
        "        entry.FaultHash = faultHash != 0u ? faultHash : _lastFaultHash;\n"
        "    }\n"
        "    private void WriteBlackBoxDump(Span<byte> header)\n"
        "    {\n"
        "        WriteUIntLittleEndian(header.Slice(8, 4), ResolveBlackBoxFaultHash());\n"
        "    }\n"
        "}\n",
        encoding="utf-8",
    )


def write_pda_event_producer_source(
    root: Path,
    include_queue_refusal_visibility: bool = True,
    include_dedup_as_success: bool = True,
    include_dedup_before_capacity: bool = True,
    include_play_mode_guards: bool = True,
) -> None:
    path = root / "Assets" / "_Project" / "Scripts" / "PlayerPDA.cs"
    path.parent.mkdir(parents=True, exist_ok=True)
    register_play_guard = "        if (listener == null || !Application.isPlaying) return false;\n" if include_play_mode_guards else ""
    enqueue_play_guard = "        if (!Application.isPlaying) return false;\n" if include_play_mode_guards else ""
    duplicate_branch = (
        "        if (dedupKey != 0UL && ContainsDedupKey(dedupKey))\n"
        f"            return {'true' if include_dedup_as_success else 'false'};\n"
    )
    capacity_branch = (
        "        if (isFull)\n"
        "        {\n"
        "            s_x001PDAEventsQueueRefusalCount++;\n"
        "            return false;\n"
        "        }\n"
    )
    registration_branch = (
        "        if (dedupKey != 0UL && !TryRegisterDedupKey(dedupKey))\n"
        "            return true;\n"
    )
    enqueue_body = (
        f"{duplicate_branch}{capacity_branch}{registration_branch}"
        if include_dedup_before_capacity
        else
        f"{capacity_branch}{duplicate_branch}{registration_branch}"
    )
    refusal_symbols = (
        "    private static int s_x001PDAEventsQueueRefusalCount;\n"
        "    private static int s_x001PDAEventsListenerRegistrationRefusalCount;\n"
        "    internal static int DroppedTypedSignalCount => s_x001PDAEventsSignalPushDropCount;\n"
        "    internal static int RefusedQueuedEventCount => s_x001PDAEventsQueueRefusalCount;\n"
        "    internal static int RefusedListenerRegistrationCount => s_x001PDAEventsListenerRegistrationRefusalCount;\n"
        "    internal static bool TryRegister(IPDAEventListener listener)\n"
        "    {\n"
        f"{register_play_guard}"
        "        s_x001PDAEventsListenerRegistrationRefusalCount++;\n"
        "        return false;\n"
        "    }\n"
        "    private static bool Enqueue()\n"
        "    {\n"
        f"{enqueue_play_guard}"
        f"{enqueue_body}"
        "        return SignalBus<PDAEventPayload>.TryPushTracked(default, ref s_x001PDAEventsSignalPushDropCount);\n"
        "    }\n"
        "    private static bool ContainsDedupKey(ulong dedupKey) => false;\n"
        "    private static bool TryRegisterDedupKey(ulong dedupKey) => true;\n"
        if include_queue_refusal_visibility
        else (
            "    internal static bool TryRegister(IPDAEventListener listener)\n"
            "    {\n"
            f"{register_play_guard}"
            "        return true;\n"
            "    }\n"
            "    internal static int DroppedTypedSignalCount => s_x001PDAEventsSignalPushDropCount;\n"
            "    private static bool Enqueue()\n"
            "    {\n"
            f"{enqueue_play_guard}"
            "        return SignalBus<PDAEventPayload>.TryPushTracked(default, ref s_x001PDAEventsSignalPushDropCount);\n"
            "    }\n"
        )
    )
    path.write_text(
        "public static class PDAEvents\n"
        "{\n"
        "    private static int s_x001PDAEventsSignalPushDropCount;\n"
        f"{refusal_symbols}"
        "}\n",
        encoding="utf-8",
    )


def binding_row(packet_id: str) -> dict[str, str]:
    packet_hash = fnv1a32(packet_id)
    return {
        "packet_id": packet_id,
        "packet_hash_hex": f"0x{packet_hash:08X}",
        "packet_hash_uint": str(packet_hash),
        "primary_component": "NarrativeDiscovery",
        "primary_field": "appliedLorePacketHash",
        "suggested_world_target": "poi.test",
        "unlock_moment": "test",
    }


def scene_binding_row(packet_id: str) -> dict[str, str]:
    packet_hash = fnv1a32(packet_id)
    return {
        "packet_id": packet_id,
        "packet_hash_hex": f"0x{packet_hash:08X}",
        "packet_hash_decimal": str(packet_hash),
        "authoring_component": "ScannableFragment",
        "serialized_field": "appliedLoreFinalPacketHash",
        "primary_target_candidates": "Assets/_Project/Prefabs/Test/PFB_Test.prefab",
        "secondary_target_candidates": "",
        "unity_safe_action": "test action",
        "notes": "test",
    }


def evidence_graph_row(
    packet_id: str,
    *,
    prereq_packet_ids: str = "",
    next_packet_ids: str = "",
) -> dict[str, str]:
    return {
        "packet_id": packet_id,
        "arc_id": "test_arc",
        "depth_band": "0-10m",
        "route_moment": "test",
        "prereq_packet_ids": prereq_packet_ids,
        "next_packet_ids": next_packet_ids,
        "evidence_type": "test",
        "truth_claim": "test",
        "player_decision": "test",
        "spoiler_tier": "0",
        "primary_surface": "scanner",
    }


def navigation_cluster_graph_row(
    packet_id: str,
    *,
    route_moment: str = "cluster_test",
    prereq_packet_ids: str = "",
    next_packet_ids: str = "",
) -> dict[str, str]:
    return {
        "packet_id": packet_id,
        "arc_id": "site_wiki_navigation_clusters",
        "depth_band": "0-10m",
        "route_moment": route_moment,
        "prereq_packet_ids": prereq_packet_ids,
        "next_packet_ids": next_packet_ids,
        "evidence_type": "site cluster",
        "truth_claim": "cluster truth",
        "player_decision": "cluster decision",
        "spoiler_tier": "1",
        "primary_surface": "external_site",
    }


def route_card_row(packet_id: str, route_card_id: str) -> dict[str, str]:
    return {
        "route_card_id": route_card_id,
        "phase_id": "test_phase",
        "depth_min_m": "0",
        "depth_max_m": "10",
        "packet_ids": packet_id,
        "required_packet_ids": "",
        "primary_surface": "scanner",
        "world_object_hint": "poi.test",
        "player_question": "test?",
        "truth_payload": "test",
        "replay_axis": "test",
        "ending_pressure": "none",
    }


def manual_scene_binding_row(packet_id: str) -> dict[str, str]:
    row = scene_binding_row(packet_id)
    row["authoring_component"] = "NarrativeDiscovery"
    row["serialized_field"] = "appliedLorePacketHash"
    row["primary_target_candidates"] = "Assets/_Project/Manual/DiscoveryAnchor.asset"
    return row


def manual_policy_row(packet_id: str) -> dict[str, str]:
    packet_hash = fnv1a32(packet_id)
    return {
        "packet_id": packet_id,
        "packet_hash_hex": f"0x{packet_hash:08X}",
        "packet_hash_decimal": str(packet_hash),
        "manual_policy": "discovery_world_prop_required",
        "required_anchor_type": "marked_world_prop",
        "approved_template_prefab": "",
        "authoring_component": "NarrativeDiscovery",
        "serialized_field": "appliedLorePacketHash",
        "discovery_id": f"disc.{packet_id.lower()}",
        "placement_rule": "manual_test",
        "reason": "test",
    }


def scene_placement_row(packet_id: str) -> dict[str, str]:
    packet_hash = fnv1a32(packet_id)
    return {
        "packet_id": packet_id,
        "packet_hash_hex": f"0x{packet_hash:08X}",
        "packet_hash_decimal": str(packet_hash),
        "scene_path": "Assets/_Project/Scenes/Test.unity",
        "placement_root": "Root",
        "object_name": f"OBJ_{packet_id}",
        "source_prefab": "Assets/_Project/Prefabs/Test/PFB_Test.prefab",
        "authoring_component": "NarrativeDiscovery",
        "serialized_field": "appliedLorePacketHash",
        "discovery_id": f"disc.{packet_id.lower()}",
        "display_name": "Test",
        "local_position": "0|0|0",
        "local_euler": "0|0|0",
        "local_scale": "1|1|1",
        "depth_band": "0-10m",
        "zone_tag": "test",
        "placement_note": "test",
    }


class TestAppliedLoreRuntimeAudit(unittest.TestCase):
    def test_text_integrity_issue_catches_replacement_question_marks(self):
        self.assertTrue(has_text_integrity_issue("L??conomie de route garde HECTON-8 honn?te."))

    def test_text_integrity_issue_ignores_draft_placeholder_marker(self):
        self.assertFalse(has_text_integrity_issue("LOC HOLD: pending native pass."))

    def test_text_integrity_issue_allows_sentence_questions(self):
        self.assertFalse(has_text_integrity_issue("Who pays the braking? The ledger answers later."))

    def test_import_outputs_current_rejects_missing_generated_files(self):
        with temporary_directory() as tmp:
            root = Path(tmp)

            with self.assertRaises(AuditFailure) as context:
                validate_import_outputs_current(root)

            self.assertIn("importer outputs are stale or missing", str(context.exception))

    def test_publication_outputs_current_rejects_stale_generated_pages(self):
        stale_stats = SimpleNamespace(
            checked_files=10,
            stale_files=1,
            missing_files=0,
            disabled_generated_pages=0,
            integrity_issues=0,
            sample_issues=("stale: Docs/Lore/AppliedContent/in_game_wiki/en_US/P_TEST.md",),
        )

        with patch("AppliedLoreRuntimeAudit.check_applied_lore_publication_freshness", return_value=stale_stats):
            with self.assertRaises(AuditFailure) as context:
                validate_publication_outputs_current(Path("."))

        self.assertIn("publication pages are stale", str(context.exception))

    def test_publication_outputs_current_rejects_text_integrity_issues(self):
        stale_stats = SimpleNamespace(
            checked_files=10,
            stale_files=0,
            missing_files=0,
            disabled_generated_pages=0,
            integrity_issues=1,
            sample_issues=(
                "integrity: Docs/Lore/AppliedContent/external_site/fr_FR/P_TEST.md: "
                "suspicious_question_mark=double_question_mark",
            ),
        )

        with patch("AppliedLoreRuntimeAudit.check_applied_lore_publication_freshness", return_value=stale_stats):
            with self.assertRaises(AuditFailure) as context:
                validate_publication_outputs_current(Path("."))

        message = str(context.exception)
        self.assertIn("text-corrupt", message)
        self.assertIn("integrity_issues=1", message)

    def test_native_localization_gate_counts_draft_rows_without_strict_gate(self):
        rows = [
            packet_row("P_READY", locale="en_US"),
            packet_row("P_DRAFT", locale="ru_RU", flags=1),
        ]

        self.assertEqual(validate_native_localization_gate(rows), 1)

    def test_native_localization_gate_strict_rejects_draft_rows(self):
        rows = [
            packet_row("P_READY", locale="en_US"),
            packet_row("P_DRAFT", locale="ru_RU", flags=1),
        ]

        with self.assertRaises(AuditFailure) as context:
            validate_native_localization_gate(rows, strict_native_localization=True)

        message = str(context.exception)
        self.assertIn("native localization incomplete", message)
        self.assertIn("draft_rows=1", message)
        self.assertIn("P_DRAFT/ru_RU", message)
        self.assertIn("AppliedLoreLocalizationDeltaAudit.py", message)

    def test_native_localization_gate_strict_accepts_non_draft_translated_rows(self):
        rows = [
            packet_row(
                "P_READY",
                locale="en_US",
                fields={"title": "Crash Shelf", "scanner": "Signal recovered"},
            ),
            packet_row(
                "P_READY",
                locale="ru_RU",
                fields={"title": "Полка крушения", "scanner": "Сигнал восстановлен"},
            ),
        ]

        self.assertEqual(validate_native_localization_gate(rows, strict_native_localization=True), 0)

    def test_native_localization_gate_strict_rejects_non_draft_english_clone_rows(self):
        rows = [
            packet_row(
                "P_CLONE",
                locale="en_US",
                fields={"title": "Crash Shelf", "scanner": "Signal recovered"},
            ),
            packet_row(
                "P_CLONE",
                locale="ru_RU",
                fields={"title": "Crash Shelf", "scanner": "Signal recovered"},
            ),
        ]

        with self.assertRaises(AuditFailure) as context:
            validate_native_localization_gate(rows, strict_native_localization=True)

        message = str(context.exception)
        self.assertIn("english_clone_rows=1", message)
        self.assertIn("P_CLONE/ru_RU", message)
        self.assertIn("AppliedLoreLocalizationDeltaAudit.py", message)

    def test_scene_placement_runtime_coverage_reports_uncovered_rows_without_strict_gate(self):
        stats = SimpleNamespace(rows=7)

        uncovered = validate_scene_placement_runtime_coverage(
            Path("unused"),
            stats,
            scene_placement_serialized_rows=2,
            scene_placement_covered_rows=4,
        )

        self.assertEqual(uncovered, 3)

    def test_scene_placement_runtime_coverage_strict_rejects_uncovered_rows(self):
        stats = SimpleNamespace(rows=7)

        with self.assertRaises(AuditFailure) as context:
            validate_scene_placement_runtime_coverage(
                Path("unused"),
                stats,
                scene_placement_serialized_rows=2,
                scene_placement_covered_rows=4,
                strict_scene_placement=True,
            )

        message = str(context.exception)
        self.assertIn("scene placement coverage incomplete", message)
        self.assertIn("uncovered=3", message)
        self.assertIn("Apply Applied Lore Scene Placement Plan", message)
        self.assertIn("ApplyScenePlacementPlanFromCommandLine", message)
        self.assertIn("AppliedLoreScenePlacementDeltaAudit.py", message)
        self.assertIn("--json", message)

    def test_production_gate_collects_all_blockers(self):
        with patch("AppliedLoreRuntimeAudit.validate_import_outputs_current") as import_mock, \
            patch("AppliedLoreRuntimeAudit.validate_publication_outputs_current") as publication_mock, \
            patch(
                "AppliedLoreRuntimeAudit.run",
                side_effect=(
                    AuditFailure("draft_rows=3"),
                    AuditFailure("uncovered=2"),
                    AuditFailure("static_data.h8bin stale"),
                ),
            ) as run_mock:
            with self.assertRaises(AuditFailure) as context:
                run_production_gate(Path("unused"))

        message = str(context.exception)
        self.assertIn("AppliedLore production gate blocked", message)
        self.assertIn("native localization: draft_rows=3", message)
        self.assertIn("scene placement: uncovered=2", message)
        self.assertIn("baked runtime artifact: static_data.h8bin stale", message)
        self.assertEqual(import_mock.call_count, 1)
        self.assertEqual(publication_mock.call_count, 1)
        self.assertEqual(run_mock.call_count, 3)
        for call in run_mock.call_args_list:
            self.assertFalse(call.kwargs["validate_freshness"])

    def test_production_gate_passes_only_after_all_subchecks_pass(self):
        with patch("AppliedLoreRuntimeAudit.validate_import_outputs_current") as import_mock, \
            patch("AppliedLoreRuntimeAudit.validate_publication_outputs_current") as publication_mock, \
            patch("AppliedLoreRuntimeAudit.run", return_value="ok") as run_mock:
            result = run_production_gate(Path("unused"))

        self.assertIn("AppliedLore production gate OK", result)
        self.assertEqual(import_mock.call_count, 1)
        self.assertEqual(publication_mock.call_count, 1)
        self.assertEqual(run_mock.call_count, 3)
        for call in run_mock.call_args_list:
            self.assertFalse(call.kwargs["validate_freshness"])

    def test_production_gate_reports_freshness_blockers_once(self):
        with patch(
            "AppliedLoreRuntimeAudit.validate_import_outputs_current",
            side_effect=AuditFailure("import stale"),
        ) as import_mock, patch(
            "AppliedLoreRuntimeAudit.validate_publication_outputs_current",
            side_effect=AuditFailure("publication stale"),
        ) as publication_mock, patch("AppliedLoreRuntimeAudit.run", return_value="ok") as run_mock:
            with self.assertRaises(AuditFailure) as context:
                run_production_gate(Path("unused"))

        message = str(context.exception)
        self.assertIn("source import freshness: import stale", message)
        self.assertIn("publication freshness: publication stale", message)
        self.assertEqual(import_mock.call_count, 1)
        self.assertEqual(publication_mock.call_count, 1)
        self.assertEqual(run_mock.call_count, 3)

    def test_production_gate_json_payload_keeps_structured_failures(self):
        with patch("AppliedLoreRuntimeAudit.validate_import_outputs_current") as import_mock, \
            patch("AppliedLoreRuntimeAudit.validate_publication_outputs_current") as publication_mock, \
            patch(
                "AppliedLoreRuntimeAudit.run",
                side_effect=(
                    AuditFailure("draft_rows=3"),
                    AuditFailure("uncovered=2"),
                    AuditFailure("static_data.h8bin stale"),
                ),
            ) as run_mock:
            payload = production_gate_json_payload(Path("unused"))

        self.assertFalse(payload["clean"])
        self.assertEqual(payload["failure_count"], 3)
        self.assertEqual(
            [failure["gate"] for failure in payload["failures"]],
            ["native localization", "scene placement", "baked runtime artifact"],
        )
        self.assertIn("draft_rows=3", payload["failures"][0]["message"])
        self.assertIn(
            "AppliedLoreLocalizationDeltaAudit.py",
            payload["failures"][0]["diagnostic_commands"][0],
        )
        self.assertIn(
            "AppliedLoreScenePlacementDeltaAudit.py",
            payload["failures"][1]["diagnostic_commands"][0],
        )
        self.assertIn(
            "AppliedLoreBlobDeltaAudit.py",
            payload["failures"][2]["diagnostic_commands"][0],
        )
        self.assertEqual(import_mock.call_count, 1)
        self.assertEqual(publication_mock.call_count, 1)
        self.assertEqual(run_mock.call_count, 3)

    def test_cli_production_gate_json_writes_stdout_and_failure_exit_code(self):
        stdout = io.StringIO()
        stderr = io.StringIO()
        with patch(
            "AppliedLoreRuntimeAudit.production_gate_json_payload",
            return_value={
                "clean": False,
                "failure_count": 1,
                "failures": [{"gate": "scene placement", "message": "uncovered=344"}],
            },
        ), patch("sys.argv", ["AppliedLoreRuntimeAudit.py", "--production-gate", "--json"]), \
            contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            exit_code = main()

        self.assertEqual(exit_code, 1)
        self.assertEqual(stderr.getvalue(), "")
        payload = json.loads(stdout.getvalue())
        self.assertFalse(payload["clean"])
        self.assertEqual(payload["failures"][0]["gate"], "scene placement")

    def test_data_monolith_bake_cli_bridge_accepts_runnable_contract(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_data_monolith_bake_cli_bridge_sources(root)

            validate_data_monolith_bake_cli_bridge(root)

    def test_data_monolith_bake_cli_bridge_accepts_formatted_exception_boundary(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_data_monolith_bake_cli_bridge_sources(root)
            program_path = root / "Tools" / "DataMonolithBakeCli" / "Program.cs"
            program_path.write_text(
                program_path.read_text(encoding="utf-8").replace(
                    "catch (Exception exception)",
                    "catch   (   Exception   exception   )",
                ),
                encoding="utf-8",
            )

            validate_data_monolith_bake_cli_bridge(root)

    def test_data_monolith_bake_cli_bridge_rejects_missing_project_binding(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_data_monolith_bake_cli_bridge_sources(root, include_project=False)

            with self.assertRaises(AuditFailure) as context:
                validate_data_monolith_bake_cli_bridge(root)

            self.assertIn("DataMonolithBakeCli.csproj", str(context.exception))

    def test_data_monolith_bake_cli_bridge_rejects_missing_runtimeconfig_contract(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_data_monolith_bake_cli_bridge_sources(root)
            project_path = root / "Tools" / "DataMonolithBakeCli" / "DataMonolithBakeCli.csproj"
            project_path.write_text(
                project_path.read_text(encoding="utf-8").replace(
                    "    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>\n",
                    "",
                ),
                encoding="utf-8",
            )

            with self.assertRaises(AuditFailure) as context:
                validate_data_monolith_bake_cli_bridge(root)

            self.assertIn("runtimeconfig.json", str(context.exception))

    def test_data_monolith_bake_cli_bridge_rejects_ignored_cli_project(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_data_monolith_bake_cli_bridge_sources(root)
            (root / ".gitignore").write_text("*.csproj\n", encoding="utf-8")

            with self.assertRaises(AuditFailure) as context:
                validate_data_monolith_bake_cli_bridge(root)

            self.assertIn("!Tools/DataMonolithBakeCli/DataMonolithBakeCli.csproj", str(context.exception))

    def test_data_monolith_bake_cli_bridge_rejects_detached_world_impact_layout(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_data_monolith_bake_cli_bridge_sources(root, include_world_impact_compile=False)

            with self.assertRaises(AuditFailure) as context:
                validate_data_monolith_bake_cli_bridge(root)

            self.assertIn("H8AppliedLoreWorldImpactRecord.cs", str(context.exception))

    def test_data_monolith_bake_cli_bridge_rejects_detached_unity_xxhash_source(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_data_monolith_bake_cli_bridge_sources(root, include_xxhash_source=False)

            with self.assertRaises(AuditFailure) as context:
                validate_data_monolith_bake_cli_bridge(root)

            self.assertIn("Unity.Collections/xxHash3.cs", str(context.exception))

    def test_data_monolith_bake_cli_bridge_rejects_stale_xxhash_include_path(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_data_monolith_bake_cli_bridge_sources(root, stale_xxhash_include=True)

            with self.assertRaises(AuditFailure) as context:
                validate_data_monolith_bake_cli_bridge(root)

            self.assertIn("xxHash3 include does not exist", str(context.exception))

    def test_data_monolith_bake_cli_bridge_rejects_stale_compile_include_path(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_data_monolith_bake_cli_bridge_sources(root, stale_compiler_include=True)

            with self.assertRaises(AuditFailure) as context:
                validate_data_monolith_bake_cli_bridge(root)

            self.assertIn("compile include does not exist", str(context.exception))
            self.assertIn("MissingH8DataMonolithCompiler.cs", str(context.exception))

    def test_data_monolith_bake_cli_bridge_rejects_missing_load_stress_probe(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_data_monolith_bake_cli_bridge_sources(root, include_load_stress_probe=False)

            with self.assertRaises(AuditFailure) as context:
                validate_data_monolith_bake_cli_bridge(root)

            self.assertIn("DataMonolithLoadStressProbe.cs", str(context.exception))

    def test_data_monolith_bake_cli_bridge_rejects_missing_unity_editor_stubs(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_data_monolith_bake_cli_bridge_sources(root, include_unity_editor_stubs=False)

            with self.assertRaises(AuditFailure) as context:
                validate_data_monolith_bake_cli_bridge(root)

            self.assertIn("UnityEditorStubs.cs", str(context.exception))

    def test_data_monolith_bake_cli_bridge_rejects_cli_without_failure_codes(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_data_monolith_bake_cli_bridge_sources(root, include_cli_failure_codes=False)

            with self.assertRaises(AuditFailure) as context:
                validate_data_monolith_bake_cli_bridge(root)

            self.assertIn("return 1;", str(context.exception))

    def test_data_monolith_bake_cli_bridge_rejects_cli_without_exception_boundary(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_data_monolith_bake_cli_bridge_sources(root, include_cli_exception_boundary=False)

            with self.assertRaises(AuditFailure) as context:
                validate_data_monolith_bake_cli_bridge(root)

            self.assertIn("catch (Exception exception)", str(context.exception))

    def test_data_monolith_bake_cli_bridge_rejects_duplicate_world_impact_runtime_record(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_data_monolith_bake_cli_bridge_sources(root, duplicate_world_impact_in_runtime=True)

            with self.assertRaises(AuditFailure) as context:
                validate_data_monolith_bake_cli_bridge(root)

            self.assertIn("data-contract file", str(context.exception))

    def test_uncovered_scene_placement_samples_skip_noncanonical_packets(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_scene_placement_plan(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "RS001_RS010_scene_placement_plan.csv",
                [scene_placement_row("P_DRAFT_ONLY"), scene_placement_row("P_EXPECTED")],
            )

            samples = collect_uncovered_scene_placement_samples(root, {"P_EXPECTED"})

            self.assertEqual(len(samples), 1)
            self.assertTrue(samples[0].startswith("P_EXPECTED@"))

    def test_terminal_os_scene_runtime_keying_rejects_scene_only_dedupe(self):
        old_editor_source = (
            "private static void EnsureTerminalOsRuntimeForLoadedScenes()\n"
            "{\n"
            "    HashSet<string> processedScenePaths = new HashSet<string>();\n"
            "    processedScenePaths.Add(row.ScenePath);\n"
            "}\n"
            "private static void AssignTerminalPreviewIndices()\n"
            "{\n"
            "    int terminalIndex = 0;\n"
            "    row.TerminalPreviewIndex = terminalIndex++;\n"
            "}\n"
            "private static bool TrySetTerminalOsRuntimeArrays()\n"
            "{\n"
            "    return false;\n"
            "}\n"
        )

        with self.assertRaises(AuditFailure) as context:
            validate_terminal_os_scene_runtime_keying(old_editor_source)

        self.assertIn("scene+placement root", str(context.exception))

    def test_terminal_os_scene_runtime_keying_accepts_runtime_owner_key(self):
        fixed_editor_source = (
            "private static void EnsureTerminalOsRuntimeForLoadedScenes()\n"
            "{\n"
            "    HashSet<string> processedRuntimeKeys = new HashSet<string>();\n"
            "    string runtimeKey = ScenePlacementRuntimeKey(row.ScenePath, row.PlacementRoot);\n"
            "    processedRuntimeKeys.Add(runtimeKey);\n"
            "}\n"
            "private static void AssignTerminalPreviewIndices()\n"
            "{\n"
            "    Dictionary<string, int> terminalIndicesByRuntime = new Dictionary<string, int>();\n"
            "    string runtimeKey = ScenePlacementRuntimeKey(row.ScenePath, row.PlacementRoot);\n"
            "}\n"
            "private static bool TrySetTerminalOsRuntimeArrays()\n"
            "{\n"
            "    HashSet<int> assignedPreviewIndices = new HashSet<int>();\n"
            "    report.TerminalOsRuntimeDuplicatePreviewIndices++;\n"
            "    return assignedPreviewIndices.Count > 0;\n"
            "}\n"
        )

        validate_terminal_os_scene_runtime_keying(fixed_editor_source)

    def test_scene_placement_preflight_rejects_partial_apply_before_hash_validation(self):
        old_editor_source = (
            "private static ScenePlacementReport ApplyScenePlacementPlanToOpenScene()\n"
            "{\n"
            "    GameObject root = FindOrCreatePlacementRoot(scene, row.PlacementRoot, out bool rootCreated);\n"
            "    PrefabUtility.InstantiatePrefab(sourcePrefab, scene);\n"
            "    if (!TryValidateScenePlacementRowsBeforeMutation(rows, knownPacketHashes, ref report))\n"
            "    {\n"
            "        report.PreflightAborted = true;\n"
            "        return report;\n"
            "    }\n"
            "    return report;\n"
            "}\n"
            "private static bool TryValidateScenePlacementRowsBeforeMutation()\n"
            "{\n"
            "    if (!row.IsValid) { report.InvalidRows++; valid = false; }\n"
            "    if (!knownPacketHashes.Contains(row.PacketHashUInt)) { report.UnknownHashes++; valid = false; }\n"
            "    if (!FindLoadedScene(row.ScenePath, out _)) { report.SceneNotLoaded++; valid = false; }\n"
            "    if (AssetDatabase.LoadAssetAtPath<GameObject>(row.SourcePrefab) == null) { report.MissingPrefabs++; valid = false; }\n"
            "    return valid;\n"
            "}\n"
        )

        with self.assertRaises(AuditFailure) as context:
            validate_scene_placement_preflight_abort(old_editor_source)

        self.assertIn("before creating placement roots", str(context.exception))

    def test_scene_placement_preflight_accepts_abort_before_mutation(self):
        fixed_editor_source = (
            "private static ScenePlacementReport ApplyScenePlacementPlanToOpenScene()\n"
            "{\n"
            "    if (!TryValidateScenePlacementRowsBeforeMutation(rows, knownPacketHashes, ref report))\n"
            "    {\n"
            "        report.PreflightAborted = true;\n"
            "        return report;\n"
            "    }\n"
            "    GameObject root = FindOrCreatePlacementRoot(scene, row.PlacementRoot, out bool rootCreated);\n"
            "    PrefabUtility.InstantiatePrefab(sourcePrefab, scene);\n"
            "    return report;\n"
            "}\n"
            "private static bool TryValidateScenePlacementRowsBeforeMutation()\n"
            "{\n"
            "    if (!row.IsValid) { report.InvalidRows++; valid = false; }\n"
            "    if (!knownPacketHashes.Contains(row.PacketHashUInt)) { report.UnknownHashes++; valid = false; }\n"
            "    if (!FindLoadedScene(row.ScenePath, out _)) { report.SceneNotLoaded++; valid = false; }\n"
            "    if (AssetDatabase.LoadAssetAtPath<GameObject>(row.SourcePrefab) == null) { report.MissingPrefabs++; valid = false; }\n"
            "    return valid;\n"
            "}\n"
        )

        validate_scene_placement_preflight_abort(fixed_editor_source)

    def test_scene_placement_preflight_rejects_missing_prefab_existence_check(self):
        broken_editor_source = (
            "private static ScenePlacementReport ApplyScenePlacementPlanToOpenScene()\n"
            "{\n"
            "    if (!TryValidateScenePlacementRowsBeforeMutation(rows, knownPacketHashes, ref report))\n"
            "    {\n"
            "        report.PreflightAborted = true;\n"
            "        return report;\n"
            "    }\n"
            "    GameObject root = FindOrCreatePlacementRoot(scene, row.PlacementRoot, out bool rootCreated);\n"
            "    PrefabUtility.InstantiatePrefab(sourcePrefab, scene);\n"
            "    return report;\n"
            "}\n"
            "private static bool TryValidateScenePlacementRowsBeforeMutation()\n"
            "{\n"
            "    if (!row.IsValid) { report.InvalidRows++; valid = false; }\n"
            "    if (!knownPacketHashes.Contains(row.PacketHashUInt)) { report.UnknownHashes++; valid = false; }\n"
            "    if (!FindLoadedScene(row.ScenePath, out _)) { report.SceneNotLoaded++; valid = false; }\n"
            "    return valid;\n"
            "}\n"
        )

        with self.assertRaises(AuditFailure) as context:
            validate_scene_placement_preflight_abort(broken_editor_source)

        self.assertIn("AssetDatabase.LoadAssetAtPath", str(context.exception))

    def test_scene_placement_commandline_apply_rejects_apply_before_scene_open(self):
        old_editor_source = (
            "public static void ApplyScenePlacementPlanFromCommandLine()\n"
            "{\n"
            "    ScenePlacementReport report = ApplyScenePlacementPlanToOpenScene();\n"
            "    bool scenesOpened = TryOpenScenePlacementPlanScenesForCommandLine();\n"
            "    Debug.Log(report.ToLogLine());\n"
            "    bool success = scenesOpened && !HasScenePlacementApplyFailures(report);\n"
            "    if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);\n"
            "}\n"
            "private static bool TryOpenScenePlacementPlanScenesForCommandLine()\n"
            "{\n"
            "    LoadScenePlacementRows(rows);\n"
            "    if (FindLoadedScene(scenePath, out _)) { return true; }\n"
            "    if (!File.Exists(absoluteScenePath)) { return false; }\n"
            "    OpenSceneMode mode = openedAny ? OpenSceneMode.Additive : OpenSceneMode.Single;\n"
            "    EditorSceneManager.OpenScene(scenePath, mode);\n"
            "    return openedAny;\n"
            "}\n"
            "private static bool HasScenePlacementApplyFailures(ScenePlacementReport report)\n"
            "{\n"
            "    return report.PreflightAborted || report.InvalidRows > 0 || report.DuplicateSceneOwners > 0 ||\n"
            "           report.DuplicateDiscoveryIds > 0 || report.UnknownHashes > 0 || report.SceneNotLoaded > 0 ||\n"
            "           report.MissingPrefabs > 0 || report.Conflicts > 0 || report.UnsupportedRows > 0 ||\n"
            "           report.SaveFailures > 0;\n"
            "}\n"
        )

        with self.assertRaises(AuditFailure) as context:
            validate_scene_placement_commandline_apply(old_editor_source)

        self.assertIn("open plan scenes before applying", str(context.exception))

    def test_scene_placement_commandline_apply_accepts_batch_exit_gate(self):
        fixed_editor_source = (
            "public static void ApplyScenePlacementPlanFromCommandLine()\n"
            "{\n"
            "    try\n"
            "    {\n"
            "    bool scenesOpened = TryOpenScenePlacementPlanScenesForCommandLine();\n"
            "    ScenePlacementReport report = scenesOpened ? ApplyScenePlacementPlanToOpenScene() : new ScenePlacementReport();\n"
            "    Debug.Log(report.ToLogLine());\n"
            "    bool success = scenesOpened && !HasScenePlacementApplyFailures(report);\n"
            "    if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);\n"
            "    }\n"
            "    catch (Exception exception)\n"
            "    {\n"
            "    Debug.LogError(\"[AppliedLoreScenePlacement] Batch scene placement threw: \" + exception.Message);\n"
            "    if (Application.isBatchMode) EditorApplication.Exit(1);\n"
            "    }\n"
            "}\n"
            "private static bool TryOpenScenePlacementPlanScenesForCommandLine()\n"
            "{\n"
            "    LoadScenePlacementRows(rows);\n"
            "    if (FindLoadedScene(scenePath, out _)) { openedAny = true; }\n"
            "    if (!File.Exists(absoluteScenePath)) { return false; }\n"
            "    OpenSceneMode mode = openedAny ? OpenSceneMode.Additive : OpenSceneMode.Single;\n"
            "    EditorSceneManager.OpenScene(scenePath, mode);\n"
            "    return openedAny;\n"
            "}\n"
            "private static bool HasScenePlacementApplyFailures(ScenePlacementReport report)\n"
            "{\n"
            "    return report.PlanRows <= 0 || report.PreflightAborted || report.InvalidRows > 0 || report.DuplicateSceneOwners > 0 ||\n"
            "           report.DuplicateDiscoveryIds > 0 || report.UnknownHashes > 0 || report.SceneNotLoaded > 0 ||\n"
            "           report.MissingPrefabs > 0 || report.Conflicts > 0 || report.UnsupportedRows > 0 ||\n"
            "           report.SaveFailures > 0 || report.TerminalOsRuntimeMissingRenderers > 0 ||\n"
            "           report.TerminalOsRuntimeDuplicatePreviewIndices > 0;\n"
            "}\n"
        )

        validate_scene_placement_commandline_apply(fixed_editor_source)

    def test_scene_placement_commandline_apply_rejects_missing_terminal_runtime_faults(self):
        broken_editor_source = (
            "public static void ApplyScenePlacementPlanFromCommandLine()\n"
            "{\n"
            "    try\n"
            "    {\n"
            "    bool scenesOpened = TryOpenScenePlacementPlanScenesForCommandLine();\n"
            "    ScenePlacementReport report = scenesOpened ? ApplyScenePlacementPlanToOpenScene() : new ScenePlacementReport();\n"
            "    Debug.Log(report.ToLogLine());\n"
            "    bool success = scenesOpened && !HasScenePlacementApplyFailures(report);\n"
            "    if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);\n"
            "    }\n"
            "    catch (Exception exception)\n"
            "    {\n"
            "    Debug.LogError(\"[AppliedLoreScenePlacement] Batch scene placement threw: \" + exception.Message);\n"
            "    if (Application.isBatchMode) EditorApplication.Exit(1);\n"
            "    }\n"
            "}\n"
            "private static bool TryOpenScenePlacementPlanScenesForCommandLine()\n"
            "{\n"
            "    LoadScenePlacementRows(rows);\n"
            "    if (FindLoadedScene(scenePath, out _)) { openedAny = true; }\n"
            "    if (!File.Exists(absoluteScenePath)) { return false; }\n"
            "    OpenSceneMode mode = openedAny ? OpenSceneMode.Additive : OpenSceneMode.Single;\n"
            "    EditorSceneManager.OpenScene(scenePath, mode);\n"
            "    return openedAny;\n"
            "}\n"
            "private static bool HasScenePlacementApplyFailures(ScenePlacementReport report)\n"
            "{\n"
            "    return report.PlanRows <= 0 || report.PreflightAborted || report.InvalidRows > 0 || report.DuplicateSceneOwners > 0 ||\n"
            "           report.DuplicateDiscoveryIds > 0 || report.UnknownHashes > 0 || report.SceneNotLoaded > 0 ||\n"
            "           report.MissingPrefabs > 0 || report.Conflicts > 0 || report.UnsupportedRows > 0 ||\n"
            "           report.SaveFailures > 0;\n"
            "}\n"
        )

        with self.assertRaises(AuditFailure) as context:
            validate_scene_placement_commandline_apply(broken_editor_source)

        self.assertIn("TerminalOsRuntimeMissingRenderers", str(context.exception))

    def test_pda_capacity_covers_imported_applied_lore_packets(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_streamer_source(root, 1024)

            self.assertEqual(validate_pda_runtime_capacity(root, 477), 1024)
            self.assertEqual(pda_unlock_free_slots(1024, 477), 547)

    def test_pda_capacity_rejects_silent_256_packet_cap(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_streamer_source(root, 256, word_count=4)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_runtime_capacity(root, 477)

            self.assertIn("smaller than imported packet count", str(context.exception))

    def test_pda_capacity_rejects_non_power_of_two_hash_mask(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_streamer_source(root, 960, word_count=15)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_runtime_capacity(root, 477)

            self.assertIn("power-of-two", str(context.exception))

    def test_pda_capacity_rejects_word_count_mismatch(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_streamer_source(root, 1024, word_count=8)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_runtime_capacity(root, 477)

            self.assertIn("does not cover capacity", str(context.exception))

    def test_terminal_preview_drop_visibility_accepts_lane_drop_fault_path(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_terminal_preview_consumer_source(root)

            validate_terminal_preview_drop_visibility(root)

    def test_terminal_preview_drop_visibility_rejects_invisible_lane_drop(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_terminal_preview_consumer_source(root, include_drop_visibility=False)

            with self.assertRaises(AuditFailure) as context:
                validate_terminal_preview_drop_visibility(root)

            self.assertIn("DroppedLastFlush", str(context.exception))

    def test_pda_lore_signal_drop_visibility_accepts_lane_drop_fault_path(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_lore_signal_consumer_source(root)

            validate_pda_lore_signal_drop_visibility(root)

    def test_pda_lore_signal_drop_visibility_rejects_invisible_lane_drop(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_lore_signal_consumer_source(root, include_drop_visibility=False)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_lore_signal_drop_visibility(root)

            self.assertIn("DroppedLastFlush", str(context.exception))

    def test_pda_lore_signal_drop_visibility_rejects_invisible_typed_counter_drop(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_lore_signal_consumer_source(root, include_typed_signal_drop_visibility=False)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_lore_signal_drop_visibility(root)

            self.assertIn("DroppedTypedSignalCount", str(context.exception))

    def test_pda_lore_signal_drop_visibility_rejects_counter_reset_false_positive(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_lore_signal_consumer_source(root, include_counter_reset_guard=False)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_lore_signal_drop_visibility(root)

            self.assertIn("pdaEventTypedSignalDrops <", str(context.exception))

    def test_pda_lore_signal_drop_visibility_rejects_invisible_queue_refusal(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_lore_signal_consumer_source(root, include_queue_refusal_visibility=False)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_lore_signal_drop_visibility(root)

            self.assertIn("RefusedQueuedEventCount", str(context.exception))

    def test_pda_event_queue_refusal_visibility_accepts_producer_counter(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_event_producer_source(root)

            validate_pda_event_queue_refusal_visibility(root)

    def test_pda_event_queue_refusal_visibility_rejects_dedup_as_failure(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_event_producer_source(root, include_dedup_as_success=False)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_event_queue_refusal_visibility(root)

            self.assertIn("duplicate suppression", str(context.exception))

    def test_pda_event_queue_refusal_visibility_rejects_capacity_before_dedup(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_event_producer_source(root, include_dedup_before_capacity=False)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_event_queue_refusal_visibility(root)

            self.assertIn("before queue-capacity refusal", str(context.exception))

    def test_pda_event_queue_refusal_visibility_rejects_missing_play_mode_guards(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_event_producer_source(root, include_play_mode_guards=False)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_event_queue_refusal_visibility(root)

            self.assertIn("edit-mode", str(context.exception))

    def test_pda_event_queue_refusal_visibility_rejects_missing_counter(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_event_producer_source(root, include_queue_refusal_visibility=False)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_event_queue_refusal_visibility(root)

            self.assertIn("RefusedQueuedEventCount", str(context.exception))

    def test_pda_event_try_register_lifecycle_rejects_direct_self_register(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            path = root / "Assets" / "_Project" / "Scripts" / "UI" / "BrokenPdaListener.cs"
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(
                "public sealed class BrokenPdaListener\n"
                "{\n"
                "    private bool _registeredPdaEvents;\n"
                "    private void TryRegisterPdaEvents()\n"
                "    {\n"
                "        PDAEvents.Register(this);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            with self.assertRaises(AuditFailure) as context:
                validate_pda_event_try_register_lifecycle(root)

            self.assertIn("PDA listener lifecycle", str(context.exception))

    def test_pda_event_try_register_lifecycle_accepts_visible_refusal_path(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            path = root / "Assets" / "_Project" / "Scripts" / "UI" / "FixedPdaListener.cs"
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(
                "public sealed class FixedPdaListener\n"
                "{\n"
                "    private bool _registeredPdaEvents;\n"
                "    private void TryRegisterPdaEvents()\n"
                "    {\n"
                "        _registeredPdaEvents = PDAEvents.TryRegister(this);\n"
                "    }\n"
                "    private void UnregisterPdaEvents()\n"
                "    {\n"
                "        if (!_registeredPdaEvents) return;\n"
                "        PDAEvents.Unregister(this);\n"
                "        _registeredPdaEvents = false;\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            validate_pda_event_try_register_lifecycle(root)

    def test_pda_event_try_register_lifecycle_rejects_missing_flag_clear(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            path = root / "Assets" / "_Project" / "Scripts" / "UI" / "BrokenPdaListener.cs"
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(
                "public sealed class BrokenPdaListener\n"
                "{\n"
                "    private bool _registeredPdaEvents;\n"
                "    private void TryRegisterPdaEvents()\n"
                "    {\n"
                "        _registeredPdaEvents = PDAEvents.TryRegister(this);\n"
                "    }\n"
                "    private void UnregisterPdaEvents()\n"
                "    {\n"
                "        PDAEvents.Unregister(this);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            with self.assertRaises(AuditFailure) as context:
                validate_pda_event_try_register_lifecycle(root)

            self.assertIn("clear the local PDAEvents registration flag", str(context.exception))

    def test_pda_event_try_register_lifecycle_rejects_fire_and_forget_try_register(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            path = root / "Assets" / "_Project" / "Scripts" / "UI" / "BrokenTryPdaListener.cs"
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(
                "public sealed class BrokenTryPdaListener\n"
                "{\n"
                "    private void TryRegisterPdaEvents()\n"
                "    {\n"
                "        PDAEvents.TryRegister(this);\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )

            with self.assertRaises(AuditFailure) as context:
                validate_pda_event_try_register_lifecycle(root)

            self.assertIn("local registration flag", str(context.exception))

    def test_pda_logbook_save_load_bridge_accepts_clear_and_replay_contract(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_logbook_bridge_sources(root)

            validate_pda_logbook_save_load_bridge(root)

    def test_pda_logbook_save_load_bridge_rejects_stale_ring_on_load(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_logbook_bridge_sources(root, include_clear_history=False)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_logbook_save_load_bridge(root)

            self.assertIn("ClearPDALogEventHistory", str(context.exception))

    def test_pda_logbook_save_load_bridge_rejects_data_log_without_ui_state_fallback(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_logbook_bridge_sources(root, include_data_log_fallback=False)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_logbook_save_load_bridge(root)

            self.assertIn("RefreshEventSourcedLogStateFromUIStore", str(context.exception))

    def test_pda_logbook_save_load_bridge_rejects_data_log_without_detail_refresh(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_logbook_bridge_sources(root, include_data_log_detail_refresh=False)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_logbook_save_load_bridge(root)

            self.assertIn("RefreshDetail", str(context.exception))

    def test_pda_logbook_save_load_bridge_rejects_silent_stale_replay_hash(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_logbook_bridge_sources(root, include_replay_missing_payload_fault=False)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_logbook_save_load_bridge(root)

            self.assertIn("persisted lore hash", str(context.exception))

    def test_pda_logbook_save_load_bridge_rejects_invisible_blackbox_write_failure(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_logbook_bridge_sources(root, include_blackbox_write_failure_visible=False)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_logbook_save_load_bridge(root)

            self.assertIn("blackbox write failure", str(context.exception))

    def test_pda_logbook_save_load_bridge_rejects_blackbox_retry_loop(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_logbook_bridge_sources(root, include_blackbox_write_failure_requeue=True)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_logbook_save_load_bridge(root)

            self.assertIn("IO retry loop", str(context.exception))

    def test_pda_logbook_save_load_bridge_rejects_rollback_without_ring_clear(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_pda_logbook_bridge_sources(root, include_rollback_ring_clear=False)

            with self.assertRaises(AuditFailure) as context:
                validate_pda_logbook_save_load_bridge(root)

            self.assertIn("PDA rollback", str(context.exception))

    def test_applied_lore_count_mismatch_names_stale_blob_bake_route(self):
        with self.assertRaises(AuditFailure) as context:
            validate_applied_records(
                [packet_row("P_EXPECTED")],
                b"",
                SectionEntry(section_id=1, record_size=1, count=1, offset=0),
                SectionEntry(section_id=2, record_size=128, count=0, offset=0),
            )

        message = str(context.exception)
        self.assertIn("AppliedLore count mismatch:", message)
        self.assertIn(DATA_MONOLITH_OUTPUT_RELATIVE_PATH, message)
        self.assertIn(DATA_MONOLITH_BAKE_METHOD, message)
        self.assertIn("AppliedLoreBlobDeltaAudit.py", message)
        self.assertIn("--json", message)

    def test_binding_map_reports_all_unknown_packets(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_binding_map(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "RS_TEST_runtime_binding_map.csv",
                [
                    binding_row("P_UNKNOWN_A"),
                    binding_row("P_UNKNOWN_B"),
                ],
            )

            with self.assertRaises(AuditFailure) as context:
                validate_binding_map(root, [packet_row("P_EXPECTED")])

            message = str(context.exception)
            self.assertIn("Binding map validation failed:", message)
            self.assertIn("P_UNKNOWN_A", message)
            self.assertIn("P_UNKNOWN_B", message)
            self.assertIn("missing packets: P_EXPECTED", message)

    def test_binding_map_skips_noncanonical_manifest_source_file(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS_DRAFT_manifest.json"
            )
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text('{"canonical_importer_ready": false}', encoding="utf-8")
            write_binding_map(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "RS_DRAFT_runtime_binding_map.csv",
                [binding_row("P_DRAFT_ONLY")],
            )
            write_binding_map(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "RS_TEST_runtime_binding_map.csv",
                [binding_row("P_EXPECTED")],
            )

            self.assertEqual(validate_binding_map(root, [packet_row("P_EXPECTED")]), 1)

    def test_binding_map_skips_noncanonical_manifest_packet_rows(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS_DRAFT_manifest.json"
            )
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text(
                '{"canonical_importer_ready": false, "packets": ["P_DRAFT_ONLY"]}',
                encoding="utf-8",
            )
            write_binding_map(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "061_runtime_binding_map.csv",
                [binding_row("P_DRAFT_ONLY")],
            )
            write_binding_map(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "RS_TEST_runtime_binding_map.csv",
                [binding_row("P_EXPECTED")],
            )

            self.assertEqual(validate_binding_map(root, [packet_row("P_EXPECTED")]), 1)

    def test_scene_binding_targets_skip_noncanonical_manifest_source_file(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS_DRAFT_manifest.json"
            )
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text('{"canonical_importer_ready": false}', encoding="utf-8")
            write_scene_binding_targets(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "RS_DRAFT_scene_binding_targets.csv",
                [scene_binding_row("P_DRAFT_ONLY")],
            )

            self.assertEqual(validate_scene_binding_targets(root, [packet_row("P_EXPECTED")]).rows, 0)

    def test_scene_binding_targets_skip_noncanonical_manifest_packet_rows(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS_DRAFT_manifest.json"
            )
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text(
                '{"canonical_importer_ready": false, "packets": ["P_DRAFT_ONLY"]}',
                encoding="utf-8",
            )
            write_scene_binding_targets(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "061_scene_binding_targets.csv",
                [scene_binding_row("P_DRAFT_ONLY")],
            )

            self.assertEqual(validate_scene_binding_targets(root, [packet_row("P_EXPECTED")]).rows, 0)

    def test_evidence_graph_skips_noncanonical_manifest_source_file(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS_DRAFT_manifest.json"
            )
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text('{"canonical_importer_ready": false}', encoding="utf-8")
            write_evidence_graph(
                root / "Docs" / "Lore" / "AppliedContent" / "graphs" / "RS_DRAFT_evidence_graph.csv",
                [evidence_graph_row("P_DRAFT_ONLY")],
            )
            write_evidence_graph(
                root / "Docs" / "Lore" / "AppliedContent" / "graphs" / "RS_TEST_evidence_graph.csv",
                [evidence_graph_row("P_EXPECTED")],
            )

            self.assertEqual(validate_evidence_graph(root, [packet_row("P_EXPECTED")]), 1)

    def test_evidence_graph_rejects_runtime_ref_to_noncanonical_packet(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS_DRAFT_manifest.json"
            )
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text(
                '{"canonical_importer_ready": false, "packets": ["P_DRAFT_ONLY"]}',
                encoding="utf-8",
            )
            write_evidence_graph(
                root / "Docs" / "Lore" / "AppliedContent" / "graphs" / "RS_TEST_evidence_graph.csv",
                [evidence_graph_row("P_EXPECTED", next_packet_ids="P_DRAFT_ONLY")],
            )

            with self.assertRaisesRegex(AuditFailure, "runtime-active next_packet_ids ref points at staged packet"):
                validate_evidence_graph(root, [packet_row("P_EXPECTED")])

    def test_evidence_graph_allows_ref_when_baked_packet_is_also_in_legacy_noncanonical_manifest(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS_LEGACY_manifest.json"
            )
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text(
                '{"canonical_importer_ready": false, "packets": ["P_EXPECTED_REF"]}',
                encoding="utf-8",
            )
            write_evidence_graph(
                root / "Docs" / "Lore" / "AppliedContent" / "graphs" / "RS_TEST_evidence_graph.csv",
                [
                    evidence_graph_row("P_EXPECTED", next_packet_ids="P_EXPECTED_REF"),
                    evidence_graph_row("P_EXPECTED_REF", prereq_packet_ids="P_EXPECTED"),
                ],
            )

            self.assertEqual(
                validate_evidence_graph(root, [packet_row("P_EXPECTED"), packet_row("P_EXPECTED_REF")]),
                2,
            )

    def test_navigation_cluster_graph_keeps_staged_authoring_rows_out_of_runtime(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS084_SITE_WIKI_NAVIGATION_CLUSTERS_manifest.json"
            )
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text(
                (
                    '{"canonical_importer_ready": false, "packets": ['
                    '"P_DRAFT_1", "P_DRAFT_2", "P_DRAFT_3", "P_DRAFT_4"]}'
                ),
                encoding="utf-8",
            )
            write_evidence_graph(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "graphs"
                / "RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv",
                [
                    navigation_cluster_graph_row("P_EXPECTED", route_moment="cluster_active"),
                    navigation_cluster_graph_row(
                        "P_DRAFT_1",
                        route_moment="cluster_draft_1",
                        prereq_packet_ids="P_EXPECTED",
                        next_packet_ids="P_DRAFT_2",
                    ),
                    navigation_cluster_graph_row("P_DRAFT_2", route_moment="cluster_draft_2"),
                    navigation_cluster_graph_row("P_DRAFT_3", route_moment="cluster_draft_3"),
                    navigation_cluster_graph_row("P_DRAFT_4", route_moment="cluster_draft_4"),
                ],
            )

            rows = load_navigation_cluster_graph(root, {"P_EXPECTED"})

            self.assertEqual([row["packet_id"] for row in rows], ["P_EXPECTED"])

    def test_navigation_cluster_graph_rejects_runtime_active_ref_to_staged_packet(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS084_SITE_WIKI_NAVIGATION_CLUSTERS_manifest.json"
            )
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text(
                '{"canonical_importer_ready": false, "packets": ["P_DRAFT_1", "P_DRAFT_2", "P_DRAFT_3", "P_DRAFT_4"]}',
                encoding="utf-8",
            )
            write_evidence_graph(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "graphs"
                / "RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv",
                [
                    navigation_cluster_graph_row(
                        "P_EXPECTED",
                        route_moment="cluster_active",
                        next_packet_ids="P_DRAFT_1",
                    ),
                    navigation_cluster_graph_row("P_DRAFT_1", route_moment="cluster_draft_1"),
                    navigation_cluster_graph_row("P_DRAFT_2", route_moment="cluster_draft_2"),
                    navigation_cluster_graph_row("P_DRAFT_3", route_moment="cluster_draft_3"),
                    navigation_cluster_graph_row("P_DRAFT_4", route_moment="cluster_draft_4"),
                ],
            )

            with self.assertRaisesRegex(AuditFailure, "runtime-active next_packet_ids ref points at staged packet"):
                load_navigation_cluster_graph(root, {"P_EXPECTED"})

    def test_navigation_cluster_graph_allows_ref_when_baked_packet_is_also_in_legacy_noncanonical_manifest(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS084_SITE_WIKI_NAVIGATION_CLUSTERS_manifest.json"
            )
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text(
                (
                    '{"canonical_importer_ready": false, "packets": ['
                    '"P_EXPECTED_REF", "P_DRAFT_1", "P_DRAFT_2", "P_DRAFT_3"]}'
                ),
                encoding="utf-8",
            )
            write_evidence_graph(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "graphs"
                / "RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv",
                [
                    navigation_cluster_graph_row(
                        "P_EXPECTED",
                        route_moment="cluster_active",
                        next_packet_ids="P_EXPECTED_REF",
                    ),
                    navigation_cluster_graph_row("P_EXPECTED_REF", route_moment="cluster_active_ref"),
                    navigation_cluster_graph_row("P_DRAFT_1", route_moment="cluster_draft_1"),
                    navigation_cluster_graph_row("P_DRAFT_2", route_moment="cluster_draft_2"),
                    navigation_cluster_graph_row("P_DRAFT_3", route_moment="cluster_draft_3"),
                ],
            )

            rows = load_navigation_cluster_graph(root, {"P_EXPECTED", "P_EXPECTED_REF"})

            self.assertEqual([row["packet_id"] for row in rows], ["P_EXPECTED", "P_EXPECTED_REF"])

    def test_route_cards_skip_rows_without_baked_owner_packet(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_route_cards(
                root / "Docs" / "Lore" / "AppliedContent" / "route_cards" / "RS_DRAFT_route_cards.csv",
                [route_card_row("P_DRAFT_ONLY", "RC_DRAFT")],
            )
            write_route_cards(
                root / "Docs" / "Lore" / "AppliedContent" / "route_cards" / "RS_TEST_route_cards.csv",
                [route_card_row("P_EXPECTED", "RC_EXPECTED")],
            )

            self.assertEqual(validate_route_cards(root, [packet_row("P_EXPECTED")]), 1)

    def test_manual_binding_policy_skips_nonbaked_backlog_rows(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_scene_binding_targets(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "RS_TEST_scene_binding_targets.csv",
                [manual_scene_binding_row("P_EXPECTED")],
            )
            write_manual_binding_policy(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "RS001_RS010_manual_binding_policy.csv",
                [manual_policy_row("P_DRAFT_ONLY"), manual_policy_row("P_EXPECTED")],
            )

            stats = validate_manual_binding_policy(root, [packet_row("P_EXPECTED")])

            self.assertEqual(stats.rows, 1)
            self.assertEqual(stats.discovery_rows, 1)

    def test_manual_binding_policy_rejects_duplicate_discovery_id(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_scene_binding_targets(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "RS_TEST_scene_binding_targets.csv",
                [manual_scene_binding_row("P_EXPECTED"), manual_scene_binding_row("P_SECOND")],
            )
            first_policy = manual_policy_row("P_EXPECTED")
            second_policy = manual_policy_row("P_SECOND")
            second_policy["discovery_id"] = first_policy["discovery_id"]
            write_manual_binding_policy(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "RS001_RS010_manual_binding_policy.csv",
                [first_policy, second_policy],
            )

            with self.assertRaises(AuditFailure) as context:
                validate_manual_binding_policy(root, [packet_row("P_EXPECTED"), packet_row("P_SECOND")])

            self.assertIn("duplicate discovery_id", str(context.exception))

    def test_scene_placement_plan_skips_nonbaked_backlog_rows(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_manual_binding_policy(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "RS001_RS010_manual_binding_policy.csv",
                [manual_policy_row("P_DRAFT_ONLY")],
            )
            write_scene_placement_plan(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "RS001_RS010_scene_placement_plan.csv",
                [scene_placement_row("P_DRAFT_ONLY")],
            )

            self.assertEqual(validate_scene_placement_plan(root, [packet_row("P_EXPECTED")]).rows, 0)

    def test_scene_placement_plan_rejects_duplicate_scene_owner(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_scene_placement_dependencies(root)
            write_manual_binding_policy(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "RS001_RS010_manual_binding_policy.csv",
                [manual_policy_row("P_EXPECTED"), manual_policy_row("P_SECOND")],
            )
            first_placement = scene_placement_row("P_EXPECTED")
            second_placement = scene_placement_row("P_SECOND")
            second_placement["object_name"] = first_placement["object_name"]
            write_scene_placement_plan(
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "binding_maps"
                / "RS001_RS010_scene_placement_plan.csv",
                [first_placement, second_placement],
            )

            with self.assertRaises(AuditFailure) as context:
                validate_scene_placement_plan(root, [packet_row("P_EXPECTED"), packet_row("P_SECOND")])

            self.assertIn("duplicate scene owner", str(context.exception))


if __name__ == "__main__":
    unittest.main()
