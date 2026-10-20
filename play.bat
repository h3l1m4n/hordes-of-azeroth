@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\start-server.ps1" -Play
if errorlevel 1 pause
