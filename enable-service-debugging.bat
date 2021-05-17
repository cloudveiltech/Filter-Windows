@echo off

IF EXIST "CloudVeilGUI\bin\Debug x64\" (
	echo > "CloudVeilGUI\bin\Debug x64\debug-filterserviceprovider"
) ELSE (
	echo "Please build CloudVeilGUI x64 (Debug) and try again."
)