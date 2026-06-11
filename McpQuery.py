import subprocess
import json
import time

uvx_cmd = [
    r"C:\Users\danat\.local\bin\uvx.exe", "--offline", "--prerelease", "explicit",
    "--from", "mcpforunityserver>=0.0.0a0", "mcp-for-unity",
    "--transport", "stdio", "--default-instance", "Hecton8", "--project-scoped-tools"
]

proc = subprocess.Popen(uvx_cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)

# Helper to send JSON-RPC
message_id = 1
def send_request(method, params=None):
    global message_id
    req = {"jsonrpc": "2.0", "id": message_id, "method": method}
    if params is not None:
        req["params"] = params
    message_id += 1
    proc.stdin.write(json.dumps(req) + "\n")
    proc.stdin.flush()

send_request("initialize", {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test", "version": "1.0"}})
send_request("notifications/initialized")
send_request("tools/list")

time.sleep(2)

# Read output
while True:
    try:
        line = proc.stdout.readline()
        if not line: break
        print(line.strip())
        if "tools/list" in line or "unityMCP" in line:
            pass
    except Exception as e:
        print(e)
        break

proc.terminate()
