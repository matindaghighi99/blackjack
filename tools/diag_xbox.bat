@echo off
setlocal
rem READ-ONLY diagnostic. Queries only; changes nothing.
call "%~dp0_find_unity.bat"

set "OUT=%LOGDIR%\diag_xbox.txt"
> "%OUT%" echo === XBOX DIAG %DATE% %TIME% ===

>>"%OUT%" echo.
>>"%OUT%" echo --- Xbox / GameBar processes running now ---
tasklist /fi "imagename eq XboxPcApp.exe"      >>"%OUT%" 2>&1
tasklist /fi "imagename eq GameBar.exe"        >>"%OUT%" 2>&1
tasklist /fi "imagename eq GameBarFTServer.exe">>"%OUT%" 2>&1
tasklist /fi "imagename eq XboxGameBarWidgets.exe" >>"%OUT%" 2>&1
tasklist /fi "imagename eq GameBarPresenceWriter.exe" >>"%OUT%" 2>&1
tasklist /fi "imagename eq XboxAppServices.exe" >>"%OUT%" 2>&1

>>"%OUT%" echo.
>>"%OUT%" echo --- all processes with 'xbox' or 'game' in the name ---
powershell -NoProfile -Command "Get-Process | Where-Object { $_.ProcessName -match 'xbox|gamebar|gaming' } | Select-Object ProcessName,Id,StartTime | Format-Table -AutoSize | Out-String -Width 200" >>"%OUT%" 2>&1

>>"%OUT%" echo.
>>"%OUT%" echo --- Game Bar / GameDVR settings (0 = off, 1 = on) ---
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR" >>"%OUT%" 2>&1
reg query "HKCU\System\GameConfigStore" /v GameDVR_Enabled >>"%OUT%" 2>&1
reg query "HKCU\Software\Microsoft\GameBar" >>"%OUT%" 2>&1

>>"%OUT%" echo.
>>"%OUT%" echo --- startup entries (Run keys) ---
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" >>"%OUT%" 2>&1
reg query "HKLM\Software\Microsoft\Windows\CurrentVersion\Run" >>"%OUT%" 2>&1

>>"%OUT%" echo.
>>"%OUT%" echo --- scheduled tasks mentioning Xbox ---
powershell -NoProfile -Command "Get-ScheduledTask | Where-Object { $_.TaskName -match 'xbox|game' -or $_.TaskPath -match 'xbox|game' } | Select-Object TaskName,TaskPath,State | Format-Table -AutoSize | Out-String -Width 200" >>"%OUT%" 2>&1

>>"%OUT%" echo.
>>"%OUT%" echo --- has Windows flagged Unity.exe as a game? ---
reg query "HKCU\System\GameConfigStore\Children" /s 2>nul | findstr /i "unity" >>"%OUT%" 2>&1
powershell -NoProfile -Command "$p='HKCU:\System\GameConfigStore\Children'; if (Test-Path $p) { Get-ChildItem $p | ForEach-Object { $v=Get-ItemProperty $_.PSPath; if ($v.MatchedExeFullPath -match 'Unity|blackjack') { $v.MatchedExeFullPath } } }" >>"%OUT%" 2>&1

> "%LOGDIR%\XBOXDIAG_DONE.txt" echo done %TIME%
endlocal
exit /b 0
