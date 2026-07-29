REM -nographics deliberately ABSENT: Stage3PlaymodeTest captures splatmaps and screenshots. With no GPU context compute shaders and Graphics.Blit
REM return ZEROS with no error, so the tool produces plausible-looking data that is entirely
REM fabricated - see .claude/rules/hecton8-shaders-compute.md:36-37. Do not re-add it.
"C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -projectPath "C:\hades\Hecton8" -executeMethod "Hecton8.Editor.Diagnostics.Stage3PlaymodeTest.RunTest" -batchmode -logFile "C:\hades\Hecton8\Logs\Stage3PlaymodeTest.log"
