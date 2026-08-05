@echo off
setlocal enabledelayedexpansion
title Blackjack - Step 2: commit Unity .meta files

call "%~dp0_find_unity.bat"

rem Author AND committer are forced here so nothing else can claim the commit.
set "GIT_AUTHOR_NAME=Matin Daghighi"
set "GIT_AUTHOR_EMAIL=matindaghighi99@gmail.com"
set "GIT_COMMITTER_NAME=Matin Daghighi"
set "GIT_COMMITTER_EMAIL=matindaghighi99@gmail.com"

echo ===========================================================
echo  STEP 2 - Commit the Unity-generated .meta files
echo ===========================================================
echo.

git -C "%ROOT%" config user.name  "Matin Daghighi"
git -C "%ROOT%" config user.email "matindaghighi99@gmail.com"

set "LOG=%LOGDIR%\git_meta.txt"
> "%LOG%" echo === GIT META COMMIT %DATE% %TIME% ===

echo --- What will be committed (first 60 entries) ---
git -C "%ROOT%" add -A
git -C "%ROOT%" status --short >>"%LOG%" 2>&1
git -C "%ROOT%" status --short

git -C "%ROOT%" diff --cached --quiet
if not errorlevel 1 (
  echo.
  echo [SKIP] Nothing staged - already committed?
  >>"%LOG%" echo NOTHING TO COMMIT
  echo.
  pause
  exit /b 0
)

echo.
echo Committing...
git -C "%ROOT%" commit -m "chore: add Unity-generated .meta files and ProjectSettings" >>"%LOG%" 2>&1
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

>>"%LOG%" echo --- resulting commit ---
git -C "%ROOT%" log -1 --pretty=fuller >>"%LOG%" 2>&1
git -C "%ROOT%" log -1 --pretty=fuller

echo.
if "%RC%"=="0" (echo [OK] Committed and pushed.) else (echo [FAIL] Push failed - see %LOG%)
echo.
echo Tell Claude that step 2 finished.
echo.
pause
endlocal
