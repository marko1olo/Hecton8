import os
import json
import re

base_dir = r"C:\hades\Hecton8\Docs\Lore\AppliedContent"
rs_dir = os.path.join(base_dir, "release_sets")
out_json = os.path.join(base_dir, "compiled_lore_database.json")

database = {
    "version": "1.0.0",
    "compiler_date": "2026-06-06",
    "packets": []
}

def parse_markdown_to_json(filepath):
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()
        
    # Split by packet
    packets_raw = content.split("## Packet ")
    if len(packets_raw) < 2:
        return []
        
    parsed_packets = []
    
    # Skip the first split item, which is just the file header
    for p_raw in packets_raw[1:]:
        lines = p_raw.strip().split("\n")
        
        packet_id = lines[0].strip()
        packet_data = {
            "packet_id": packet_id,
            "metadata": {},
            "surfaces": {}
        }
        
        current_surface = None
        surface_buffer = []
        
        for line in lines[1:]:
            line = line.strip()
            if not line:
                continue
                
            # Parse metadata list items
            if line.startswith("- **") and ":" in line:
                key_match = re.search(r'\*\*(.*?)\*\*:\s*(.*)', line)
                if key_match:
                    key = key_match.group(1).lower().replace(" ", "_")
                    val = key_match.group(2)
                    packet_data["metadata"][key] = val
                continue
                
            # Parse surface headers
            if line.startswith("### "):
                if current_surface and surface_buffer:
                    packet_data["surfaces"][current_surface] = " ".join(surface_buffer)
                current_surface = line[4:].strip().lower().replace(" ", "_").replace("/", "_")
                surface_buffer = []
                continue
                
            # Collect surface content
            if current_surface:
                surface_buffer.append(line)
                
        # Flush last surface
        if current_surface and surface_buffer:
            packet_data["surfaces"][current_surface] = " ".join(surface_buffer)
            
        parsed_packets.append(packet_data)
        
    return parsed_packets

print("Compiling HECTON-8 Lore Database...")

total_packets = 0
for filename in os.listdir(rs_dir):
    if filename.endswith(".md"):
        filepath = os.path.join(rs_dir, filename)
        packets = parse_markdown_to_json(filepath)
        database["packets"].extend(packets)
        total_packets += len(packets)

with open(out_json, "w", encoding="utf-8") as f:
    json.dump(database, f, indent=4)

print(f"Compilation complete. {total_packets} packets written to {out_json}")
