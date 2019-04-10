# Cross-platform code docs

This document sets out to explain the high-level structure of the Filter-Windows code and the plan for easier ports of the CV4W filtering engine to other platforms.

There are some flaws with the way I have structured it, and some places where the restructuring is not complete, but hopefully this document will help any future contributor understand the logic behind some of the project structures.

## FilterProvider.Common

This holds the core of the Citadel filtering engine. It manages high-level tasks like dispatching DNS server setting enforcement, and application updates. It also manages the configuration of the filter and downloads the lists which the filter uses to determine whether a site or URL should be blocked.

FilterProvider.Common is not the place for any cross-platform code. The cross-platform code goes in Filter.Platform.Mac, Citadel.Core.Windows, and CitadelService, among others. There will be more explanation on these further in the document.

Note that there is currently some Windows-specific code in FilterProvider.Common. This is because I have not yet had to move it out into CitadelService.

This contains a few of the interfaces described in the PlatformTypes section.

### CommonFilterServiceProvider (OnExtension)

CommonFilterServiceProvider provides an extension point for platform-specific services to initialize platform-specific features that FilterProvider.Common does not need knowledge of.

This is `OnExtension`. It is called after FilterProvider.Common is done with the initialization of common elements. Note that PlatformTypes registrations **should not be run** in `OnExtension`, but instead should be run before `CommonFilterServiceProvider` is instantiated by the platform-specific service.

```C#
delegate void OnExtension(CommonFilterServiceProvider provider);
```

## Filter.Platform.Common

This project holds the system which enables us to use platform-specific implementations where they are required. It also has logic which is shared among the GUIs and the services.

### PlatformTypes

Anywhere where there is a platform-specific implementation of something, `PlatformTypes` comes into play.

For example, consider DNS settings.

Because Windows, macOS, and Linux all have vastly different APIs for enforcing DNS settings, an interface is required to allow FilterProvider.Common to dispatch DNS enforcement commands. It is up to the platform-specific implementation (Filter.Platform.Mac or Citadel.Core.Windows) to register their specific implementations of `IDNSEnforcement` with `PlatformTypes.Register<T>(Func<object[], T> instantiateFn)`.

Paths are another good example of the need for platform-specific implementations. In Windows, the path for CV4W's data might be `C:\ProgramData\CloudVeil\config.json`, but in macOS, the path might be `/usr/local/share/cloudveil`. To overcome this, `IPathProvider` is used, and the platform-specific projects register their implementations of `IPathProvider` with `PlatformTypes`

PlatformTypes API:
```C#
	public T New<T>(params object[] args);
	public void Register<T>(Func<object[], T> instantiateFunc);
```

# Platform Projects

This section provides a general overview of the different platform projects and what needs to be implemented by them.

## Filter.Platform.Mac, Citadel.Core.Windows, (and maybe Filter.Platform.Linux)

These are chiefly responsible for implementing platform-specific code for the interfaces provided in Filter.Platform.Common

They should also provide their own `{namespace}.Platform` class with an `Init()` method to allow the platform's service wrapper to register their platform types.

## CitadelService, FilterServiceProvider.Mac, (and maybe FilterServiceProvider.Linux -- not implemented)

These projects provide platform-specific service wrappers for FilterProvider.Common. They should implement the interfaces found in `FilterProvider.Common.Platform`

These are the daemon/service processes that get run on the host OS.
