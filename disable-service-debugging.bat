@echo off

IF EXIST "CloudVeilGUI\CloudVeilGUI.WPF\bin\Debug\debug-filterserviceprovider" (
	del "CitadelGUI\bin\Debug x64\debug-filterserviceprovider"
)