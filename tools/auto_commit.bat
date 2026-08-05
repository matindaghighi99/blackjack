@echo off
setlocal enabledelayedexpansion
title Blackjack - automated commit (no input needed)

call "%~dp0_find_unity.bat"

set "GIT_AUTHOR_NAME=Matin Daghighi"
set "GIT_AUTHOR_EMAIL=matindaghighi99@gmail.com"
set "GIT_COMMITTER_NAME=Matin Daghighi"
set "GIT_COMMITTER_EMAIL=matindaghighi99@gmail.com"

set "DONE=%LOGDIR%\COMMIT_DONE.txt"
if exist "%DONE%" del /q "%DONE%"

set "LOG=%LOGDIR%\git_art.txt"
> "%LOG%" echo === ART COMMIT %DATE% %TIME% ===

git -C "%ROOT%" config user.name  "Matin Daghighi"
git -C "%ROOT%" config user.email "matindaghighi99@gmail.com"

rem A stale index.lock silently blocks every subsequent git write. It gets left
rem behind when a git process is killed or cannot unlink the file. No git process
rem should be running here, so clearing it is safe.
if exist "%ROOT%\.git\index.lock" (
  >>"%LOG%" echo Removing stale .git\index.lock
  del /q /f "%ROOT%\.git\index.lock"
)

>>"%LOG%" echo --- git add ---
git -C "%ROOT%" add -A >>"%LOG%" 2>&1
if errorlevel 1 (
  >>"%LOG%" echo GIT ADD FAILED
  > "%DONE%" echo STATUS=ADD_FAILED
  exit /b 1
)
>>"%LOG%" echo --- staged ---
git -C "%ROOT%" status --short >>"%LOG%" 2>&1

git -C "%ROOT%" diff --cached --quiet
if not errorlevel 1 (
  > "%DONE%" echo STATUS=NOTHING_TO_COMMIT
  exit /b 0
)

git -C "%ROOT%" commit ^
  -m "feat(ui): derive the menu and store from their concept renders too" ^
  -m "All three screens now come from docs/screenshots rather than being redrawn. The menu keeps its painted crest, wordmark, tagline, house rules and corner props; the store keeps GET CHIPS, TAP TO PURCHASE and its corner chip stacks. Only the genuinely dynamic parts - top bars, buttons and pack rows - are rebuilt live and positioned in the renders own pixel coordinates." ^
  -m "Store rows are a real component now. StorePackRow carries artwork, amount, bonus, price pill and a BEST VALUE ribbon, replacing the single label that packed everything into one string and overflowed its button at both ends. The badge is derived from the best bonus-per-chip ratio in EconomyConfig rather than hard-coded to a row, so it follows the data if the packs are retuned." ^
  -m "Row frame, price pill, four chip stacks and the ribbon are all lifted from the store render; the chip art is keyed off the felt by sampling the background from a corner rather than assuming a colour, since the rows sit on slightly different greens." ^
  -m "Two animation bugs fixed on the way. Cards hidden mid-deal kept Travel below 1 forever because Update skips inactive objects, which left IsAnimating stuck true and hung the play-mode test against its frame budget. And Render restarted an in-progress flip on every Refresh; it now ignores a flip already heading for the same face." ^
  -m "The test was also caching StandButton across a state change - hitting can bust the hand, which settles the round and hides the action row - so it now resolves the button inside the loop while the phase is known." >>"%LOG%" 2>&1
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
  > "%DONE%" echo STATUS=COMMIT_FAILED
  >>"%DONE%" echo RC=%RC%
  exit /b %RC%
)

git -C "%ROOT%" push origin main >>"%LOG%" 2>&1
set "RC=%ERRORLEVEL%"

git -C "%ROOT%" log -1 --pretty=fuller >>"%LOG%" 2>&1

if "%RC%"=="0" (
  > "%DONE%" echo STATUS=PUSHED
) else (
  > "%DONE%" echo STATUS=PUSH_FAILED
  >>"%DONE%" echo RC=%RC%
)
>>"%DONE%" echo FINISHED=%DATE% %TIME%

endlocal
exit /b 0
