@echo off

IF EXIST "CitadelGUI\bin\Debug x64\" (
	echo > "CitadelGUI\bin\Debug x64\debug-filterserviceprovider"
) ELSE (
	echo "Please build CitadelGUI x64 (Debug) and try again."
)