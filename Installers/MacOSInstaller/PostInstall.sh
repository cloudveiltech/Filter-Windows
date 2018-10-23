#!/bin/sh

if [ ! -d /usr/local/share/cloudveil ]; then
	mkdir /usr/local/share/cloudveil
fi;

launchctl load -w /Library/LaunchDaemons/org.cloudveil.filterserviceprovider.plist

# The filterserviceprovider takes care of all protective measures.
launchctl start org.cloudveil.filterserviceprovider