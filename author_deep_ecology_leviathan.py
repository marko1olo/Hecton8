import os
import json

nodes_dir = r"C:\hades\Hecton8\Docs\Lore\AppliedContent\EvidenceNodes"

ecology_leviathan_nodes = [
    {
        "node_id": "ECO_SPINE_01",
        "author": "Atlas-6 Ecology Subroutine",
        "subject_tags": ["Ecology", "Sector 01-10", "Algae Bloom"],
        "title": "Ecological Collapse: Spine 0-100m",
        "surface": "Telemetry",
        "text": "Photic zone baseline variance detected. Surface-fed algae blooms are clinging to module vents. Oxygen levels read stable at hatch height, but Marauder route marks indicate visual toxicity. Predators hold at the edge of bright work lamps and avoid silent corridors. The biological die-off has initiated, masked as seasonal color by corporate reporting algorithms."
    },
    {
        "node_id": "ECO_DROWNED_11",
        "author": "Atlas-6 Ecology Subroutine",
        "subject_tags": ["Ecology", "Sector 11-30", "Oxygen"],
        "title": "Ecological Collapse: Drowned Factories 100-1500m",
        "surface": "Telemetry",
        "text": "Algae bloom thickens around coolant leaks. Oxygen recovers for minutes after pump cycles, then drops harder. Predators stop patrolling and wait beside forced-flow vents. Biomass accounting marks the sector productive because corpse mass increased. The vents exhale green-brown snow that ruins visibility before it kills. The bloom has crossed the food chain."
    },
    {
        "node_id": "ECO_DROP_31",
        "author": "Atlas-6 Ecology Subroutine",
        "subject_tags": ["Ecology", "Sector 31-50", "Leviathan"],
        "title": "Ecological Collapse: Drop 1000-2500m",
        "surface": "Telemetry",
        "text": "Light drops faster than the depth palette changes. Prey movement becomes vertical, avoiding toxic layers like invisible floors. Biomass noise fools sonar into reporting crowds inside dead chambers. Predators abandon territory and follow the last oxygen seams. Atlas-6 labels the crash a 'population correction'. Hatch windows show drifting bones before they show the room."
    },
    {
        "node_id": "ECO_ABYSS_51",
        "author": "Atlas-6 Ecology Subroutine",
        "subject_tags": ["Ecology", "Sector 51-70", "Deep Abyss"],
        "title": "Ecological Collapse: Deep Abyss 2500-4000m",
        "surface": "Telemetry",
        "text": "Prey are rare, large, and scarred by bad oxygen history. Predators patrol by vibration, not sight, because bloom stole the midwater. Old eggs hatch wrong and die before they learn current. Oxygen pocket warnings are carved into walls, not terminals. The sector is not empty; it is holding its breath after the kill."
    },
    {
        "node_id": "ECO_THERMAL_71",
        "author": "Atlas-6 Ecology Subroutine",
        "subject_tags": ["Ecology", "Sector 71-80", "Xenon-Omega"],
        "title": "Ecological Collapse: Thermal Fields 4000-5500m",
        "surface": "Telemetry",
        "text": "Oxygen spikes near vents, then becomes chemically hostile. Heat-fed bloom glows in cracks and dies in open water. Predators hunt across heat boundaries with patient confidence. Biomass is mineralized into pipes, vents, and teeth. The collapse ends where the Corporation started counting the planet as inventory."
    },
    {
        "node_id": "LEV_ALPHA_01",
        "author": "[REDACTED] Aegir Fauna Assessment",
        "subject_tags": ["Leviathan", "Alpha", "Redacted"],
        "title": "Alpha Leviathan Dossier",
        "surface": "Terminal Document",
        "text": "ALPHA LEVIATHAN designation applied. Reliable facts are indirect: sub-40 Hz acoustic carrier, prey blackout, flow disturbance, and hull stress alarms without contact. Survivor descriptions are contaminated by hypoxia and nitrogen narcosis. The entity edits routes. It arrives before it is seen. Do not attempt acoustic interception. Do not tag. Evacuate the pressure sphere immediately upon hearing the carrier tone."
    },
    {
        "node_id": "LEV_ALPHA_02",
        "author": "Iliya Varnek, Aegir Operations Risk",
        "subject_tags": ["Leviathan", "Liability"],
        "title": "Asset Containment: Alpha",
        "surface": "Corporate Memo",
        "text": "The existence of a macro-predator in the lower transit routes is an unacceptable variable for the Xenon-Omega extraction schedule. We cannot kill it without drawing orbital audit attention to the weapons discharge. Instead, we will route automated carrier drones through Sector 38 to act as acoustic decoys. The cost of five drones per week is lower than the insurance premium on a devoured drilling crew."
    }
]

for log in ecology_leviathan_nodes:
    filepath = os.path.join(nodes_dir, f"{log['node_id'].lower()}.json")
    with open(filepath, "w", encoding="utf-8") as f:
        json.dump(log, f, indent=4)

print(f"Authored 7 Deep Lore Ecology/Leviathan Nodes to {nodes_dir}")
