import urllib.request
import json

data = json.dumps({"code": "Hecton8.Editor.TerrainRenderTestGoal.Execute();"}).encode('utf-8')
req = urllib.request.Request("http://localhost:8088/execute", data=data, headers={"Content-Type": "application/json"})

try:
    with urllib.request.urlopen(req) as response:
        result = response.read().decode('utf-8')
        print(result)
except Exception as e:
    print(f"Error: {e}")
