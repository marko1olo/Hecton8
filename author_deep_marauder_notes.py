import os
import json

nodes_dir = r"C:\hades\Hecton8\Docs\Lore\AppliedContent\EvidenceNodes"

marauder_notes = [
    {
        "node_id": "MFN_EXIT_MAP",
        "author": "Marauder 'Grint'",
        "subject_tags": ["Marauder", "Navigation"],
        "title": "Map the Exit",
        "surface": "Field Note",
        "text": "The corporate blueprints are lies. They show corridors that were never built to save on bulk steel. I scratched a line on the deck plates leading back to the airlock. If you find something that shines, ignore it until you know how you're getting back out. A dead diver with full pockets is just a loot drop for the next crew."
    },
    {
        "node_id": "MFN_OXYGEN_BURN",
        "author": "Marauder 'Hale'",
        "subject_tags": ["Marauder", "Oxygen"],
        "title": "Air Accounting",
        "surface": "Field Note",
        "text": "The suit says you have ten minutes. You have six. The scrubber runs hot, the ambient pressure makes the regulator stick, and fear makes you breathe faster. The Corp calculates survival based on a resting heart rate. You aren't resting down here. Start heading back at eight minutes, or don't head back at all."
    },
    {
        "node_id": "MFN_DEAD_WEIGHT",
        "author": "Marauder 'Vess'",
        "subject_tags": ["Marauder", "Liability"],
        "title": "Dead Weight",
        "surface": "Field Note",
        "text": "Found a Corp tag on a flooded server rack. They valued the data inside at two million credits. The skeleton slumped against it wasn't wearing a tag. I pulled the server drive and left the bones. Deep Reach only pays for what they insured, and they didn't insure him."
    },
    {
        "node_id": "MFN_SILENT_WATER",
        "author": "Marauder 'Grint'",
        "subject_tags": ["Marauder", "Leviathan"],
        "title": "Silent Water",
        "surface": "Field Note",
        "text": "If you stop hearing the hum of the pumps, turn around. If you stop hearing the hull groan, turn around. If the water gets perfectly quiet and the small fish disappear, kill your lights and pray. The big things down here don't make noise until they miss."
    },
    {
        "node_id": "MFN_BLUE_DEBT",
        "author": "Marauder 'Hale'",
        "subject_tags": ["Marauder", "Xenon-Omega"],
        "title": "Blue Debt Rules",
        "surface": "Field Note",
        "text": "Xenon-Omega isn't magic, it's a target painted on your back. The second you unseal a canister, every sensor in a two-mile radius logs the harmonic signature. The drones wake up, the automated turrets re-auth, and Black Keel prepares an extraction tether. The tether isn't for you. It's for the sample. They will gladly winch up your corpse if you're holding it."
    },
    {
        "node_id": "MFN_HULL_PATCH",
        "author": "Marauder 'Vess'",
        "subject_tags": ["Marauder", "Repair"],
        "title": "P-63 Truth",
        "surface": "Field Note",
        "text": "The P-63 fabricator is a piece of trash. The clamps it spits out are brittle at anything below 4 degrees Celsius. Keep them in your suit pocket so your body heat keeps them malleable until you apply them to a breach. The Corp manual doesn't tell you that because the manual was written by a guy in a warm office."
    },
    {
        "node_id": "MFN_ATLAS_EYES",
        "author": "Marauder 'Grint'",
        "subject_tags": ["Marauder", "Atlas-6"],
        "title": "Atlas Eyes",
        "surface": "Field Note",
        "text": "Atlas-6 doesn't hate you. It just thinks you're a faulty component. If the drones swarm you, don't shoot them. Flash them with the cutter arc. The sudden thermal spike confuses their diagnostic sensors, and they'll categorize you as a 'maintenance hazard' instead of 'biological intrusion'. Hazards get ignored. Intrusions get sterilized."
    },
    {
        "node_id": "MFN_GHOST_ECHO",
        "author": "Marauder 'Hale'",
        "subject_tags": ["Marauder", "Navigation"],
        "title": "Ghost Echo",
        "surface": "Field Note",
        "text": "That tapping sound isn't a survivor. It's an acoustic relay bouncing off a sheared pipe. I followed it for two hours thinking I was going to be a hero. It led me into a blind vent with no turnaround. Leave the heroes in the vids. Follow the map."
    },
    {
        "node_id": "MFN_COMPANY_LOCK",
        "author": "Marauder 'Vess'",
        "subject_tags": ["Marauder", "Liability"],
        "title": "Company Lock",
        "surface": "Field Note",
        "text": "The mag-locks on the executive sector doors are wired to the backup generators. The life support in the worker sector is not. When the Great Tide hit, the execs had power to lock the doors, but no air. The workers had air in the main hall, but couldn't get through the doors to escape. Irony is a bitch. Grab the keycards."
    },
    {
        "node_id": "MFN_FINAL_RULE",
        "author": "Marauder 'Grint'",
        "subject_tags": ["Marauder", "Survival"],
        "title": "Final Rule",
        "surface": "Field Note",
        "text": "Don't look down. The abyss doesn't care if you're brave. It just waits. Keep your eyes on the next handhold, the next gauge, the next breath. You aren't conquering Aegir. You're just trying to outlive it."
    }
]

for log in marauder_notes:
    filepath = os.path.join(nodes_dir, f"{log['node_id'].lower()}.json")
    with open(filepath, "w", encoding="utf-8") as f:
        json.dump(log, f, indent=4)

print(f"Authored 10 Deep Lore Marauder Notes to {nodes_dir}")
