#!/bin/sh

cd FilterServiceProvider.Mac
msbuild /t:Rebuild /p:Configuration=Release

cd ..
cd CloudVeilGUI/CloudVeilGUI.MacOS
msbuild /p:Configuration=Release

cd ../../Installers

packagesbuild --reference-folder MacOSInstaller MacOSInstaller.pkgproj

