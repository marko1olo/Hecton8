@echo off
setlocal
set REPO=C:\hades\Hecton8
set OUT=%REPO%\Tools\_cline_scratch\push_out.txt
cd /d "%REPO%"
echo start %DATE% %TIME%> "%OUT%"
git log -5 --oneline >> "%OUT%" 2>&1
git status -sb >> "%OUT%" 2>&1
echo PUSHING>> "%OUT%"
git push gitlab main >> "%OUT%" 2>&1
echo push_ec=%ERRORLEVEL%>> "%OUT%"
git status -sb >> "%OUT%" 2>&1
git log -3 --oneline >> "%OUT%" 2>&1
echo done %DATE% %TIME%>> "%OUT%"
