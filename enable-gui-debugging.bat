@echo off

IF EXIST "CloudVeilGUI\bin\Debug x86\" (
	touch "CloudVeilGUI\bin\Debug x86\debug-cloudveil"
) ELSE (
	echo "Please build CloudVeilGUI x86 (Debug) and try again."
)