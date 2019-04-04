@echo off

If "%~1" == "" ( set /p VER="Enter new project version: " ) else ( set VER=%1 )

If "%VER%" == "" ( exit )

set VERSIONSTR=-version %VER%

powershell -ExecutionPolicy Unrestricted -Command .\update-project-versions.ps1 %VERSIONSTR%
