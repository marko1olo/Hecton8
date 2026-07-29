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

# Root GameObject name inside each HUD prefab, used for the name probe below.
# Verified 2026-07-29: `m_Name: HUD_Internal` is the sole name in HUD_Internal.prefab,
# and `m_Name: Suit_HUD_Canvas` is at Suit_HUD_Canvas.prefab:2350.
HUD_INTERNAL_ROOT_NAME = "HUD_Internal"
SUIT_HUD_CANVAS_ROOT_NAME = "Suit_HUD_Canvas"

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


# ---------------------------------------------------------------------------
# The prefab-instance inversion.
#
# A component carried by a prefab INSTANCE emits no scene entry unless the value
# is overridden. So for anything that lives on Player.prefab, absence from
# 02_HECTON_WORLD.unity is the EXPECTED SIGNATURE OF A CORRECT PREFAB INSTANCE,
# not evidence against one. Asking the scene is asking the wrong artifact, and
# four checks in this file used to treat the right answer as a defect.
#
# Measured 2026-07-29 in this repo, and this is the control that proves the
# inversion rather than asserting it: `HUD_Render_Camera` is a GameObject inside
# Player.prefab (Player.prefab:3549) and the binary world scene contains that
# name ZERO times, while it does contain `Main Camera` and `Directional Light`
# once each. The scene emits nothing for a prefab instance's interior. The same
# scene DOES carry Player.prefab's own asset GUID. Both facts together are the
# whole point: the scene names the prefab, and says nothing about its contents.
#
# Therefore:
#   * a script GUID that resolves only to Player.prefab -> ask Player.prefab
#     whether it carries the component (`prefab_carries_script`);
#   * a prefab asset GUID -> ask BOTH whether the scene instantiates it AND
#     whether Player.prefab nests it, because a nested prefab serialises its
#     source GUID into the parent prefab and never into the scene.
#
# DO NOT "fix" these back into scene-only GUID searches. That inversion is one
# of the five wrong claims retracted from
# Docs/Orchestration/PLAYER_HUD_MOVEMENT_P0_SYNTHESIS_20260605.md (blocker 4),
# which stood for seven weeks and was used to declare visor polish and cinematic
# camera work invalid.
# ---------------------------------------------------------------------------


def prefab_carries_script(prefab_text: str, script_guid: str) -> bool:
    """Is this MonoBehaviour an actual component of the prefab?

    Requires the full `m_Script` binding, not a bare GUID substring: a prefab can
    mention a GUID in a serialized field without carrying the component, and
    "the component is on the prefab" is a stronger claim than "the GUID occurs".
    """
    pattern = rf"m_Script:\s*\{{fileID:\s*11500000,\s*guid:\s*{re.escape(script_guid)},\s*type:\s*3\}}"
    return bool(prefab_text) and re.search(pattern, prefab_text) is not None


def text_references_guid(text: str, guid: str) -> bool:
    """Does this text artifact reference the GUID at all, in either hex case?

    Used against Player.prefab, which is a TEXT prefab - a nested prefab stores
    its source GUID here as hex, which is why the nesting question is answerable
    without Unity while the same question against a binary scene is not.
    """
    return bool(text) and (guid in text or guid.upper() in text)


def object_name_present(text: str, name: str) -> bool:
    """Does a GameObject with exactly this name exist in a TEXT scene or prefab?"""
    if not text:
        return False
    return re.search(rf"^\s*m_Name:\s*{re.escape(name)}\s*$", text, re.MULTILINE) is not None


