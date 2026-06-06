import os
import json

nodes_dir = r"C:\hades\Hecton8\Docs\Lore\AppliedContent\EvidenceNodes"

system_ui_nodes = [
    {
        "node_id": "SYS_UI_THERMAL_LIE",
        "author": "HectonSubmarineOS",
        "subject_tags": ["UI", "Thermal", "Liability"],
        "title": "Thermal Sheer Warning",
        "surface": "System HUD",
        "text": "STATUS: MONITORED BACKGROUND ACTIVITY. Ambient thermal variance within engineered margins. Hull tension: Nominal. [WARNING CANCELED BY VARNEK OVERRIDE]"
    },
    {
        "node_id": "SYS_UI_ACTUARIAL_THREAT",
        "author": "Atlas-6 Drone Routing",
        "subject_tags": ["UI", "Actuarial", "Liability"],
        "title": "Drone Target Reclassification",
        "surface": "System HUD",
        "text": "SCAN DETECTED: UNAUTHORIZED RECOVERY OF UNRESOLVED SYSTEM LOAD. You are handling unregistered biological material. Drone repair priority revoked. Actuarial threat level elevated. Please discard bodies to restore maintenance privileges."
    },
    {
        "node_id": "SYS_UI_AIRLOCK_DENIED",
        "author": "BaseAirlock",
        "subject_tags": ["UI", "Quarantine"],
        "title": "Airlock Cycle Denied",
        "surface": "System HUD",
        "text": "CYCLE FAILED. Haldane-8 Quarantine active. Xenon-Omega biomatter detected on suit exterior. You are a contamination risk to corporate infrastructure. Return to the basin or locate an authorized chemical shower. This airlock will not open."
    },
    {
        "node_id": "SYS_UI_PDA_SILENCE",
        "author": "PDAExchangeSystem",
        "subject_tags": ["UI", "Liability", "Silence"],
        "title": "Upload Severed",
        "surface": "System HUD",
        "text": "UPLOAD FAILED. Sato-Ren Filter triggered. The data packet contains restricted payroll information and casualty counts. Acoustic link severed to protect carrier integrity. Scrub drive before re-attempting extraction."
    },
    {
        "node_id": "SYS_UI_ARENDT_OVERRIDE",
        "author": "LifePodDamageSystem",
        "subject_tags": ["UI", "Atlas-6"],
        "title": "Life Support Diverted",
        "surface": "System HUD",
        "text": "POWER REROUTED. Atlas-6 Directive Weighting Override active. Life support and pressure shields in Sector 44 deactivated to stabilize adjacent Xenon-Omega substrate vaults. Your survival parameter is currently weighted at: -0.15."
    }
]

for log in system_ui_nodes:
    filepath = os.path.join(nodes_dir, f"{log['node_id'].lower()}.json")
    with open(filepath, "w", encoding="utf-8") as f:
        json.dump(log, f, indent=4)

print(f"Authored 5 Deep System UI Nodes to {nodes_dir}")
