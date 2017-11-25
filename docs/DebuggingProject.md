# Debugging notes

If there is any way to make this process easier, go ahead and edit this to your heart's content.
It's just what I (KF) discovered while finding my way around the project.

First of all, when you clone the project, you're going to have to make sure that there's an x86 platform to compile to.
1. On the debug bar there should be something that looks like "Debug" "Any CPU" "Citadel GUI", click on "Any CPU" dropdown and click "Configuration Manager"
2. In configuration manager, we want to create an x86 solution platform. Go to "Active Solution platform" -> "<New>"
3. Select x86 under "Type or select the new platform" and make sure "Create new project platforms" is set
4. Hit "OK" and you should be good to go!

After you've got the x86 platform created, you can go ahead and follow these instructions.
1. Open VS2015 (or whatever version you have) in Administrator mode.
2. Build project if needed.
3. Right click CitadelGUI project then click "Open Folder in File Explorer"
4. Go to "bin\x86\Debug" and open CloudVeil.exe.
5. In VS2015, click Debug -> Attach to process
6. Find "CloudVeil.exe" and attach your debugger to it.
7. Open another VS2015 in administrator mode.
8. Open up Citadel-Windows project
9. Repeat step 6, except this time for "FilterServiceProvider.exe"
10. Go to CitadelGUI properties, then Build Events, and change Post-build event command line to `xcopy /Y /C "$(ProjectDir)..\CitadelService\$(OutDir)*.*" "$(TargetDir)"`
11. voila, you have a debug environment for FilterServiceProvider and CitadelGUI.

The reason we use x86 is because of a driver issue in x64. This causes any attempt of debugging the applications to fail.
