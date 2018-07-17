# Filter Debugging

## Debug with x86, not x64
To debug the filter, build Citadel GUI x86. x64 debugging doesn't work thanks to Visual Studio Remote Debugging being broken on the filter.

In order to enable automatic debugging, I've added a couple of Debugger.Launch() statements to Citadel GUI and CitadelService projects. See below for instructions on how to enable debugging on both of those projects.

# Enabling automatic debugger launching

For CitadelGUI (CloudVeil.exe): Add an empty file named 'debug-cloudveil' (no extension) in the same directory as CloudVeil.exe

For CitadelService (FilterServiceProvider.exe): Add an empty file named 'debug-filterserviceprovider' (no extension) in the same directory as FilterServiceProvider.exe

