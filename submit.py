import urllib.request
import os
import json

def get_submission_url():
    # If the environment has a specific submit URL, use it
    if 'SUBMIT_URL' in os.environ:
        return os.environ['SUBMIT_URL']

    # Try different common ports
    for port in [8080, 8000, 5000, 3000, 80]:
        try:
            url = f"http://127.0.0.1:{port}/submit"
            req = urllib.request.Request(url, data=b"", method="POST")
            with urllib.request.urlopen(req, timeout=1) as response:
                print(f"Submitted to {url}:", response.read().decode('utf-8'))
                return True
        except:
            pass

    print("Could not find submit endpoint.")
    return False

get_submission_url()
