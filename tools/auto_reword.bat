@echo off
setlocal enabledelayedexpansion
title Blackjack - reword 65347f5 (unattended)

call "%~dp0_find_unity.bat"

set "GIT_COMMITTER_NAME=Matin Daghighi"
set "GIT_COMMITTER_EMAIL=matindaghighi99@gmail.com"

set "TARGET=65347f5"
set "DONE=%LOGDIR%\REWORD_DONE.txt"
set "LOG=%LOGDIR%\git_reword.txt"
if exist "%DONE%" del /q "%DONE%"
> "%LOG%" echo === REWORD %TARGET% %DATE% %TIME% ===

rem A stale lock silently blocks every git write - clear it first.
if exist "%ROOT%\.git\index.lock" del /q /f "%ROOT%\.git\index.lock"

rem --- refuse to run on a dirty tree ---------------------------------------
git -C "%ROOT%" diff --quiet
if errorlevel 1 goto :dirty
git -C "%ROOT%" diff --cached --quiet
if errorlevel 1 goto :dirty

rem --- safety net ----------------------------------------------------------
git -C "%ROOT%" branch -f backup-before-reword main >>"%LOG%" 2>&1

rem --- preserve the original author date ------------------------------------
for /f "delims=" %%D in ('git -C "%ROOT%" log -1 --format^=%%aI %TARGET%') do set "ADATE=%%D"
>>"%LOG%" echo Original author date: !ADATE!

>>"%LOG%" echo --- checkout detached ---
git -C "%ROOT%" checkout --detach %TARGET% >>"%LOG%" 2>&1
if errorlevel 1 goto :fail

>>"%LOG%" echo --- amend message ---
git -C "%ROOT%" commit --amend --date="!ADATE!" ^
  -m "fix(iap): resolve UnityIapService compile errors, add editor bootstrap" ^
  -m "Implement the Unity IAP listener callbacks explicitly so they stop colliding with the IPurchaseService events of the same name (CS0102), and add the missing UnityEngine.UI assembly reference so the UI screens compile." ^
  -m "Also adds the SceneBootstrap editor tool and the play-mode scene tests. Contains no .meta files, despite what the original commit message claimed." >>"%LOG%" 2>&1
if errorlevel 1 goto :fail

rem Commit a4c45bf ADDS blackjack-social-casino.slnx, and Unity has since regenerated
rem it as an untracked file - so the replay would overwrite it and git refuses.
rem It is a generated VS solution (now gitignored); Unity recreates it on next open.
if exist "%ROOT%\blackjack-social-casino.slnx" (
  >>"%LOG%" echo Removing regenerated untracked blackjack-social-casino.slnx
  del /q /f "%ROOT%\blackjack-social-casino.slnx"
)

>>"%LOG%" echo --- replay later commits ---
git -C "%ROOT%" rebase --onto HEAD %TARGET% main >>"%LOG%" 2>&1
if errorlevel 1 goto :fail

>>"%LOG%" echo --- force-push with lease ---
git -C "%ROOT%" push --force-with-lease origin main >>"%LOG%" 2>&1
if errorlevel 1 goto :fail

git -C "%ROOT%" log -4 --pretty=fuller >>"%LOG%" 2>&1

> "%DONE%" echo STATUS=REWRITTEN
>>"%DONE%" echo FINISHED=%DATE% %TIME%
endlocal
exit /b 0

:dirty
>>"%LOG%" echo ABORT - working tree dirty
git -C "%ROOT%" status --short >>"%LOG%" 2>&1
> "%DONE%" echo STATUS=ABORTED_DIRTY_TREE
endlocal
exit /b 1

:fail
>>"%LOG%" echo FAILED - see above
git -C "%ROOT%" rebase --abort >>"%LOG%" 2>&1
git -C "%ROOT%" checkout main >>"%LOG%" 2>&1
> "%DONE%" echo STATUS=FAILED
>>"%DONE%" echo RECOVER=git reset --hard backup-before-reword
endlocal
exit /b 1
