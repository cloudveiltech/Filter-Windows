#!/bin/sh

if [ -ne /usr/local/share/cloudveil ]; then
	mkdir /usr/local/share/cloudveil
fi;

launchctl load /Library/LaunchDaemons/org.cloudveil.filterserviceprovider.plist

# The filterserviceprovider takes care of all protective measures.
launchctl start org.cloudveil.filterserviceprovider