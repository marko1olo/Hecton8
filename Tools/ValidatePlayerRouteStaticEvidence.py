#!/usr/bin/env python3
"""Validate static blockers for the HECTON-8 production player route.

This is a static blocker guard, not runtime proof. It rejects evidence when the
world scene still exposes a scene-local shell Player and no production
Player.prefab route is statically visible.
"""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
REPO_ROOT = TOOLS_ROOT.parent

PLAYER_PREFAB_GUID = "1c4db7a430141e5408e01b6ce4ed19d7"
HUD_INTERNAL_PREFAB_GUID = "949b94e6d99fdd44ea13e320d0784005"
SUIT_HUD_CANVAS_PREFAB_GUID = "e286dd44e529d8b4498750dd0abbbfd8"
HECTON_PLAYER_MOVEMENT_GUID = "6d195933dec89b14ebbfa47a621ac549"
PLAYER_INTERACTION_GUID = "215f6ea2a912636499ffc2dda9bdfb9d"

DEFAULT_SCENE = REPO_ROOT / "Assets" / "_Project" / "Scenes" / "02_HECTON_WORLD.unity"
DEFAULT_BOOTSTRAP = REPO_ROOT / "Assets" / "_Project" / "Scripts" / "Bootstrap" / "GameBootstrapper.cs"
DEFAULT_SCENE_GATE = REPO_ROOT / "Assets" / "_Project" / "Scripts" / "Bootstrap" / "SceneInstantiationGate.cs"
DEFAULT_BOOTSTRAP_STATE = REPO_ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "BootstrapContracts" / "BootstrapState.cs"
DEFAULT_SPAWNER = REPO_ROOT / "Assets" / "_Project" / "Scripts" / "HectonPlayerSpawner.cs"
DEFAULT_PLAYER_MOVEMENT = REPO_ROOT / "Assets" / "_Project" / "Scripts" / "HectonPlayerMovement.cs"
DEFAULT_PLAYER_INTERACTION = REPO_ROOT / "Assets" / "_Project" / "Scripts" / "Interaction" / "PlayerInteraction.cs"
DEFAULT_WORLD_SHELL = REPO_ROOT / "Assets" / "_Project" / "Scripts" / "World" / "HectonWorldShellController1428.cs"
DEFAULT_PLAYER_PREFAB = REPO_ROOT / "Assets" / "_Project" / "Prefabs" / "Player.prefab"
DEFAULT_HUD_INTERNAL_PREFAB = REPO_ROOT / "Assets" / "_Project" / "Prefabs" / "HUD_Internal.prefab"
DEFAULT_SUIT_HUD_CANVAS_PREFAB = REPO_ROOT / "Assets" / "_Project" / "Prefabs" / "Suit_HUD_Canvas.prefab"


@dataclass(frozen=True)
class PlayerRouteEvidence:
    status: str
    blockers: tuple[str, ...]
    notes: tuple[str, ...]

    @property
    def is_static_route_visible(self) -> bool:
        return not self.blockers


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace")


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(REPO_ROOT.resolve()).as_posix()
    except ValueError:
        return str(path)


def has_scene_local_shell_player(scene_text: str) -> bool:
    player_name = re.search(r"m_Name:\s*Player\b", scene_text) is not None
    player_tag = re.search(r"m_TagString:\s*Player\b", scene_text) is not None
    shell_component = "HectonWorldShellController1428" in scene_text
    no_prefab_asset = re.search(r"m_PrefabAsset:\s*\{fileID:\s*0\}", scene_text) is not None
    return player_name and player_tag and shell_component and no_prefab_asset


def has_zero_ref(text: str, field_name: str) -> bool:
    pattern = rf"\b{re.escape(field_name)}:\s*\{{fileID:\s*0\}}"
    return re.search(pattern, text) is not None


def has_scene_prefab_instance(scene_text: str, prefab_guid: str) -> bool:
    pattern = rf"m_SourcePrefab:\s*\{{fileID:\s*100100000,\s*guid:\s*{re.escape(prefab_guid)},\s*type:\s*3\}}"
    return re.search(pattern, scene_text) is not None


