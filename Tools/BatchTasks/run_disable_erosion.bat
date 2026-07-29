REM -nographics deliberately ABSENT: graph mutator - does not render, but the ban names it and a future edit could add generation. With no GPU context compute shaders and Graphics.Blit
REM return ZEROS with no error, so the tool produces plausible-looking data that is entirely
REM fabricated - see .claude/rules/hecton8-shaders-compute.md:36-37. Do not re-add it.
"C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -projectPath "C:\hades\Hecton8" -executeMethod "MapMagic.Editor.Diagnostics.DisableErosionNodeTask.DisableErosionNodeBatchmode" -batchmode -logFile "C:\hades\Hecton8\Logs\disable_erosion_unity.log"
