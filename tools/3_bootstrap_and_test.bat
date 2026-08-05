@echo off
setlocal enabledelayedexpansion
title Blackjack - Step 3: build scenes + run tests

call "%~dp0_find_unity.bat"

echo ===========================================================
echo  STEP 3 - Build/wire the UI scenes, then run the tests
echo ===========================================================
echo  Project : %ROOT%
echo.

if not defined UNITY (
  echo [FAIL] Could not find a Unity 6000.0.x editor. Run 1_import.bat first.
  pause
  exit /b 1
)

tasklist /fi "imagename eq Unity.exe" 2>nul | find /i "Unity.exe" >nul
if not errorlevel 1 (
  echo [FAIL] A Unity Editor is running and holds the project lock. Close it first.
  pause
  exit /b 3
)

echo Using: %UNITY%
echo.
echo [3a] Building and wiring MainMenu / Game / Store...
"%UNITY%" -batchmode -nographics -projectPath "%ROOT%" -logFile "%LOGDIR%\bootstrap.log" ^
  -executeMethod BlackjackGame.EditorTools.SceneBootstrap.BuildAllFromCommandLine
set "RC_BOOTSTRAP=%ERRORLEVEL%"
echo      exit code: %RC_BOOTSTRAP%
> "%LOGDIR%\bootstrap_exitcode.txt" echo %RC_BOOTSTRAP%
findstr /n /c:"error CS" /c:"SceneBootstrap" /c:"Exception" "%LOGDIR%\bootstrap.log" > "%LOGDIR%\bootstrap_summary.txt" 2>nul

if not "%RC_BOOTSTRAP%"=="0" (
  echo.
  echo [FAIL] Scene bootstrap failed. See %LOGDIR%\bootstrap.log
  echo        Skipping tests.
  echo.
  pause
  exit /b %RC_BOOTSTRAP%
)

echo.
echo [3b] Running EditMode tests...
"%UNITY%" -batchmode -nographics -projectPath "%ROOT%" -logFile "%LOGDIR%\tests_editmode.log" ^
  -runTests -testPlatform EditMode -testResults "%LOGDIR%\results_editmode.xml"
set "RC_EDIT=%ERRORLEVEL%"
echo      exit code: %RC_EDIT%
> "%LOGDIR%\tests_editmode_exitcode.txt" echo %RC_EDIT%

echo.
echo [3c] Running PlayMode tests (this actually enters Play mode and plays a round)...
"%UNITY%" -batchmode -projectPath "%ROOT%" -logFile "%LOGDIR%\tests_playmode.log" ^
  -runTests -testPlatform PlayMode -testResults "%LOGDIR%\results_playmode.xml"
set "RC_PLAY=%ERRORLEVEL%"
echo      exit code: %RC_PLAY%
> "%LOGDIR%\tests_playmode_exitcode.txt" echo %RC_PLAY%

echo.
echo ===========================================================
echo  bootstrap : %RC_BOOTSTRAP%   (0 = ok)
echo  EditMode  : %RC_EDIT%        (0 = all passed)
echo  PlayMode  : %RC_PLAY%        (0 = all passed)
echo ===========================================================
echo.
echo Tell Claude that step 3 finished. It reads the logs and XML results directly.
echo.
pause
endlocal
