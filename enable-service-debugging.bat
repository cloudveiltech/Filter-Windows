@echo off

IF EXIST "CitadelGUI\bin\Debug x64\" (
	touch "CitadelGUI\bin\Debug x64\debug-filterserviceprovider"
) ELSE (
	echo "Please build CitadelGUI x64 (Debug) and try again."
)