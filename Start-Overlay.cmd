@echo off
cd /d "%~dp0"
if exist "%~dp0WtTacticalOverlay.exe" (
  start "" "%~dp0WtTacticalOverlay.exe"
) else (
  start "" "%~dp0bin\WtTacticalOverlay.exe"
)
