@echo off
rem ---------------------------------------------------------------------------
rem  Shared helper: locates the project root, the log folder and the Unity editor.
rem  Sets: ROOT, LOGDIR, UNITY
rem
rem  Preference order (first hit wins):
rem    1. %UNITY_EXE% if you set it manually
rem    2. 6000.3.21f1   <- chosen for this project (Unity 6.3 LTS)
rem    3. 6000.0.23f1   <- the version in ProjectVersion.txt, if ever installed
rem    4. any other 6000.3.x, then any 6000.0.x
rem
rem  Override with:  set UNITY_EXE=C:\path\to\Unity.exe
rem ---------------------------------------------------------------------------

pushd "%~dp0.." >nul
set "ROOT=%CD%"
popd >nul

set "LOGDIR=%ROOT%\tools\logs"
if not exist "%LOGDIR%" mkdir "%LOGDIR%"

set HUBROOTS="C:\Program Files\Unity\Hub\Editor" "C:\Program Files (x86)\Unity\Hub\Editor" "D:\Program Files\Unity\Hub\Editor" "D:\Unity\Hub\Editor" "E:\Unity\Hub\Editor"

set "UNITY="
if defined UNITY_EXE if exist "%UNITY_EXE%" set "UNITY=%UNITY_EXE%"

rem --- 2 & 3: exact preferred versions -------------------------------------
for %%V in (6000.3.21f1 6000.0.23f1) do (
  for %%R in (%HUBROOTS%) do (
    if not defined UNITY if exist "%%~R\%%V\Editor\Unity.exe" set "UNITY=%%~R\%%V\Editor\Unity.exe"
  )
)

rem --- 4: newest matching family ------------------------------------------
for %%F in (6000.3. 6000.0.) do (
  for %%R in (%HUBROOTS%) do (
    if exist "%%~R" (
      for /f "delims=" %%D in ('dir /b /ad /o-n "%%~R\%%F*" 2^>nul') do (
        if not defined UNITY if exist "%%~R\%%D\Editor\Unity.exe" set "UNITY=%%~R\%%D\Editor\Unity.exe"
      )
    )
  )
)

exit /b 0
