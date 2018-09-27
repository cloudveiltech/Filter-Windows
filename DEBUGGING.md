# Filter Debugging

## Debug with x86, not x64
To debug the filter, build Citadel GUI x86. x64 debugging doesn't work thanks to Visual Studio Remote Debugging being broken on the filter.

In order to enable automatic debugging, I've added a couple of Debugger.Launch() statements to Citadel GUI and CitadelService projects. See below for instructions on how to enable debugging on both of those projects.

# Enabling automatic debugger launching

For CitadelGUI (CloudVeil.exe): Add an empty file named 'debug-cloudveil' (no extension) in the same directory as CloudVeil.exe

For CitadelService (FilterServiceProvider.exe): Add an empty file named 'debug-filterserviceprovider' (no extension) in the same directory as FilterServiceProvider.exe

# FilterServiceProvider.exe Service Control Manager errors? - Windows

When FilterServiceProvider throws an exception during initialization, the Service Control Manager (SCM) likes to spit this error onto the event log.
```
The FilterServiceProvider service failed to start due to the following error:
The service did not respond to the start or control request in a timely fashion.
```

I've added a little shim into `CitadelService\Program.cs` that allows us to initialize the filter provider from the command line to see if there are any initialization errors.

Just open `cmd` in administrator mode, and navigate to your CitadelService binary directory and run:
```
FilterServiceProvider.exe test-me
```

This should reveal any errors in initialization.