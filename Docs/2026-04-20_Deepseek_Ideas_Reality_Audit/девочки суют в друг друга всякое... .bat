@echo off
setlocal disabledelayedexpansion
title ДЕМОНИЧЕСКИЙ ПСИХОЗ v999
mode con: cols=95 lines=30
color 0a
cls

echo.
echo   ╔══════════════════════════════════════════════════════╗
echo   ║  ВНИМАНИЕ!!! ЗАПУЩЕН ПРОТОКОЛ "ЦИФРОВОЙ АД"          ║
echo   ║  Сейчас тут начнётся такое, что мама не горюй...     ║
echo   ╚══════════════════════════════════════════════════════╝
echo.
echo   Подключение потусторонних модулей...
ping -n 3 127.0.0.1 >nul

:: ====== ПРОВЕРКА PowerShell (для голоса и окон ужаса) ======
where powershell >nul 2>&1
if %errorlevel% equ 0 (
    set "HAVE_PS=1"
    echo   [OK] Обнаружен PowerShell. Активируем ГОЛОС БЕЗДНЫ...
    start /min "" powershell -Command "$voice=New-Object -ComObject SAPI.SpVoice; while($true){ $voice.Speak('Ты в матрице, кожаный ублюдок!'); Start-Sleep -Milliseconds 700; $voice.Speak('Файлы приносятся в жертву Сатане!'); Start-Sleep -Milliseconds 700; $voice.Speak('Системный демон уже внутри!'); Start-Sleep -Milliseconds 700 }"
    start "" powershell -Command "Add-Type -AssemblyName System.Windows.Forms; while($true){ [System.Windows.Forms.MessageBox]::Show('ТВОЙ ЦИФРОВОЙ ДОМ ПАЛ','ДЕМОНЫ БЛИЗКО','OK','Error'); Start-Sleep -Seconds 4 }"
) else (
    echo   [WARN] PowerShell не найден — будет просто текстовый ад...
)

:: ====== ОКНО ФЕЙКОВОГО ФОРМАТИРОВАНИЯ ======
start "ФОРМАТИРОВАНИЕ АДА" cmd /v:on /c "color 0c & mode con cols=75 lines=12 & echo ========================================= & echo   ФОРМАТИРОВАНИЕ C: НАЧАТО... & echo ========================================= & for /L %i in (0,2,100) do (echo Выполнено %i процентов & ping -n 1 127.0.0.1>nul) & echo. & echo ДИСК УНИЧТОЖЕН. ХА-ХА-ХА! & pause>nul"

:: ====== ОКНО ЛЕГИОНА БЕСОВ ======
start "ЛЕГИОН БЕСОВ" cmd /v:on /c "color 0e & mode con cols=65 lines=22 & echo ПРОНИКНОВЕНИЕ БЕСОВ В СИСТЕМУ & echo. & set n=1 & :loop & if ^!n^! gtr 77 goto end & set /a pid=%%random%% %%%% 9999 +1 & echo Бес №^!n^! вселяется в процесс PID ^!pid^!... & set /a n+=1 & ping -n 0.3 127.0.0.1>nul & goto loop & :end & echo. & echo ВСЕ ПРОЦЕССЫ ОДЕРЖИМЫ! & pause>nul"

:: ====== ОКНО ГЛЮЧНОЙ МАТРИЦЫ ======
start "ГЛЮЧНАЯ МАТРИЦА" cmd /c "color 0a & mode con cols=85 lines=26 & :glitch & cls & echo %DATE% %TIME% - KERNEL PANIC IN HELL & echo. & for /L %x in (1,1,10) do (echo %RANDOM%%RANDOM%%RANDOM% ОШИБКА АДРЕСА 0xDEAD%RANDOM% & ping -n 0.1 127.0.0.1>nul) & goto glitch"

:: ====== ГЛАВНОЕ ОКНО – ТАНЦУЮЩИЙ ДЕМОН ======
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
echo      ║        ЦИФРОВОЙ ДЕМОН ПРАЗДНУЕТ!!            ║
echo      ╚══════════════════════════════════════════════╝
echo.
echo    СЛУЧАЙНОЕ ПРОРОЧЕСТВО:
set /a msg=%random% %% 5
if %msg%==0 echo    "Твой C: станет бездной!"
if %msg%==1 echo    "Вирусы-демоны уже в BIOS!"
if %msg%==2 echo    "Ядерная бомба активирована!"
if %msg%==3 echo    "Кожаный мешок, ты в матрице!"
if %msg%==4 echo    "Windows молится и удаляется..."
echo.
echo    НАЖМИ Ctrl+C ЧТОБЫ ПОПЫТАТЬСЯ ОСТАНОВИТЬ ЭТОТ АД
echo    (но бесы в других окнах останутся, МУА-ХА-ХА!)
ping -n 1 127.0.0.1>nul
endlocal
goto mainloop