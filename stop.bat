@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\stop-server.ps1"
if errorlevel 1 pause
