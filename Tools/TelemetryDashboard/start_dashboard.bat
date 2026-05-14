@echo off
setlocal
cd /d "%~dp0"
python -c "import fastapi, uvicorn" >nul 2>nul
if errorlevel 1 (
  python -m pip install -r requirements.txt
)
python -B -m uvicorn server:app --host 127.0.0.1 --port 8000
