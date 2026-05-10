@echo off
setlocal disabledelayedexpansion
title DEMONIChESKIY PSIHOZ v999
mode con: cols=95 lines=30
color 0a
cls

echo.
echo   ╔══════════════════════════════════════════════════════╗
echo   ║  VNIMANIE!!! ZAPUSchEN PROTOKOL "TsIFROVOY AD"          ║
echo   ║  Seychas tut nachnetsya takoe, chto mama ne goryuy...     ║
echo   ╚══════════════════════════════════════════════════════╝
echo.
echo   Podklyuchenie potustoronnih moduley...
ping -n 3 127.0.0.1 >nul

:: ====== PROVERKA PowerShell (dlya golosa i okon uzhasa) ======
where powershell >nul 2>&1
if %errorlevel% equ 0 (
    set "HAVE_PS=1"
    echo   [OK] Obnaruzhen PowerShell. Aktiviruem GOLOS BEZDNY...
    start /min "" powershell -Command "$voice=New-Object -ComObject SAPI.SpVoice; while($true){ $voice.Speak('Ty v matritse, kozhanyy ublyudok!'); Start-Sleep -Milliseconds 700; $voice.Speak('Fayly prinosyatsya v zhertvu Satane!'); Start-Sleep -Milliseconds 700; $voice.Speak('Sistemnyy demon uzhe vnutri!'); Start-Sleep -Milliseconds 700 }"
    start "" powershell -Command "Add-Type -AssemblyName System.Windows.Forms; while($true){ [System.Windows.Forms.MessageBox]::Show('TVOY TsIFROVOY DOM PAL','DEMONY BLIZKO','OK','Error'); Start-Sleep -Seconds 4 }"
) else (
    echo   [WARN] PowerShell ne nayden — budet prosto tekstovyy ad...
)

:: ====== OKNO FEYKOVOGO FORMATIROVANIYa ======
start "FORMATIROVANIE ADA" cmd /v:on /c "color 0c & mode con cols=75 lines=12 & echo ========================================= & echo   FORMATIROVANIE C: NAChATO... & echo ========================================= & for /L %i in (0,2,100) do (echo Vypolneno %i protsentov & ping -n 1 127.0.0.1>nul) & echo. & echo DISK UNIChTOZhEN. HA-HA-HA! & pause>nul"

:: ====== OKNO LEGIONA BESOV ======
start "LEGION BESOV" cmd /v:on /c "color 0e & mode con cols=65 lines=22 & echo PRONIKNOVENIE BESOV V SISTEMU & echo. & set n=1 & :loop & if ^!n^! gtr 77 goto end & set /a pid=%%random%% %%%% 9999 +1 & echo Bes №^!n^! vselyaetsya v protsess PID ^!pid^!... & set /a n+=1 & ping -n 0.3 127.0.0.1>nul & goto loop & :end & echo. & echo VSE PROTsESSY ODERZhIMY! & pause>nul"

:: ====== OKNO GLYuChNOY MATRITsY ======
start "GLYuChNAYa MATRITsA" cmd /c "color 0a & mode con cols=85 lines=26 & :glitch & cls & echo %DATE% %TIME% - KERNEL PANIC IN HELL & echo. & for /L %x in (1,1,10) do (echo %RANDOM%%RANDOM%%RANDOM% OShIBKA ADRESA 0xDEAD%RANDOM% & ping -n 0.1 127.0.0.1>nul) & goto glitch"

:: ====== GLAVNOE OKNO – TANTsUYuSchIY DEMON ======
:mainloop
setlocal enabledelayedexpansion
set /a cc=%random% %% 4
if %cc%==0 color 0c
if %cc%==1 color 0a
if %cc%==2 color 0e
if %cc%==3 color 04
cls
echo.
echo         ██╗   ██╗ █████╗ ██╗   ██╗██╗   ██╗██╗   ██╗
echo         ██║   ██║██╔══██╗██║   ██║██║   ██║╚██╗ ██╔╝
echo         ██║   ██║███████║██║   ██║██║   ██║ ╚████╔╝ 
echo         ╚██╗ ██╔╝██╔══██║██║   ██║██║   ██║  ╚██╔╝  
echo          ╚████╔╝ ██║  ██║╚██████╔╝╚██████╔╝   ██║   
echo           ╚═══╝  ╚═╝  ╚═╝ ╚═════╝  ╚═════╝    ╚═╝   
echo.
echo      ╔══════════════════════════════════════════════╗
echo      ║        TsIFROVOY DEMON PRAZDNUET!!            ║
echo      ╚══════════════════════════════════════════════╝
echo.
echo    SLUChAYNOE PROROChESTVO:
set /a msg=%random% %% 5
if %msg%==0 echo    "Tvoy C: stanet bezdnoy!"
if %msg%==1 echo    "Virusy-demony uzhe v BIOS!"
if %msg%==2 echo    "Yadernaya bomba aktivirovana!"
if %msg%==3 echo    "Kozhanyy meshok, ty v matritse!"
if %msg%==4 echo    "Windows molitsya i udalyaetsya..."
echo.
echo    NAZhMI Ctrl+C ChTOBY POPYTATSYa OSTANOVIT ETOT AD
echo    (no besy v drugih oknah ostanutsya, MUA-HA-HA!)
ping -n 1 127.0.0.1>nul
endlocal
goto mainloop