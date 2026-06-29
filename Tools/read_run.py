import re

def read_method():
    with open('Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridge1504StaticAudit.cs', 'r') as f:
        content = f.read()

    # Find the Run method
    match = re.search(r'(internal static void Run\(string projectRoot\).*?\n        })', content, re.DOTALL)
    if match:
        method = match.group(1)
        print(f"Run method length: {len(method.splitlines())} lines.")

        # Print the start and end of the JSON building part
        json_start = method.find('StringBuilder builder = new StringBuilder(4096);')
        if json_start != -1:
            print("JSON building starts here.")
            # print(method[json_start:json_start+500])

read_method()
