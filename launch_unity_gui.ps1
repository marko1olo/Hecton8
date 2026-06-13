$u = Start-Process -FilePath "C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" -ArgumentList "-projectPath C:\hades\Hecton8" -PassThru
Start-Sleep -Seconds 30
python C:\Users\danat\.gemini\antigravity\brain\abe2d500-b2d1-4409-97a4-29263e0d7c11\scratch\capture_scene_only.py
Stop-Process -Id $u.Id -Force
