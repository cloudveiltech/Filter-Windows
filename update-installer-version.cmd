@echo off
set /p InstallerVersion=Enter a version to update the installers:

.\wix-verify-bin\wix-verify.exe set Installers\SetupPayload64\Module.wxs wix.module.version %InstallerVersion%
.\wix-verify-bin\wix-verify.exe set Installers\SetupPayload86\Module.wxs wix.module.version %InstallerVersion%
.\wix-verify-bin\wix-verify.exe set Installers\SetupProjects\Product-x86.wxs wix.product.version %InstallerVersion%
.\wix-verify-bin\wix-verify.exe set Installers\SetupProjects\Product-x64.wxs wix.product.version %InstallerVersion%
.\wix-verify-bin\wix-verify.exe set CloudVeilInstaller\Bundle.wxs Wix.Bundle.Version %InstallerVersion%
