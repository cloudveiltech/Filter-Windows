#!/bin/sh

if [ -f /Library/LaunchDaemons/org.cloudveil.filterserviceprovider.plist ]; then
	launchctl unload /Library/LaunchDaemons/org.cloudveil.filterserviceprovider.plist
fi;

if [ -d /usr/local/libexec/FilterServiceProvider.Mac.app ]; then
	rm -r /usr/local/libexec/FilterServiceProvider.Mac.app
fi;

if [ -d /Applications/CloudVeilGUI.MacOS.app ]; then
	rm -r /Applications/CloudVeilGUI.MacOS.app
fi;

exit 0
