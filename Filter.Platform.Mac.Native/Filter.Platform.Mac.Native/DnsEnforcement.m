// Thanks to https://00f.net/2011/08/14/programmatically-changing-network-configuration-on-osx/

#import "DnsEnforcement.h"

#import <Foundation/Foundation.h>
#import <SystemConfiguration/SystemConfiguration.h>

// The two functions we need to implement are
// SetDnsForNic(string ifaceName, string primary, string secondary);
// SetDnsForNicToDhcp(string ifaceName);

bool touchDynamicStore(void);
bool setResolvers(SCNetworkServiceRef service, NSArray* resolvers);
bool networkServiceHasDnsSupport(SCNetworkServiceRef service);

bool networkServiceHasDnsSupport(SCNetworkServiceRef service) {
    if(!SCNetworkServiceGetEnabled(service)) {
        return false;
    }
    
    SCNetworkInterfaceRef interface = SCNetworkServiceGetInterface(service);
    if(interface == nil) {
        return false;
    }
    
    CFArrayRef supportedProtocols = SCNetworkInterfaceGetSupportedProtocolTypes(interface);
    CFIndex arrayCount = CFArrayGetCount(supportedProtocols);
    for(CFIndex i = 0; i < arrayCount; i++) {
        CFStringRef cfStr = (CFStringRef)CFArrayGetValueAtIndex(supportedProtocols, i);
        
        if(CFEqual(cfStr, kSCNetworkProtocolTypeDNS)) {
            return true;
        }
    }
    
    return false;
}

bool applyToNetworkServices(NetworkServiceFn cb, bool commit, void* cbData) {
    SCPreferencesRef preferences = SCPreferencesCreate(NULL, CFSTR("org.cloudveil.filterserviceprovider"), NULL);
    
    SCPreferencesLock(preferences, TRUE);
    SCNetworkSetRef networkSet = SCNetworkSetCopyCurrent(preferences);
    
    NSArray* networkSetServices = (__bridge_transfer NSArray*) SCNetworkSetCopyServices(networkSet);
    bool ret = true;
    
    for(id _networkService in networkSetServices) {
        SCNetworkServiceRef networkService = (__bridge SCNetworkServiceRef) _networkService;
        ret &= cb(networkService, cbData);
    }
    
    SCPreferencesUnlock(preferences);
    if(commit) {
        SCPreferencesCommitChanges(preferences);
        SCPreferencesApplyChanges(preferences);
    }
    
    CFRelease(networkSet);
    CFRelease(preferences);
    
    if(commit) {
        touchDynamicStore();
    }
    
    return ret;
}

bool touchDynamicStore() {
    SCDynamicStoreRef ds = SCDynamicStoreCreate(nil, CFSTR("org.cloudveil.filterserviceprovider"), nil, nil);
    CFRelease(ds);
    
    return true;
}

bool forceDhcpUpdate() {
    CFArrayRef interfaces = SCNetworkInterfaceCopyAll();
    
    CFIndex i, arrayCount;
    arrayCount = CFArrayGetCount(arrayCount);
    for(i = 0; i < arrayCount; i++) {
        SCNetworkInterfaceRef interface = (SCNetworkInterfaceRef)CFArrayGetValueAtIndex(interfaces, i);
        SCNetworkInterfaceForceConfigurationRefresh(interface);
    }
    
    return true;
}

bool setResolversForNetworkService(SCNetworkServiceRef service, NSArray* resolvers) {
    SCNetworkProtocolRef protocol = nil;
    bool result = false;
    NSDictionary* dnsDict = nil;
    NSMutableDictionary* newDnsDict = nil;
    
    if(networkServiceHasDnsSupport(service) == false) {
        result = false;
        goto cleanup;
    }
    
    if(![resolvers isKindOfClass:[NSArray class]] || resolvers.count == 0) {
        resolvers = nil;
    }
    
    protocol = SCNetworkServiceCopyProtocol(service, kSCNetworkProtocolTypeDNS);
    if(protocol == nil || !SCNetworkProtocolGetEnabled(protocol)) {
        if(resolvers == nil) {
            result = true;
            goto cleanup;
        } else {
            result = false;
            goto cleanup;
        }
    }
    
    dnsDict = (__bridge NSDictionary*) SCNetworkProtocolGetConfiguration(protocol);
    newDnsDict = nil;
    
    if(dnsDict != nil) {
        if(resolvers == nil) {
            SCNetworkProtocolSetConfiguration(protocol, nil);
            result = true;
            goto cleanup;
        }
        
        newDnsDict = [NSMutableDictionary dictionaryWithDictionary: dnsDict];
        if(resolvers != nil) {
            [newDnsDict setValue: resolvers forKey: (__bridge NSString*) kSCPropNetDNSServerAddresses];
        }
    } else {
        // This interface had no existing configuration.
        if(resolvers == nil) {
            result = true;
            goto cleanup;
        }
        
        newDnsDict = [NSMutableDictionary dictionaryWithObject: resolvers forKey: (__bridge NSString*) kSCPropNetDNSServerAddresses];
    }
    
    result = SCNetworkProtocolSetConfiguration(protocol, (__bridge CFDictionaryRef) newDnsDict);
    
cleanup:
    if(protocol != nil) {
        CFRelease(protocol);
    }
    
    return result;
}

static bool setResolversCallback(SCNetworkServiceRef service, void* cbData) {
    NSArray* resolvers = (__bridge NSArray*)cbData;
    
    return setResolversForNetworkService(service, resolvers);
}

bool EnforceDns(const char* primary, const char* secondary) {
    NSMutableArray* dnsServers = [NSMutableArray array];
    
    if(primary != nil) {
        [dnsServers addObject:[NSString stringWithUTF8String:primary]];
    }
    
    if(secondary != nil) {
        [dnsServers addObject:[NSString stringWithUTF8String:secondary]];
    }
    
    return applyToNetworkServices(setResolversCallback, true, (__bridge void*)dnsServers);
}
