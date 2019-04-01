@echo off

If "%1" == "" ( set /p Version=Enter new installer version: ) else ( set Version=%1 )

call update-project-versions.cmd %Version%
call update-installer-version.cmd %Version%

call build-installer.cmd