# ---------------------------------------------------------------------------
# Binary-scene awareness.
#
# 02_HECTON_WORLD.unity is a BINARY scene: no `%YAML` header, and the string
# `m_Script` occurs in it zero times. Every check below used to run against
# `read_text(..., errors="replace")` of that file, which turns the bytes into
# U+FFFD replacement characters - so a hex-GUID substring test could not match
# whatever the scene actually contained. All five scene GUID tests were
# structurally incapable of returning true, and this tool therefore emitted
# `scene-missing-production-prefab-guid` unconditionally.
#
# That is not hypothetical. It manufactured the claim at
# Docs/Orchestration/PLAYER_HUD_MOVEMENT_P0_SYNTHESIS_20260605.md:238 that
# Player.prefab is absent from the world scene. The claim stood for seven weeks
# and was used to declare visor polish, cinematic camera work and surface
# screenshots invalid. Player.prefab is in fact PRESENT - one FileIdentifier
# external-reference entry at offset 566551, type 3, nibble-swapped.
#
# A binary scene stores a GUID as raw bytes in NIBBLE-SWAPPED order, which is
# why a plain byte search for the hex digits fails too. The byte-order helper is
# imported from Tools/SceneGuidReachability.py rather than re-derived here: that
# tool self-tests the swap against a control GUID, and sharing one definition
# stops the two from drifting into disagreeing about what "present" means.
# ---------------------------------------------------------------------------

try:  # pragma: no cover - taken whenever the sibling tool is on disk
    if str(TOOLS_ROOT) not in sys.path:
        sys.path.insert(0, str(TOOLS_ROOT))
    from SceneGuidReachability import nibble_swap as _nibble_swap
except ImportError:  # sibling tool is the source of truth; this is only a last resort
    def _nibble_swap(raw: bytes) -> bytes:
        return bytes(((b & 0x0F) << 4) | (b >> 4) for b in raw)


def is_binary_scene(scene_bytes: bytes) -> bool:
    """The `%YAML` header test - the only reliable discriminator.

    Not the extension and not the size: `.unity` covers both encodings, and four
    scenes in this project are binary while 995 other scene/prefab files are not.
    """
    return bool(scene_bytes) and scene_bytes.lstrip()[:5] != b"%YAML"


def scene_guid_present(guid: str, scene_text: str, scene_bytes: bytes) -> bool:
    """Is this GUID referenced by the scene, in whichever encoding the scene uses?

    Text scenes carry the GUID as hex. Binary scenes carry it as raw bytes,
    nibble-swapped; both byte orders are tested, because the swap is a property
    of the serialiser rather than of the GUID - so a future Unity version
    changing it should surface as a hit, not as a silent false negative.
    """
    if is_binary_scene(scene_bytes):
        raw = bytes.fromhex(guid)
        return _nibble_swap(raw) in scene_bytes or raw in scene_bytes
    return guid in scene_text or guid.upper() in scene_text


def has_production_player_prefab_source_route(text: str) -> bool:
    prefab_field = re.search(r"\b(?:GameObject|AssetReferenceGameObject)\s+productionPlayerPrefab\b", text) is not None
    prefab_instantiate = re.search(r"\bInstantiate\s*\(\s*productionPlayerPrefab\b", text) is not None
    authority_acceptance = "TryAcceptProductionPlayerTransform" in text or "TryAcceptProductionPlayerAuthority" in text
    return PLAYER_PREFAB_GUID in text and prefab_field and prefab_instantiate and authority_acceptance


def has_null_tab_entries(text: str) -> bool:
    match = re.search(r"\btabs:\s*(?:\r?\n\s*-\s*\{fileID:\s*0\}){2,}", text)
    return match is not None


def component_blocks_with_zero_ref(text: str, field_name: str) -> tuple[str, ...]:
    """Return Unity MonoBehaviour-ish blocks that contain a null serialized field."""
    if not text or not has_zero_ref(text, field_name):
        return ()

    blocks: list[str] = []
    separator = "\n--- !u!"
    pattern = rf"\b{re.escape(field_name)}:\s*\{{fileID:\s*0\}}"
    for match in re.finditer(pattern, text):
        block_start = text.rfind(separator, 0, match.start())
        if block_start < 0:
            block_start = 0
        else:
            block_start += 1

        block_end = text.find(separator, match.end())
        if block_end < 0:
            block_end = len(text)
        blocks.append(text[block_start:block_end])

    return tuple(blocks)


