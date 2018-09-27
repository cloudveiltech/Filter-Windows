@echo off

IF EXIST "CloudVeilGUI\CloudVeilGUI.WPF\bin\Debug\" (
	touch "CloudVeilGUI\CloudVeilGUI.WPF\bin\Debug\debug-filterserviceprovider"
) ELSE (
	echo "Please build CloudVeilGUI.WPF x86 (Debug) and try again."
)