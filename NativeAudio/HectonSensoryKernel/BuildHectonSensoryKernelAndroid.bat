@echo off
setlocal EnableExtensions

set "ROOT=%~dp0"
set "PROJECT_ROOT=%ROOT%..\.."
set "OUTDIR=%PROJECT_ROOT%\Assets\Plugins\Android\arm64-v8a"
set "INTDIR=%ROOT%build\android-arm64"
set "NDK_ROOT=%~1"

if defined NDK_ROOT goto :ValidateNdk
if defined ANDROID_NDK_ROOT set "NDK_ROOT=%ANDROID_NDK_ROOT%"
if defined NDK_ROOT goto :ValidateNdk
if defined ANDROID_NDK_HOME set "NDK_ROOT=%ANDROID_NDK_HOME%"
if defined NDK_ROOT goto :ValidateNdk
if defined ANDROID_NDK set "NDK_ROOT=%ANDROID_NDK%"
if defined NDK_ROOT goto :ValidateNdk

call :FindUnityHubNdk
if defined NDK_ROOT goto :ValidateNdk

echo [HectonAudioKernel] Android NDK not found.
echo Pass NDK root as the first argument or set ANDROID_NDK_ROOT.
exit /b 2

:ValidateNdk
set "CLANG_CMD=%NDK_ROOT%\toolchains\llvm\prebuilt\windows-x86_64\bin\aarch64-linux-android24-clang++.cmd"
if exist "%CLANG_CMD%" goto :Build
set "CLANG_CMD=%NDK_ROOT%\toolchains\llvm\prebuilt\windows-x86_64\bin\aarch64-linux-android24-clang++.exe"
if exist "%CLANG_CMD%" goto :Build

echo [HectonAudioKernel] aarch64-linux-android24-clang++ was not found under "%NDK_ROOT%".
exit /b 3

:Build
if not exist "%OUTDIR%" mkdir "%OUTDIR%"
if errorlevel 1 exit /b 4
if not exist "%INTDIR%" mkdir "%INTDIR%"
if errorlevel 1 exit /b 4

"%CLANG_CMD%" ^
  -shared ^
  -O2 ^
  -fPIC ^
  -std=c++11 ^
  -fvisibility=hidden ^
  -ffunction-sections -fdata-sections ^
  -fno-exceptions ^
  -fno-rtti ^
  -D__ANDROID_API__=24 ^
  -I"%ROOT%" ^
  -o "%OUTDIR%\libHectonAudioKernel.so" ^
  "%ROOT%Plugin_HectonSensoryKernel.cpp" ^
  -Wl,--gc-sections ^
  -Wl,--no-undefined
if errorlevel 1 exit /b 5

echo [HectonAudioKernel] Built Android arm64 plugin: %OUTDIR%\libHectonAudioKernel.so
exit /b 0

:FindUnityHubNdk
for /d %%D in ("%ProgramFiles%\Unity\Hub\Editor\*") do (
    if exist "%%~fD\Editor\Data\PlaybackEngines\AndroidPlayer\NDK\toolchains\llvm\prebuilt\windows-x86_64\bin\aarch64-linux-android24-clang++.cmd" (
        set "NDK_ROOT=%%~fD\Editor\Data\PlaybackEngines\AndroidPlayer\NDK"
        exit /b 0
    )
    if exist "%%~fD\Editor\Data\PlaybackEngines\AndroidPlayer\NDK\toolchains\llvm\prebuilt\windows-x86_64\bin\aarch64-linux-android24-clang++.exe" (
        set "NDK_ROOT=%%~fD\Editor\Data\PlaybackEngines\AndroidPlayer\NDK"
        exit /b 0
    )
)

for /d %%D in ("%ProgramFiles(x86)%\Unity\Hub\Editor\*") do (
    if exist "%%~fD\Editor\Data\PlaybackEngines\AndroidPlayer\NDK\toolchains\llvm\prebuilt\windows-x86_64\bin\aarch64-linux-android24-clang++.cmd" (
        set "NDK_ROOT=%%~fD\Editor\Data\PlaybackEngines\AndroidPlayer\NDK"
        exit /b 0
    )
    if exist "%%~fD\Editor\Data\PlaybackEngines\AndroidPlayer\NDK\toolchains\llvm\prebuilt\windows-x86_64\bin\aarch64-linux-android24-clang++.exe" (
        set "NDK_ROOT=%%~fD\Editor\Data\PlaybackEngines\AndroidPlayer\NDK"
        exit /b 0
    )
)

exit /b 0
