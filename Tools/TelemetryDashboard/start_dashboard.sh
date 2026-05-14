#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")"
python -c "import fastapi, uvicorn" >/dev/null 2>&1 || python -m pip install -r requirements.txt
python -B -m uvicorn server:app --host 127.0.0.1 --port 8000