def block_after(text: str, marker: str, max_chars: int) -> str:
    index = text.find(marker)
    if index < 0:
        return ""
    return text[index : index + max_chars]


def has_production_player_authority_guard(
    bootstrap_state_text: str,
    player_movement_text: str = "",
    player_interaction_text: str = "",
    world_shell_text: str = "",
) -> bool:
    bootstrap_tokens = (
        "IsProductionPlayerAuthorityObject",
        "IsLegacyWorldShellOwned",
        "IBootstrapProductionPlayerMovementAuthority",
        "IBootstrapProductionPlayerInteractionAuthority",
        "IBootstrapLegacyWorldShellOwner",
        "Rigidbody",
    )
    source_tokens = (
        "HectonPlayerMovement" in player_movement_text
        and "IBootstrapProductionPlayerMovementAuthority" in player_movement_text,
        "PlayerInteraction" in player_interaction_text
        and "IBootstrapProductionPlayerInteractionAuthority" in player_interaction_text,
        "HectonWorldShellController1428" in world_shell_text
        and "IBootstrapLegacyWorldShellOwner" in world_shell_text,
    )
    return all(token in bootstrap_state_text for token in bootstrap_tokens) and all(source_tokens)


def classify(
    scene_text: str,
    bootstrap_text: str,
    spawner_text: str,
    scene_gate_text: str = "",
    bootstrap_state_text: str = "",
    player_movement_text: str = "",
    player_interaction_text: str = "",
    world_shell_text: str = "",
    player_prefab_text: str = "",
    hud_internal_prefab_text: str = "",
    suit_hud_canvas_prefab_text: str = "",
    scene_bytes: bytes = b"",
) -> PlayerRouteEvidence:
    blockers: list[str] = []
    notes: list[str] = []

    # `scene_bytes` is keyword-with-default so existing positional callers
    # (Tools/RunAssetStaticValidators.py, Tools/ValidateAssetFrontFileMap.py)
    # keep working. When it is empty the scene is treated as text, which is the
    # old behaviour - correct for the 995 text scene/prefab files, and the reason
    # main() now always passes the bytes for the four binary ones.
    scene_is_binary = is_binary_scene(scene_bytes)

    scene_has_guid = scene_guid_present(PLAYER_PREFAB_GUID, scene_text, scene_bytes)
    scene_has_hud_internal_guid = scene_guid_present(HUD_INTERNAL_PREFAB_GUID, scene_text, scene_bytes)
    scene_has_suit_hud_guid = scene_guid_present(SUIT_HUD_CANVAS_PREFAB_GUID, scene_text, scene_bytes)
    scene_has_movement_guid = scene_guid_present(HECTON_PLAYER_MOVEMENT_GUID, scene_text, scene_bytes)
    scene_has_interaction_guid = scene_guid_present(PLAYER_INTERACTION_GUID, scene_text, scene_bytes)

    # Two questions below are answered by YAML text markers and are genuinely
    # UNDECIDABLE against a binary scene. `None` means "not answered", which is
    # deliberately distinct from `False` - reporting an unearned negative is the
    # exact failure this tool committed for seven weeks, and a validator that
    # cannot answer must say so rather than emit a blocker or a clean bill.
    scene_has_player_prefab_instance = (
        None if scene_is_binary else has_scene_prefab_instance(scene_text, PLAYER_PREFAB_GUID)
    )
    bootstrap_has_prefab_route = has_production_player_prefab_source_route(bootstrap_text)
    spawner_has_prefab_route = has_production_player_prefab_source_route(spawner_text)
    runtime_has_guid = bootstrap_has_prefab_route or spawner_has_prefab_route
    state_has_production_guard = has_production_player_authority_guard(
        bootstrap_state_text,
        player_movement_text=player_movement_text,
        player_interaction_text=player_interaction_text,
        world_shell_text=world_shell_text,
    )
    bootstrap_uses_production_guard = "TryAcceptProductionPlayerAuthority" in bootstrap_text and "IsProductionPlayerAuthorityObject" in bootstrap_text
    gate_uses_production_guard = "MarkPlayerInstantiated" in scene_gate_text and "IsProductionPlayerAuthorityObject" in scene_gate_text
    spawner_uses_production_guard = "TryAcceptProductionPlayerRigidbody" in spawner_text and "IsProductionPlayerAuthorityObject" in spawner_text
    shell_player = None if scene_is_binary else has_scene_local_shell_player(scene_text)

    if scene_is_binary:
        notes.append(
            "scene-encoding: BINARY - GUID presence answered by byte search in both orders; "
            "YAML-marker questions below are reported UNDECIDABLE rather than negative"
        )

    if shell_player is None:
        notes.append(
            "scene-shell-player: UNDECIDABLE - needs m_Name/m_TagString/m_PrefabAsset YAML markers "
            "that a binary scene does not carry as text. Not a clean bill."
        )
    elif shell_player:
        blockers.append("scene-shell-player: 02_HECTON_WORLD contains scene-local Player shell markers")
    else:
        notes.append("scene-shell-player: not detected by static marker scan")

    if not scene_has_guid:
        blockers.append(f"scene-missing-production-prefab-guid: {PLAYER_PREFAB_GUID} not found in scene")
    else:
        notes.append("scene-production-prefab-guid: present")

    if scene_has_player_prefab_instance is None:
        notes.append(
            "scene-production-prefab-instance-exact: UNDECIDABLE - a binary scene stores no "
            "m_SourcePrefab text. The GUID hit above proves the scene REFERENCES Player.prefab; it "
            "does not distinguish a PrefabInstance binding from a serialized field pointing at the "
            "asset. Unity readback is the only way to settle it."
        )
    elif not scene_has_player_prefab_instance:
        blockers.append("scene-production-prefab-instance-exact: production Player.prefab m_SourcePrefab binding not found")
    else:
        notes.append("scene-production-prefab-instance-exact: present")

    if not scene_has_hud_internal_guid:
        blockers.append(f"scene-missing-hud-internal-prefab-guid: {HUD_INTERNAL_PREFAB_GUID} not found in scene")
    else:
        notes.append("scene-hud-internal-prefab-guid: present")

    if not scene_has_suit_hud_guid:
        blockers.append(f"scene-missing-suit-hud-prefab-guid: {SUIT_HUD_CANVAS_PREFAB_GUID} not found in scene")
    else:
        notes.append("scene-suit-hud-prefab-guid: present")

    if not scene_has_movement_guid:
        blockers.append(f"scene-missing-player-movement-guid: {HECTON_PLAYER_MOVEMENT_GUID} not found in scene")
    else:
        notes.append("scene-player-movement-guid: present")

    if not scene_has_interaction_guid:
        blockers.append(f"scene-missing-player-interaction-guid: {PLAYER_INTERACTION_GUID} not found in scene")
    else:
        notes.append("scene-player-interaction-guid: present")

    if not runtime_has_guid:
        blockers.append(f"runtime-missing-production-prefab-guid: {PLAYER_PREFAB_GUID} production prefab source route not found in bootstrap/spawner")
    else:
        notes.append("runtime-production-prefab-guid: production prefab source route present in bootstrap/spawner")
    if state_has_production_guard:
        notes.append("runtime-production-player-authority-guard: movement/interaction/physics/shell-rejection guard present")

    if "DefaultLookTargetPrompt" in player_interaction_text and '"OPEN HATCH"' in player_interaction_text:
        blockers.append("player-interaction-hatch-fallback-prompt: default prompt leaks OPEN HATCH to non-hatch interactables")

    if "SpawnPlayerAsync" in bootstrap_text and "SpawnPlayerAsync" in spawner_text:
        notes.append("spawn-method-text-present: SpawnPlayerAsync exists, static integration still requires Unity proof")
    else:
        blockers.append("spawn-method-text-missing: SpawnPlayerAsync text missing in bootstrap or spawner")

    if (
        "Rigidbody playerRigidbody" in spawner_text
        and PLAYER_PREFAB_GUID not in spawner_text
        and not spawner_uses_production_guard
    ):
        blockers.append("spawner-existing-rigidbody-route: spawner exposes existing Rigidbody route without prefab guid")

    if (
        'TryResolveSceneTaggedObject(scene, "Player"' in bootstrap_text
        and "!IsTemporaryRuntimeShellObject(taggedPlayer)" in bootstrap_text
        and not bootstrap_uses_production_guard
    ):
        blockers.append("bootstrap-tagged-shell-acceptance-route: tagged Player can be accepted without production component validation")

    if (
        "MarkPlayerInstantiated(playerObject)" in bootstrap_text
        and PLAYER_PREFAB_GUID not in bootstrap_text
        and not gate_uses_production_guard
    ):
        blockers.append("bootstrap-mark-player-instantiated-without-production-validation: SceneInstantiationGate accepts playerObject without prefab guid guard")

    if (
        "BootstrapState.PublishCurrentPlayerObject(playerObject)" in bootstrap_text
        and PLAYER_PREFAB_GUID not in bootstrap_text
        and not state_has_production_guard
    ):
        blockers.append("bootstrap-publish-player-without-production-validation: BootstrapState can publish playerObject without prefab guid guard")

    if (
        "GameBootstrapper.TryGetCurrentPlayerTransform" in spawner_text
        and "TryGetComponent(out playerRigidbody)" in spawner_text
        and not spawner_uses_production_guard
    ):
        blockers.append("spawner-bootstrap-transform-rigidbody-fallback-route: spawner can resolve Rigidbody from bootstrap transform fallback")

    if player_prefab_text:
        if "Hecton8.UI.PlayerPDA" in player_prefab_text:
            if has_zero_ref(player_prefab_text, "pdaPanel") or has_zero_ref(player_prefab_text, "pdaCanvasGroup"):
                blockers.append("player-prefab-pda-null-panel-route: PlayerPDA has null panel/canvas group refs")
            if has_null_tab_entries(player_prefab_text) or has_zero_ref(player_prefab_text, "controlsRebindUI"):
                blockers.append("player-prefab-pda-null-tab-route: PlayerPDA tabs or controls rebind UI contain null refs")
        else:
            blockers.append("player-prefab-missing-pda-component: Player.prefab text has no PlayerPDA component marker")

        pause_menu_null_blocks = component_blocks_with_zero_ref(player_prefab_text, "pauseMenu")
        pause_menu_dev_null = any("Hecton8.Dev.UIRuntimeSmokeTester" in block for block in pause_menu_null_blocks)
        pause_menu_production_null = any("Hecton8.Dev.UIRuntimeSmokeTester" not in block for block in pause_menu_null_blocks)
        if pause_menu_production_null:
            blockers.append("player-prefab-pause-menu-null-route: Player.prefab has production pause route null refs")
        if pause_menu_dev_null:
            notes.append("player-prefab-dev-pause-smoke-null-note: UIRuntimeSmokeTester pauseMenu is null; not a production pause blocker")

        swim_contract_null_blocks = component_blocks_with_zero_ref(player_prefab_text, "_swimContract")
        builder_swim_null = any("Hecton8.Building.PlayerBuilder" in block for block in swim_contract_null_blocks)
        non_builder_swim_null = any("Hecton8.Building.PlayerBuilder" not in block for block in swim_contract_null_blocks)
        if non_builder_swim_null:
            blockers.append("player-prefab-swim-contract-null-route: Player production tool route has null swim contract")
        if builder_swim_null:
            notes.append("player-prefab-builder-swim-contract-null-readback-note: root PlayerBuilder _swimContract is null; held-tool/runtime resolution requires Unity readback")

        suit_advisory_null_blocks = component_blocks_with_zero_ref(player_prefab_text, "survival") + component_blocks_with_zero_ref(player_prefab_text, "hudNotification")
        suit_advisory_null = any("Hecton8.UI.SuitAdvisoryController" in block for block in suit_advisory_null_blocks)
        if suit_advisory_null:
            notes.append("player-prefab-suit-advisory-runtime-ref-null-note: SuitAdvisoryController serialized survival/HUD refs are null; source has cold resolve but Unity readback is still required")

        hud_null_fields = (
            "overlayModernHud",
            "projectedModernHud",
            "canvasOverlay",
            "projectionSourceOverlay",
            "screenCompositor",
        )
        if "SuitHUDPresentationController" in player_prefab_text and any(
            has_zero_ref(player_prefab_text, field_name) for field_name in hud_null_fields
        ):
            blockers.append("player-prefab-hud-presentation-null-route: SuitHUDPresentationController has null HUD projection/compositor refs")

        hud_camera_block = block_after(player_prefab_text, "m_Name: HUD_Render_Camera", 900)
        if hud_camera_block and re.search(r"\bCamera:\s*(?:\r?\n|.){0,220}\bm_Enabled:\s*0\b", hud_camera_block):
            blockers.append("player-prefab-hud-render-camera-disabled-or-unbound: HUD_Render_Camera camera is disabled")
        if hud_camera_block and re.search(r"\bm_TargetTexture:\s*\{fileID:\s*0\}", hud_camera_block):
            blockers.append("player-prefab-hud-render-camera-disabled-or-unbound: HUD_Render_Camera has null target texture")

        hud_extension_block = block_after(player_prefab_text, "m_EditorClassIdentifier: Assembly-CSharp::HectonSuitHUDExtensions", 260)
        if hud_extension_block and (
            has_zero_ref(hud_extension_block, "primaryHud")
            or has_zero_ref(hud_extension_block, "canvasOverlay")
            or has_zero_ref(hud_extension_block, "flashlight")
        ):
            blockers.append("player-prefab-hud-extension-null-route: HectonSuitHUDExtensions has null HUD/flashlight refs")

    if hud_internal_prefab_text:
        if "SuitHUDScreenCompositor" not in hud_internal_prefab_text:
            blockers.append("hud-internal-missing-compositor: HUD_Internal has no SuitHUDScreenCompositor marker")
        if re.search(r"m_EditorClassIdentifier:\s*Assembly-CSharp::NASAPunk\.Visor\.SuitHUDScreenCompositor[\s\S]{0,220}m_Enabled:\s*0", hud_internal_prefab_text):
            blockers.append("hud-internal-compositor-disabled: HUD_Internal compositor is disabled")
        elif "SuitHUDScreenCompositor" in hud_internal_prefab_text and re.search(
            r"m_Enabled:\s*0[\s\S]{0,220}SuitHUDScreenCompositor", hud_internal_prefab_text
        ):
            blockers.append("hud-internal-compositor-disabled: HUD_Internal compositor is disabled")
        if has_zero_ref(hud_internal_prefab_text, "targetCanvas") or has_zero_ref(hud_internal_prefab_text, "visorController"):
            blockers.append("hud-internal-compositor-null-route: HUD_Internal compositor has null target/visor refs")
        if re.search(r"\bforceScreenSpaceOverlay:\s*1\b", hud_internal_prefab_text):
            blockers.append("hud-internal-force-overlay-route: HUD_Internal forces screen-space overlay")

    if suit_hud_canvas_prefab_text:
        if re.search(r"\bm_RenderMode:\s*0\b", suit_hud_canvas_prefab_text):
            blockers.append("suit-hud-canvas-overlay-render-mode: Suit_HUD_Canvas is ScreenSpaceOverlay")
        suit_null_fields = ("projectionCamera", "survival", "playerMovement", "underwaterVisuals")
        if any(has_zero_ref(suit_hud_canvas_prefab_text, field_name) for field_name in suit_null_fields):
            blockers.append("suit-hud-canvas-null-runtime-route: Suit_HUD_Canvas has null projection/player/survival refs")
        if "DEPRECATED_HUD_Master_V2" in suit_hud_canvas_prefab_text and "Hecton8.Interaction.InteractionUI" in suit_hud_canvas_prefab_text:
            notes.append("suit-hud-deprecated-interaction-ui-present: requires Unity readback before prompt-route claims")

    status = "PLAYER_ROUTE_STATIC_EVIDENCE_PASS" if not blockers else "PLAYER_ROUTE_STATIC_EVIDENCE_REJECTED"
    return PlayerRouteEvidence(status=status, blockers=tuple(blockers), notes=tuple(notes))


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scene", type=Path, default=DEFAULT_SCENE)
    parser.add_argument("--bootstrap", type=Path, default=DEFAULT_BOOTSTRAP)
    parser.add_argument("--scene-gate", type=Path, default=DEFAULT_SCENE_GATE)
    parser.add_argument("--bootstrap-state", type=Path, default=DEFAULT_BOOTSTRAP_STATE)
    parser.add_argument("--spawner", type=Path, default=DEFAULT_SPAWNER)
    parser.add_argument("--player-movement", type=Path, default=DEFAULT_PLAYER_MOVEMENT)
    parser.add_argument("--player-interaction", type=Path, default=DEFAULT_PLAYER_INTERACTION)
    parser.add_argument("--world-shell", type=Path, default=DEFAULT_WORLD_SHELL)
    parser.add_argument("--player-prefab", type=Path, default=DEFAULT_PLAYER_PREFAB)
    parser.add_argument("--hud-internal-prefab", type=Path, default=DEFAULT_HUD_INTERNAL_PREFAB)
    parser.add_argument("--suit-hud-canvas-prefab", type=Path, default=DEFAULT_SUIT_HUD_CANVAS_PREFAB)
    parser.add_argument("--require-production-static", action="store_true")
    parser.add_argument("--no-fail", action="store_true", help="Print rejection status but return success.")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    required_paths = (
        args.scene,
        args.bootstrap,
        args.scene_gate,
        args.bootstrap_state,
        args.spawner,
        args.player_movement,
        args.player_interaction,
        args.player_prefab,
        args.hud_internal_prefab,
        args.suit_hud_canvas_prefab,
    )
    missing = [path for path in required_paths if not path.exists()]
    if missing:
        print(f"PLAYER_ROUTE_STATIC_EVIDENCE_REJECTED blockers={len(missing)}")
        for path in missing:
            print(f"- missing-file: {rel(path)}")
        return 0 if args.no_fail else (2 if args.require_production_static else 0)

    # HectonWorldShellController1428.cs is deliberately OPTIONAL, and this is the
    # second independent reason this tool had stopped working. It was deleted on
    # 2026-06-15 by 621403ad5 ("1428 file cleanup"), and because it sat in
    # required_paths every invocation since then printed
    # `PLAYER_ROUTE_STATIC_EVIDENCE_REJECTED blockers=1 / missing-file` and
    # returned before classify() ever ran. Six weeks of a validator that could
    # not reach its own logic, exiting 0 so nothing noticed.
    #
    # Its absence is the CORRECT post-cleanup state rather than a defect: the
    # legacy world shell it implemented is gone. Verified at the same time -
    # `IBootstrapLegacyWorldShellOwner` now appears only in its own declaration
    # in BootstrapState.cs and has ZERO implementers anywhere in Assets, and
    # every reader of `IsLegacyWorldShellOwned` is inside that same file. So the
    # scene-local shell Player this tool exists to reject is not merely absent,
    # it is unconstructable. That is worth stating in the output rather than
    # inferring from a silent pass.
    world_shell_text = read_text(args.world_shell) if args.world_shell.exists() else ""

    evidence = classify(
        read_text(args.scene),
        read_text(args.bootstrap),
        read_text(args.spawner),
        read_text(args.scene_gate),
        read_text(args.bootstrap_state),
        read_text(args.player_movement),
        read_text(args.player_interaction),
        world_shell_text,
        read_text(args.player_prefab),
        read_text(args.hud_internal_prefab),
        read_text(args.suit_hud_canvas_prefab),
        # The raw bytes, because read_text() above destroys them: it decodes with
        # errors="replace", so every non-UTF8 byte of a binary scene becomes
        # U+FFFD and no GUID can ever be found in the result.
        scene_bytes=args.scene.read_bytes(),
    )
    print(f"{evidence.status} blockers={len(evidence.blockers)} notes={len(evidence.notes)}")
    for blocker in evidence.blockers:
        print(f"- {blocker}")
    for note in evidence.notes:
        print(f"+ {note}")

    if args.require_production_static and not evidence.is_static_route_visible and not args.no_fail:
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
