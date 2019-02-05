@echo off

IF EXIST "CloudVeilGUI\CloudVeilGUI.WPF\bin\Debug\" (
	touch "CitadelGUI\bin\Debug x64\debug-filterserviceprovider"
) ELSE (
	echo "Please build CloudVeilGUI.WPF x86 (Debug) and try again."
)