def scene_object_name_present(name: str, scene_text: str, scene_bytes: bytes) -> bool:
    """Same question against whichever encoding the scene uses.

    Binary Unity scenes store GameObject names as plain length-prefixed UTF-8, so
    unlike GUIDs the name needs no nibble swap and IS greppable in the bytes.
    Verified against this scene: `Main Camera` and `Directional Light` both read
    1, so a name miss here is a real miss at scene level - subject to the
    inversion above, which means it still says nothing about prefab interiors.

    The binary branch is a raw substring test, so `Suit_HUD_Canvas_Legacy` would
    also satisfy `Suit_HUD_Canvas`. That looseness is deliberate and one-directional:
    every caller uses a name HIT only to WITHHOLD a blocker, never to raise one, so
    the failure mode is an over-cautious UNDECIDABLE rather than a false accusation.
    Do not tighten this into something that can manufacture a negative.
    """
    if is_binary_scene(scene_bytes):
        return name.encode("utf-8") in scene_bytes
    return object_name_present(scene_text, name)


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
    source_tokens = [
        "HectonPlayerMovement" in player_movement_text
        and "IBootstrapProductionPlayerMovementAuthority" in player_movement_text,
        "PlayerInteraction" in player_interaction_text
        and "IBootstrapProductionPlayerInteractionAuthority" in player_interaction_text,
    ]

    # The legacy-world-shell token is required ONLY while a legacy world shell exists, and one no
    # longer does. HectonWorldShellController1428.cs was deleted on 2026-06-15 by 621403ad5
    # ("1428 file cleanup"), and IBootstrapLegacyWorldShellOwner now appears solely in its own
    # declaration in BootstrapState.cs with ZERO implementers anywhere under Assets - so the object
    # this guard exists to reject cannot be constructed at all.
    #
    # Demanding evidence of a deleted class made the whole predicate permanently FALSE, which raised
    # `bootstrap-publish-player-without-production-validation` unconditionally: an accusation that
    # BootstrapState can publish an unvalidated playerObject, when the movement and interaction
    # authority tokens are both satisfied and the only missing witness is a file whose absence is
    # correct.
    #
    # Nobody saw it for six weeks because the two things that would have complained were disabled by
    # the same deletion: this validator short-circuited on the missing file before it could classify,
    # and Tools/test_validate_player_route_static_evidence.py skipped itself on `shell.exists()`.
    # Making the file optional unmasked it; un-skipping the test is what caught it. A requirement that
    # names a deleted file is not a strict check, it is a stuck one.
    if world_shell_text:
        source_tokens.append(
            "HectonWorldShellController1428" in world_shell_text
            and "IBootstrapLegacyWorldShellOwner" in world_shell_text
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

    # Ask Player.prefab the questions only Player.prefab can answer. See the
    # prefab-instance inversion block above for why the scene cannot.
    prefab_has_movement = prefab_carries_script(player_prefab_text, HECTON_PLAYER_MOVEMENT_GUID)
    prefab_has_interaction = prefab_carries_script(player_prefab_text, PLAYER_INTERACTION_GUID)
    prefab_nests_hud_internal = text_references_guid(player_prefab_text, HUD_INTERNAL_PREFAB_GUID)
    prefab_nests_suit_hud = text_references_guid(player_prefab_text, SUIT_HUD_CANVAS_PREFAB_GUID)
    prefab_names_hud_internal = object_name_present(player_prefab_text, HUD_INTERNAL_ROOT_NAME)
    prefab_names_suit_hud = object_name_present(player_prefab_text, SUIT_HUD_CANVAS_ROOT_NAME)
    scene_names_hud_internal = scene_object_name_present(HUD_INTERNAL_ROOT_NAME, scene_text, scene_bytes)
    scene_names_suit_hud = scene_object_name_present(SUIT_HUD_CANVAS_ROOT_NAME, scene_text, scene_bytes)

    # THE POSITIVE CONTROL. A search that has not been shown able to find a
    # positive in this exact file has not established that anything is missing -
    # that is the rule Tools/SceneGuidReachability.py enforces with --control, and
    # this tool violated it for seven weeks. Player.prefab's own GUID being found
    # in these very scene bytes is the control: it proves the byte search reaches
    # this scene. If even that GUID is missing, the search is unproven and every
    # scene negative below is withheld as UNDECIDABLE instead of blamed on the
    # asset.
    scene_guid_search_validated = scene_has_guid

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

    # ---- HUD prefab ASSET GUIDs -------------------------------------------
    # These two are prefab asset GUIDs, not script GUIDs (verified against
    # HUD_Internal.prefab.meta and Suit_HUD_Canvas.prefab.meta), so the question
    # is "does the player route instantiate this prefab", and there are TWO
    # places that can answer: the scene, as a prefab instance, or Player.prefab,
    # as a NESTED prefab. Asking only the scene was wrong because a nested prefab
    # never appears in the scene. Both are asked now, plus the root object name
    # in both artifacts, which catches an unpacked/plain-GameObject authoring
    # that carries no prefab asset link at all.
    for label, guid, scene_guid_hit, nested_hit, named_in_prefab, named_in_scene, retired_key in (
        (
            "hud-internal-prefab",
            HUD_INTERNAL_PREFAB_GUID,
            scene_has_hud_internal_guid,
            prefab_nests_hud_internal,
            prefab_names_hud_internal,
            scene_names_hud_internal,
            "scene-missing-hud-internal-prefab-guid",
        ),
        (
            "suit-hud-canvas-prefab",
            SUIT_HUD_CANVAS_PREFAB_GUID,
            scene_has_suit_hud_guid,
            prefab_nests_suit_hud,
            prefab_names_suit_hud,
            scene_names_suit_hud,
            "scene-missing-suit-hud-prefab-guid",
        ),
    ):
        if scene_guid_hit:
            notes.append(f"{label}-reference: present - scene instantiates {guid}")
        elif nested_hit:
            notes.append(f"{label}-reference: present - nested inside Player.prefab, which is why the scene has no entry")
        elif named_in_prefab:
            notes.append(
                f"{label}-reference: UNDECIDABLE - Player.prefab has no {guid} link but does carry a GameObject "
                f"named for it, so the prefab asset GUID is not the right question here. Unity readback required."
            )
        elif named_in_scene:
            notes.append(
                f"{label}-reference: UNDECIDABLE - no {guid} link anywhere on the player route, but the scene "
                f"carries a GameObject with that root name (unpacked prefab authoring). Whether it is the "
                f"authored HUD needs Unity readback."
            )
        elif not scene_guid_search_validated:
            notes.append(
                f"{label}-reference: UNDECIDABLE - {guid} not found, but the scene GUID search is unvalidated "
                f"(Player.prefab's own GUID is missing too), so this negative is withheld rather than reported."
            )
        else:
            blockers.append(
                f"{label}-unreferenced-by-player-route: {guid} is instantiated by neither the scene nor "
                f"Player.prefab, and neither artifact carries a GameObject with its root name "
                f"(retires the inverted check {retired_key}, which asked only the scene)"
            )

    # ---- Player component SCRIPT GUIDs ------------------------------------
    # HectonPlayerMovement and PlayerInteraction resolve ONLY to
    # Assets/_Project/Prefabs/Player.prefab (Tools/SceneGuidReachability.py,
    # 2026-07-29). Their absence from the scene is therefore the expected
    # signature of a correct prefab instance, and the old
    # `scene-missing-player-*-guid` blockers were reporting the right answer as a
    # defect. Player.prefab is the artifact that can answer, so it is the one
    # asked; a scene hit still counts, because that is the legitimate
    # scene-override / scene-authored case.
    for label, guid, prefab_hit, scene_hit, retired_key in (
        (
            "player-movement-component",
            HECTON_PLAYER_MOVEMENT_GUID,
            prefab_has_movement,
            scene_has_movement_guid,
            "scene-missing-player-movement-guid",
        ),
        (
            "player-interaction-component",
            PLAYER_INTERACTION_GUID,
            prefab_has_interaction,
            scene_has_interaction_guid,
            "scene-missing-player-interaction-guid",
        ),
    ):
        if prefab_hit:
            notes.append(
                f"{label}: present - Player.prefab carries the m_Script binding for {guid}. Scene absence is "
                f"the expected signature of a prefab instance, not a defect (retires {retired_key})."
            )
        elif scene_hit:
            notes.append(f"{label}: present - scene-level entry for {guid} (scene-authored or overridden)")
        elif not player_prefab_text:
            notes.append(
                f"{label}: UNDECIDABLE - {guid} absent from the scene and Player.prefab text was not supplied, "
                f"so the artifact that can answer was never read."
            )
        else:
            blockers.append(
                f"{label}-missing: {guid} has no m_Script binding in Player.prefab and no scene-level entry"
            )

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
