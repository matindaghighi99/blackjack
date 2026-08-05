@echo off
setlocal enabledelayedexpansion
title Blackjack - Step 4: commit wired scenes

call "%~dp0_find_unity.bat"

set "GIT_AUTHOR_NAME=Matin Daghighi"
set "GIT_AUTHOR_EMAIL=matindaghighi99@gmail.com"
set "GIT_COMMITTER_NAME=Matin Daghighi"
set "GIT_COMMITTER_EMAIL=matindaghighi99@gmail.com"

echo ===========================================================
echo  STEP 4 - Commit the wired scenes + editor bootstrap
echo ===========================================================
echo.

git -C "%ROOT%" config user.name  "Matin Daghighi"
git -C "%ROOT%" config user.email "matindaghighi99@gmail.com"

set "LOG=%LOGDIR%\git_final.txt"
> "%LOG%" echo === GIT FINAL COMMIT %DATE% %TIME% ===

rem Unity 6.3 generates a new-format VS solution (.slnx). It got committed before
rem .gitignore covered it - untrack it here (the file stays on disk).
git -C "%ROOT%" rm --cached -q --ignore-unmatch "blackjack-social-casino.slnx" >>"%LOG%" 2>&1

git -C "%ROOT%" add -A
git -C "%ROOT%" status --short >>"%LOG%" 2>&1
git -C "%ROOT%" status --short

git -C "%ROOT%" diff --cached --quiet
if not errorlevel 1 (
  echo.
  echo [SKIP] Nothing staged.
  >>"%LOG%" echo NOTHING TO COMMIT
  pause
  exit /b 0
)

echo.
echo Committing...
git -C "%ROOT%" commit -m "feat: wire up UI scenes + editor bootstrap" >>"%LOG%" 2>&1
set "RC=%ERRORLEVEL%"
echo Commit exit code: %RC%
if not "%RC%"=="0" (
  echo [FAIL] Commit failed - see %LOG%
  type "%LOG%"
  pause
  exit /b %RC%
)

echo Pushing to origin/main...
git -C "%ROOT%" push origin main >>"%LOG%" 2>&1
set "RC=%ERRORLEVEL%"
echo Push exit code: %RC%

>>"%LOG%" echo --- last 3 commits, full authorship ---
git -C "%ROOT%" log -3 --pretty=fuller >>"%LOG%" 2>&1
git -C "%ROOT%" log -3 --pretty=oneline

echo.
if "%RC%"=="0" (echo [OK] Committed and pushed to origin/main.) else (echo [FAIL] Push failed - see %LOG%)
echo.
echo Tell Claude that step 4 finished.
echo.
pause
endlocal
