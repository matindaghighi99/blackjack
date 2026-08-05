@echo off
setlocal enabledelayedexpansion
title Blackjack - automated run (no input needed)

rem ---------------------------------------------------------------------------
rem  Unattended version of steps 1 and 3. No prompts and no PAUSE, so it closes
rem  itself when finished - Claude polls tools\logs\DONE.txt to know the result
rem  rather than needing to press a key in this console window.
rem ---------------------------------------------------------------------------

call "%~dp0_find_unity.bat"

set "DONE=%LOGDIR%\DONE.txt"
if exist "%DONE%" del /q "%DONE%"

set "RC_IMPORT=skipped"
set "RC_BOOTSTRAP=skipped"
set "RC_EDIT=skipped"
set "RC_PLAY=skipped"

echo Project : %ROOT%
echo Unity   : %UNITY%
echo.

if not defined UNITY (
  > "%DONE%" echo STATUS=FAIL
  >>"%DONE%" echo REASON=no Unity 6000.x editor found
  exit /b 1
)

tasklist /fi "imagename eq Unity.exe" 2>nul | find /i "Unity.exe" >nul
if not errorlevel 1 (
  > "%DONE%" echo STATUS=FAIL
  >>"%DONE%" echo REASON=a Unity Editor is running and holds the project lock
  exit /b 3
)

echo [1/4] Importing project ^(new art + scripts^)...
"%UNITY%" -batchmode -quit -nographics -projectPath "%ROOT%" -logFile "%LOGDIR%\import.log" -accept-apiupdate
set "RC_IMPORT=%ERRORLEVEL%"
> "%LOGDIR%\import_exitcode.txt" echo %RC_IMPORT%
findstr /n /c:"error CS" /c:"Compilation failed" "%LOGDIR%\import.log" > "%LOGDIR%\import_errors.txt" 2>nul
echo       exit %RC_IMPORT%
if not "%RC_IMPORT%"=="0" goto :finish

echo [2/4] Building and wiring scenes...
"%UNITY%" -batchmode -nographics -projectPath "%ROOT%" -logFile "%LOGDIR%\bootstrap.log" ^
  -executeMethod BlackjackGame.EditorTools.SceneBootstrap.BuildAllFromCommandLine
set "RC_BOOTSTRAP=%ERRORLEVEL%"
> "%LOGDIR%\bootstrap_exitcode.txt" echo %RC_BOOTSTRAP%
findstr /n /c:"error CS" /c:"SceneBootstrap" /c:"Exception" "%LOGDIR%\bootstrap.log" > "%LOGDIR%\bootstrap_summary.txt" 2>nul
echo       exit %RC_BOOTSTRAP%
if not "%RC_BOOTSTRAP%"=="0" goto :finish

echo [3/4] EditMode tests...
"%UNITY%" -batchmode -nographics -projectPath "%ROOT%" -logFile "%LOGDIR%\tests_editmode.log" ^
  -runTests -testPlatform EditMode -testResults "%LOGDIR%\results_editmode.xml"
set "RC_EDIT=%ERRORLEVEL%"
> "%LOGDIR%\tests_editmode_exitcode.txt" echo %RC_EDIT%
echo       exit %RC_EDIT%

echo [4/4] PlayMode tests...
"%UNITY%" -batchmode -projectPath "%ROOT%" -logFile "%LOGDIR%\tests_playmode.log" ^
  -runTests -testPlatform PlayMode -testResults "%LOGDIR%\results_playmode.xml"
set "RC_PLAY=%ERRORLEVEL%"
> "%LOGDIR%\tests_playmode_exitcode.txt" echo %RC_PLAY%
echo       exit %RC_PLAY%

:finish
> "%DONE%" echo STATUS=COMPLETE
>>"%DONE%" echo IMPORT=%RC_IMPORT%
>>"%DONE%" echo BOOTSTRAP=%RC_BOOTSTRAP%
>>"%DONE%" echo EDITMODE=%RC_EDIT%
>>"%DONE%" echo PLAYMODE=%RC_PLAY%
>>"%DONE%" echo FINISHED=%DATE% %TIME%

echo.
echo Finished. Closing.
endlocal
exit /b 0
