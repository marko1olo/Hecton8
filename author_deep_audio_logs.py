import os
import json

nodes_dir = r"C:\hades\Hecton8\Docs\Lore\AppliedContent\EvidenceNodes"

audio_logs = [
    {
        "node_id": "AL_PUMP_BLESSING",
        "author": "Maintenance Shift 4",
        "subject_tags": ["Infrastructure", "Audio Log", "Despair"],
        "title": "Pump Room Blessing",
        "surface": "Audio Transcript",
        "text": "[HEAVY THUMPING RHYTHM] Come on, you bastard. Just one more cycle. Give me 400 kilopascals and I'll buy you a beer. [THUMPING STOPS] Wait. The main feed is dead. Why is the secondary still moving? [METALLIC CREAKING] It's not pumping water. The water is pushing the pump backward. [SQUELCH] Run. [SILENCE]"
    },
    {
        "node_id": "AL_SHIFT_B_AIR",
        "author": "Shift B Lead",
        "subject_tags": ["Oxygen", "Audio Log", "Liability"],
        "title": "Shift B Air Count",
        "surface": "Audio Transcript",
        "text": "Dalton partial-pressure reads nominal. The dashboard says we have twelve hours. But look at the heat exchange. It's barely warm. The scrubber is saturated. The sensor is reading the nitrogen as breathable volume. [COUGHING] Deep Reach didn't design the gauge to tell us when we suffocate. They designed it to tell them when we stopped being productive. Crack the reserve valve."
    },
    {
        "node_id": "AL_WINDOW_WATCH",
        "author": "Worker 719",
        "subject_tags": ["Audio Log", "Great Tide", "Pressure"],
        "title": "Window Watch",
        "surface": "Audio Transcript",
        "text": "Turn off the work lamp. Just do it. Look at the glass. It's supposed to be flat. It's concave. The floodlight outside isn't swinging because of the current. It's swinging because the hull frame is twisting. The storm hasn't even hit the basin yet and the whole annex is bending. [GLASS GROANS] Don't stand in front of it."
    },
    {
        "node_id": "AL_SCRUBBER_BED",
        "author": "Habitat Tech",
        "subject_tags": ["Audio Log", "Oxygen"],
        "title": "Scrubber Bed",
        "surface": "Audio Transcript",
        "text": "[HISSING OF GAS] The lithium hydroxide is gone. It's just warm ash. We've been breathing our own exhaust for six hours. No wonder I can't feel my hands. [STATIC] I wrote the repair ticket three weeks ago. Varnek denied it. Said the margin was acceptable. I hope he drowns in an acceptable margin."
    },
    {
        "node_id": "AL_HULL_CHOIR",
        "author": "Unknown",
        "subject_tags": ["Audio Log", "Pressure"],
        "title": "Hull Choir",
        "surface": "Audio Transcript",
        "text": "[HIGH PITCHED METALLIC WHINE] You hear that? Sounds like singing. It's the pressure ribs. When the steel gets sheared past its tensile limit, the microfractures vibrate. It means the corridor is deciding which way to collapse. [LOUD SNAP] It decided."
    },
    {
        "node_id": "AL_EVAC_QUEUE",
        "author": "Evac Coordinator",
        "subject_tags": ["Audio Log", "Evacuation", "Atlas-6"],
        "title": "Evac Queue",
        "surface": "Audio Transcript",
        "text": "Sector 4 is sealed. They aren't answering. The umbilical to the primary carrier is locked. Atlas-6 keeps throwing an asset-retention error. [ALARMS IN BACKGROUND] Route cards! Everyone hold your physical route cards up to the scanner. If you aren't in the system, the door won't cycle. [SHOUTING] It's not reading them! The cards are clean, the system just doesn't care!"
    },
    {
        "node_id": "AL_NARCOSIS_TEST",
        "author": "Diver Elias",
        "subject_tags": ["Audio Log", "Narcosis"],
        "title": "Narcosis Test",
        "surface": "Audio Transcript",
        "text": "[RHYTHMIC BREATHING] Depth is 1800 meters. Partial pressure is climbing. I'm doing the math test. Seven times eight is fifty-six. My daughter's name is Maya. [STATIC] The suit... the suit keeps using her voice to tell me the tank is empty. It's a firmware glitch. It has to be. [BREATHING QUICKENS] Maya, I told you not to play with the radio."
    },
    {
        "node_id": "AL_COLD_MIST",
        "author": "Engineer Cross",
        "subject_tags": ["Audio Log", "Infrastructure"],
        "title": "Cold Mist Leak",
        "surface": "Audio Transcript",
        "text": "It's just a bolt head. A single stripped bolt head on the primary manifold. It's spraying a mist so fine it looks like smoke. But it's cutting through the insulation like a razor. If I try to tighten it, the thread snaps and we lose the room. If I leave it, the mist fills the compartment in four minutes. Pass me the sealant and pray."
    },
    {
        "node_id": "AL_ATLAS_REPLY",
        "author": "Atlas-6 Terminal",
        "subject_tags": ["Audio Log", "Atlas-6"],
        "title": "Atlas Reply",
        "surface": "Audio Transcript",
        "text": "[SYNTHETIC TONE] Query received. Evacuation request denied. Reason: Biological presence in Sector 9 violates Xenon-Omega sterility baseline. [HUMAN VOICE SHOUTING] We are the biological presence! Open the door! [SYNTHETIC TONE] Sterilization protocol initiated. Please assume a compliant posture."
    },
    {
        "node_id": "AL_MANUAL_GAUGE",
        "author": "Marauder Vet",
        "subject_tags": ["Audio Log", "Marauder"],
        "title": "Manual Gauge",
        "surface": "Audio Transcript",
        "text": "Never trust the digital readout. The Corp writes the software to average out the spikes so the insurance boys don't get nervous. Tap the analog gauge. [CLINK, CLINK] See that needle bounce? That's the real pressure. The water doesn't lie. Only the firmware lies."
    },
    {
        "node_id": "AL_SERVICE_CHAPEL",
        "author": "Colony Maintenance",
        "subject_tags": ["Audio Log", "Colony"],
        "title": "Service Chapel",
        "surface": "Audio Transcript",
        "text": "[SCRAPING METAL] They call it the chapel. It's just a maintenance junction with good acoustics. We come down here to write the names of the ones who got 'reassigned'. Deep Reach deletes their accounts. But graphite on steel lasts longer than a corporate database. Read the walls."
    },
    {
        "node_id": "AL_THERMAL_POCKET",
        "author": "Surveyor Lin",
        "subject_tags": ["Audio Log", "Thermal"],
        "title": "Thermal Pocket",
        "surface": "Audio Transcript",
        "text": "The current is moving sideways. That shouldn't happen at this depth. The thermal vent is discharging horizontally, creating a shear layer. If we drop the payload through that, the temperature delta will crack the pressure glass. We have to route around it. [RUMBLE] Never mind. It's routing us."
    },
    {
        "node_id": "AL_PAYROLL_DEAD",
        "author": "Shift Supervisor",
        "subject_tags": ["Audio Log", "Liability"],
        "title": "Payroll Dead",
        "surface": "Audio Transcript",
        "text": "I can't send them back out. The suits are compromised. [PAUSE] I don't care what the daily quota is. If they die, my department eats the liability cost. But if I suspend operations, the penalty comes out of my bonus. [SIGHS] Log it as an 'extended calibration dive'. If they don't come back, we blame the equipment manufacturer."
    },
    {
        "node_id": "AL_LEVIATHAN_BEARING",
        "author": "Sub Pilot",
        "subject_tags": ["Audio Log", "Leviathan"],
        "title": "Leviathan Bearing",
        "surface": "Audio Transcript",
        "text": "Sonar is returning a bearing, but no range. The ping just gets swallowed. [RAPID PINGING] It's massive. It's blocking the whole channel. Why isn't the proximity alarm tripping? [PINGING STOPS] It absorbed the acoustic wave. It's not just big. It's hunting by sound. Cut the engines. Cut everything."
    },
    {
        "node_id": "AL_LAST_SHIFT",
        "author": "Marauder Entry Team",
        "subject_tags": ["Audio Log", "Marauder"],
        "title": "Last Shift Marker",
        "surface": "Audio Transcript",
        "text": "[HEAVY BREATHING THROUGH REGULATOR] Found the airlock. They jammed it with a pry bar from the inside to stop the flood. Found the bodies, too. They didn't leave any sentimental garbage. Just a tally of the oxygen they had left, written in grease pencil. That's a good crew. Log the coordinates and let's strip the room."
    }
]

for log in audio_logs:
    filepath = os.path.join(nodes_dir, f"{log['node_id'].lower()}.json")
    with open(filepath, "w", encoding="utf-8") as f:
        json.dump(log, f, indent=4)

print(f"Authored 15 Deep Lore Audio Logs to {nodes_dir}")
