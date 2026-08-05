@echo off
setlocal
call "%~dp0_find_unity.bat"

set "OUT=%LOGDIR%\diag.txt"
> "%OUT%" echo === DIAG %DATE% %TIME% ===

if exist "%ROOT%\.git\index.lock" del /q /f "%ROOT%\.git\index.lock"

>>"%OUT%" echo --- core.autocrlf (with origin) ---
git -C "%ROOT%" config --show-origin --get-all core.autocrlf >>"%OUT%" 2>&1
>>"%OUT%" echo --- core.eol ---
git -C "%ROOT%" config --show-origin --get-all core.eol >>"%OUT%" 2>&1

>>"%OUT%" echo --- status as Windows git sees it ---
git -C "%ROOT%" status --short >>"%OUT%" 2>&1
>>"%OUT%" echo --- END STATUS ---

>>"%OUT%" echo --- diff stat ---
git -C "%ROOT%" diff --stat >>"%OUT%" 2>&1
>>"%OUT%" echo --- END ---

>>"%OUT%" echo --- branches ---
git -C "%ROOT%" branch -v >>"%OUT%" 2>&1

> "%LOGDIR%\DIAG_DONE.txt" echo done %TIME%
endlocal
exit /b 0
