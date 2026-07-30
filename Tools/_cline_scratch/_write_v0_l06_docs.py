from pathlib import Path
import json

p = Path(r'C:/hades/Hecton8/Tools/_cline_scratch')
json_path = Path(r'C:/hades/Hecton8/Docs/AgentLogs/h8_playprobe_v0_L06.json')
data = json.loads(json_path.read_text(encoding='utf-8'))

