@echo off

If "%1" == "" ( set /p InstallerVersion=Enter new installer version: ) else ( set InstallerVersion=%1 )

echo Installer version is %InstallerVersion%

.\wix-verify-bin\wix-verify.exe set Installers\SetupPayloadArm64\Module.wxs wix.module.version %InstallerVersion%
.\wix-verify-bin\wix-verify.exe set Installers\SetupPayload64\Module.wxs wix.module.version %InstallerVersion%
.\wix-verify-bin\wix-verify.exe set Installers\SetupPayload86\Module.wxs wix.module.version %InstallerVersion%

.\wix-verify-bin\wix-verify.exe set Installers\SetupProjects\Product-Arm64.wxs Wix.Package.Version %InstallerVersion%
.\wix-verify-bin\wix-verify.exe set Installers\SetupProjects\Product-x86.wxs Wix.Package.Version %InstallerVersion%
.\wix-verify-bin\wix-verify.exe set Installers\SetupProjects\Product-x64.wxs Wix.Package.Version %InstallerVersion%

REM .\wix-verify-bin\wix-verify.exe set Installers\SetupProjects\Product-Arm64.wxs Wix.Package.Upgrade.UpgradeVersion[1].Minimum %InstallerVersion%
REM .\wix-verify-bin\wix-verify.exe set Installers\SetupProjects\Product-Arm64.wxs Wix.Package.Upgrade.UpgradeVersion[2].Maximum %InstallerVersion%
REM .\wix-verify-bin\wix-verify.exe set Installers\SetupProjects\Product-x86.wxs Wix.Package.Upgrade.UpgradeVersion[1].Minimum %InstallerVersion%
REM .\wix-verify-bin\wix-verify.exe set Installers\SetupProjects\Product-x86.wxs Wix.Package.Upgrade.UpgradeVersion[2].Maximum %InstallerVersion%
REM .\wix-verify-bin\wix-verify.exe set Installers\SetupProjects\Product-x64.wxs Wix.Package.Upgrade.UpgradeVersion[1].Minimum %InstallerVersion%
REM .\wix-verify-bin\wix-verify.exe set Installers\SetupProjects\Product-x64.wxs Wix.Package.Upgrade.UpgradeVersion[2].Maximum %InstallerVersion%

.\wix-verify-bin\wix-verify.exe set CloudVeilInstaller\Bundle.wxs Wix.Bundle.Version %InstallerVersion%
