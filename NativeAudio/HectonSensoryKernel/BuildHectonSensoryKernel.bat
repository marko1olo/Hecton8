@echo off
setlocal

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo vswhere.exe not found.
    exit /b 1
)

for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSINSTALL=%%i"
if "%VSINSTALL%"=="" (
    echo Visual Studio C++ Build Tools not found.
    exit /b 1
)

call "%VSINSTALL%\VC\Auxiliary\Build\vcvars64.bat"
if errorlevel 1 exit /b %errorlevel%

set "ROOT=%~dp0"
set "OUTDIR=%ROOT%..\..\Assets\Plugins\x86_64"
set "INTDIR=%ROOT%build"
if not exist "%OUTDIR%" mkdir "%OUTDIR%"
if not exist "%INTDIR%" mkdir "%INTDIR%"

cl /nologo /LD /O2 /MT /GR- /EHsc- /Gy /Gw ^
   /Fo"%INTDIR%\\" ^
   /Fe"%OUTDIR%\HectonAudioKernel.dll" ^
   "%ROOT%Plugin_HectonSensoryKernel.cpp" ^
   winmm.lib user32.lib kernel32.lib ^
   /link /OPT:REF /OPT:ICF
if errorlevel 1 exit /b %errorlevel%

echo Built %OUTDIR%\HectonAudioKernel.dll
