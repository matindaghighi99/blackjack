@echo off
setlocal enabledelayedexpansion
title Blackjack - Step 1: Unity import

call "%~dp0_find_unity.bat"

echo ===========================================================
echo  STEP 1 - Import the project and compile all scripts
echo ===========================================================
echo  Project : %ROOT%
echo  Logs    : %LOGDIR%
echo.

rem --- environment snapshot (Claude reads this) ------------------------------
set "ENVLOG=%LOGDIR%\env.txt"
> "%ENVLOG%" echo === ENVIRONMENT %DATE% %TIME% ===
>>"%ENVLOG%" echo ROOT=%ROOT%
>>"%ENVLOG%" echo UNITY=%UNITY%
>>"%ENVLOG%" echo.
>>"%ENVLOG%" echo --- Unity Hub editors found ---
for %%R in (
  "C:\Program Files\Unity\Hub\Editor"
  "C:\Program Files (x86)\Unity\Hub\Editor"
  "D:\Program Files\Unity\Hub\Editor"
  "D:\Unity\Hub\Editor"
  "E:\Unity\Hub\Editor"
) do (
  if exist "%%~R" (
    >>"%ENVLOG%" echo [%%~R]
    dir /b /ad "%%~R" >>"%ENVLOG%" 2>&1
  )
)
>>"%ENVLOG%" echo.
>>"%ENVLOG%" echo --- git ---
git --version >>"%ENVLOG%" 2>&1
git -C "%ROOT%" status --short >>"%ENVLOG%" 2>&1
>>"%ENVLOG%" echo.
>>"%ENVLOG%" echo --- running Unity processes ---
tasklist /fi "imagename eq Unity.exe" >>"%ENVLOG%" 2>&1
tasklist /fi "imagename eq Unity Hub.exe" >>"%ENVLOG%" 2>&1

if not defined UNITY (
  echo [FAIL] Could not find a Unity 6000.0.x editor.
  echo        See %ENVLOG% for the folders that were searched.
  echo        If Unity lives somewhere unusual, run:
  echo            set UNITY_EXE=D:\Your\Path\Unity.exe
  echo        in this window, then run this script again.
  echo.
  pause
  exit /b 1
)

tasklist /fi "imagename eq Unity.exe" 2>nul | find /i "Unity.exe" >nul
if not errorlevel 1 (
  echo [FAIL] A Unity Editor is already running and holds a lock on the project.
  echo        Close it, then run this script again.
  echo.
  pause
  exit /b 3
)

echo Using: %UNITY%
echo.
echo Importing... first import generates .meta files and resolves packages.
echo This can take several minutes. Please wait.
echo.

"%UNITY%" -batchmode -quit -nographics -projectPath "%ROOT%" -logFile "%LOGDIR%\import.log" -accept-apiupdate
set "RC=%ERRORLEVEL%"

echo.
echo Unity exit code: %RC%
> "%LOGDIR%\import_exitcode.txt" echo %RC%

rem --- pull compile errors out of the log for quick reading ------------------
findstr /n /c:"error CS" /c:"Compilation failed" /c:"Assembly has reference" /c:"error:" "%LOGDIR%\import.log" > "%LOGDIR%\import_errors.txt" 2>nul
for /f %%A in ('type "%LOGDIR%\import_errors.txt" ^| find /c /v ""') do set "ERRCOUNT=%%A"

echo Compile-error lines found: %ERRCOUNT%
echo.
if "%RC%"=="0" (
  if "%ERRCOUNT%"=="0" (
    echo [OK] Project imported and compiled with zero errors.
  ) else (
    echo [WARN] Unity exited 0 but the log mentions errors - see:
    echo        %LOGDIR%\import_errors.txt
  )
) else (
  echo [FAIL] Unity exited non-zero. See %LOGDIR%\import.log
)

echo.
echo Tell Claude that step 1 finished. It reads the logs directly.
echo.
pause
endlocal
