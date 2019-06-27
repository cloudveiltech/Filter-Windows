@echo off

If "%1" == "" ( set /p Version=Enter new installer version: ) else ( set Version=%1 )

update-project-versions.exe -Version %Version%
call update-installer-version.cmd %Version%

call build-installer.cmd
