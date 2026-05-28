#!/usr/bin/env python3
import json
import pathlib
import sys
import xml.etree.ElementTree as ET


ROOT = pathlib.Path(__file__).resolve().parents[1]
VENDOR_TOKENS = ("Candice", "Amplify", "Technie", "MasterAudio")
VENDOR_ASMDEFS = {
    "Candice": [
        "Assets/Candice AI for Games/Scripts/CandiceAIforGames.Runtime.asmdef",
        "Assets/Candice AI for Games/Scripts/Editor/CandiceAIforGames.Editor.asmdef",
    ],
    "Amplify": [
        "Assets/AmplifyImpostors/Plugins/Scripts/AmplifyImpostors.Runtime.asmdef",
        "Assets/AmplifyImpostors/Plugins/Editor/AmplifyImpostors.Editor.asmdef",
    ],
    "Technie": [
        "Assets/Technie/PhysicsCreator/Scripts/TechniePhysicsCreator.asmdef",
        "Assets/Technie/PhysicsCreator/Updater/Technie.PhysicsCreator.Updater.asmdef",
    ],
    "MasterAudio": [
        "Assets/Plugins/DarkTonic/MasterAudio/Scripts/DarkTonic.MasterAudio.Runtime.asmdef",
        "Assets/Plugins/DarkTonic/MasterAudio/ExampleScenes/Scripts/DarkTonic.MasterAudio.Examples.asmdef",
        "Assets/Plugins/Editor/DarkTonic/MasterAudio/DarkTonic.MasterAudio.Editor.asmdef",
        "Assets/Plugins/Editor/RelationsInspector/RelationsInspector.Editor.asmdef",
    ],
}


def read_text(path):
    return path.read_text(encoding="utf-8", errors="replace")


def project_references(path):
    if not path.exists():
        return []
    root = ET.fromstring(read_text(path))
    refs = []
    for elem in root.iter():
        if elem.tag.endswith("ProjectReference"):
            refs.append(elem.attrib.get("Include", ""))
    return refs


def contains_text(path, text):
    if not path.exists():
        return False
    return text in read_text(path)


def asmdef_references(path):
    if not path.exists():
        return []
    data = json.loads(read_text(path))
    refs = data.get("references", [])
    if not isinstance(refs, list):
        return []
    return [str(item) for item in refs]


def asmdef_quarantine_status(path):
    if not path.exists():
        return {
            "path": str(path.relative_to(ROOT)),
            "exists": False,
            "autoReferenced": None,
            "references": [],
        }

    data = json.loads(read_text(path))
    return {
        "path": str(path.relative_to(ROOT)),
        "exists": True,
        "autoReferenced": bool(data.get("autoReferenced", True)),
        "references": [str(item) for item in data.get("references", [])],
    }


def main():
    assembly_cs = ROOT / "Assembly-CSharp.csproj"
    assembly_firstpass = ROOT / "Assembly-CSharp-firstpass.csproj"
    assembly_editor_firstpass = ROOT / "Assembly-CSharp-Editor-firstpass.csproj"
    core_asmdef = ROOT / "Assets" / "_Project" / "Scripts" / "Hecton8.Core.asmdef"
    output_path = ROOT / "Docs" / "AgentLogs" / "AsmdefLeakage_1401.json"

    assembly_refs = project_references(assembly_cs)
    core_refs = asmdef_references(core_asmdef)
    source_asmdefs = {}
    findings = []

    for ref in assembly_refs:
        for token in VENDOR_TOKENS:
            if token in ref:
                findings.append({
                    "source": str(assembly_cs.relative_to(ROOT)),
                    "token": token,
                    "reference": ref,
                    "scope": "ProjectReference"
                })

    for ref in core_refs:
        for token in VENDOR_TOKENS:
            if token in ref:
                findings.append({
                    "source": str(core_asmdef.relative_to(ROOT)),
                    "token": token,
                    "reference": ref,
                    "scope": "asmdef.references"
                })

    for token, paths in VENDOR_ASMDEFS.items():
        token_status = []
        for item in paths:
            status = asmdef_quarantine_status(ROOT / item)
            token_status.append(status)
            if not status["exists"]:
                findings.append({
                    "source": item,
                    "token": token,
                    "reference": "",
                    "scope": "missingVendorAsmdef"
                })
            elif status["autoReferenced"]:
                findings.append({
                    "source": item,
                    "token": token,
                    "reference": "autoReferenced=true",
                    "scope": "vendorAsmdefQuarantine"
                })
        source_asmdefs[token] = token_status

    generated_project_state = {
        "checked": [
            str(assembly_firstpass.relative_to(ROOT)),
            str(assembly_editor_firstpass.relative_to(ROOT)),
        ],
        "assemblyCSharpFirstpassPluginWildcard": contains_text(
            assembly_firstpass,
            r'<Compile Include="Assets\Plugins\**\*.cs" />',
        ),
        "assemblyCSharpEditorFirstpassPluginEditorWildcard": contains_text(
            assembly_editor_firstpass,
            r'<Compile Include="Assets\Plugins\**\Editor\**\*.cs" />',
        ),
        "dedicatedMasterAudioRuntimeProjectExists": (
            ROOT / "DarkTonic.MasterAudio.Runtime.csproj"
        ).exists(),
        "dedicatedMasterAudioExamplesProjectExists": (
            ROOT / "DarkTonic.MasterAudio.Examples.csproj"
        ).exists(),
        "dedicatedMasterAudioEditorProjectExists": (
            ROOT / "DarkTonic.MasterAudio.Editor.csproj"
        ).exists(),
        "dedicatedRelationsInspectorEditorProjectExists": (
            ROOT / "RelationsInspector.Editor.csproj"
        ).exists(),
        "unityRegenerationRequired": True,
        "note": (
            "Generated csproj files are Unity-owned and can remain stale until "
            "the Editor imports newly added asmdefs."
        ),
    }

    report = {
        "agent": "1401",
        "status": "PASS" if not findings else "FAIL",
        "checked": [
            str(assembly_cs.relative_to(ROOT)),
            str(core_asmdef.relative_to(ROOT))
        ],
        "vendorTokens": list(VENDOR_TOKENS),
        "assemblyCSharpProjectReferenceCount": len(assembly_refs),
        "coreAsmdefReferenceCount": len(core_refs),
        "sourceAsmdefQuarantine": source_asmdefs,
        "generatedProjectState": generated_project_state,
        "findings": findings
    }

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(output_path)
    return 0 if not findings else 2


if __name__ == "__main__":
    sys.exit(main())
