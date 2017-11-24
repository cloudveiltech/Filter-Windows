# Debug Flags

This document will cover different debug/feature flags that make my (KF) life as a developer a little bit easier.

## #define CAPTIVE_PORTAL_GUI_ENABLED
This define wraps the partially completed captive portal management GUI. We disabled this because all major internet browsers already take care of captive portal login for us.

Define in: command line arguments

## #define USE_LOCAL_UPDATE_XML
Define in: AppcastUpdater.cs or command line arguments

This tells FilterServiceProvider.cs to fetch its update information from `C:\ProgramData\CloudVeil\update.xml` instead of from the internet.