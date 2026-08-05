@echo off
setlocal enabledelayedexpansion
title Blackjack - Reword commit 65347f5

call "%~dp0_find_unity.bat"

set "GIT_AUTHOR_NAME=Matin Daghighi"
set "GIT_AUTHOR_EMAIL=matindaghighi99@gmail.com"
set "GIT_COMMITTER_NAME=Matin Daghighi"
set "GIT_COMMITTER_EMAIL=matindaghighi99@gmail.com"

set "TARGET=65347f5"
set "LOG=%LOGDIR%\git_reword.txt"
> "%LOG%" echo === REWORD %TARGET% %DATE% %TIME% ===

echo ===========================================================
echo  Reword commit %TARGET%
echo ===========================================================
echo.
echo  Old: chore: add Unity-generated meta files      (inaccurate)
echo  New: fix(iap): resolve UnityIapService compile errors...
echo.
echo  This rewrites 3 commits of ALREADY-PUSHED history and
echo  force-pushes (with lease). Safe because you are the sole
echo  author and nothing else has pulled.
echo.
set /p "OK=Type YES to continue: "
if /i not "%OK%"=="YES" (
  echo Aborted - nothing changed.
  pause
  exit /b 0
)

rem --- refuse to run on a dirty tree ---------------------------------------
git -C "%ROOT%" diff --quiet
if errorlevel 1 goto :dirty
git -C "%ROOT%" diff --cached --quiet
if errorlevel 1 goto :dirty

rem --- safety net ----------------------------------------------------------
git -C "%ROOT%" branch -f backup-before-reword main >>"%LOG%" 2>&1
echo Backup branch 'backup-before-reword' created.
echo.

echo [1/4] Checking out %TARGET% detached...
git -C "%ROOT%" checkout --detach %TARGET% >>"%LOG%" 2>&1
if errorlevel 1 goto :fail

echo [2/4] Amending the message (author + date preserved)...
git -C "%ROOT%" commit --amend --no-edit ^
  -m "fix(iap): resolve UnityIapService compile errors, add editor bootstrap" ^
  -m "Implement the Unity IAP listener callbacks explicitly so they stop colliding with the IPurchaseService events of the same name (CS0102), and add the missing UnityEngine.UI assembly reference so the UI screens compile." ^
  -m "Also adds the SceneBootstrap editor tool and the play-mode scene tests. Contains no .meta files despite the original message." >>"%LOG%" 2>&1
if errorlevel 1 goto :fail

echo [3/4] Replaying the two later commits...
git -C "%ROOT%" rebase --onto HEAD %TARGET% main >>"%LOG%" 2>&1
if errorlevel 1 goto :fail

echo [4/4] Force-pushing with lease...
git -C "%ROOT%" push --force-with-lease origin main >>"%LOG%" 2>&1
if errorlevel 1 goto :fail

echo.
echo [OK] History rewritten and pushed.
git -C "%ROOT%" log -3 --pretty=oneline
git -C "%ROOT%" log -3 --pretty=fuller >>"%LOG%" 2>&1
echo.
echo Rollback if needed:  git reset --hard backup-before-reword ^&^& git push --force origin main
echo Delete backup when happy:  git branch -D backup-before-reword
echo.
pause
exit /b 0

:dirty
echo.
echo [ABORT] Working tree has uncommitted changes. Commit or stash first.
git -C "%ROOT%" status --short
echo.
pause
exit /b 1

:fail
echo.
echo [FAIL] Something went wrong - see %LOG%
echo        Your work is safe on branch 'backup-before-reword'.
echo        Recover with:  git checkout main ^&^& git reset --hard backup-before-reword
type "%LOG%"
echo.
pause
exit /b 1
