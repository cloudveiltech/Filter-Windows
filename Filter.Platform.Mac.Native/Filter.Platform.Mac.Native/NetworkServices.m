// Thanks to https://00f.net/2011/08/14/programmatically-changing-network-configuration-on-osx/

#import "NetworkServices.h"

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

static void setProxyConfig(NSMutableDictionary* newConfig, const char* hostname, int httpPort, int httpsPort) {
    NSString* nsHostname = [NSString stringWithUTF8String:hostname];
    [newConfig setValue:nsHostname forKey:(NSString*)kSCPropNetProxiesHTTPProxy];
    [newConfig setValue:[NSNumber numberWithInt:httpPort] forKey:(NSString*)kSCPropNetProxiesHTTPPort];
    [newConfig setValue:[NSNumber numberWithInt:1] forKey:(NSString*)kSCPropNetProxiesHTTPEnable];
    
    [newConfig setValue:nsHostname forKey:(NSString*)kSCPropNetProxiesHTTPSProxy];
    [newConfig setValue:[NSNumber numberWithInt:httpsPort] forKey:(NSString*)kSCPropNetProxiesHTTPSPort];
    [newConfig setValue:[NSNumber numberWithInt:1] forKey:(NSString*)kSCPropNetProxiesHTTPSEnable];
}

bool setProxiesForNetworkService(SCNetworkServiceRef service, const char* hostname, int httpPort, int httpsPort) {
    SCNetworkProtocolRef protocol = nil;
    bool result = false;
    
    NSDictionary* config = nil;
    NSMutableDictionary* newConfig = nil;
    
    protocol = SCNetworkServiceCopyProtocol(service, kSCNetworkProtocolTypeProxies);
    if(protocol == nil || !SCNetworkProtocolGetEnabled(protocol)) {
        if(hostname == nil) {
            result = true;
            goto cleanup;
        } else {
            result = false;
            goto cleanup;
        }
    }

    config = (__bridge NSDictionary*) SCNetworkProtocolGetConfiguration(protocol);
    newConfig = nil;
    
    if(config != nil) {
        if(hostname == nil) {
            SCNetworkProtocolSetConfiguration(protocol, nil);
            result = true;
            goto cleanup;
        }
        
        newConfig = [NSMutableDictionary dictionaryWithDictionary: config];
        setProxyConfig(newConfig, hostname, httpPort, httpsPort);
        
    } else {
        if(hostname == nil) {
            result = true;
            goto cleanup;
        }
        
        newConfig = [NSMutableDictionary dictionaryWithCapacity:6];
        setProxyConfig(newConfig, hostname, httpPort, httpsPort);
    }
    
    result = SCNetworkProtocolSetConfiguration(protocol, (__bridge CFDictionaryRef) newConfig);
    
cleanup:
    if(protocol != nil) {
        CFRelease(protocol);
    }
    
    return result;
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

typedef struct ProxiesData {
    const char* hostname;
    int httpPort;
    int httpsPort;
} ProxiesData;

static bool setProxiesCallback(SCNetworkServiceRef service, void* cbData) {
    ProxiesData* data = (ProxiesData*)cbData;
    
    return setProxiesForNetworkService(service, data->hostname, data->httpPort, data->httpsPort);
}

bool SetProxy(const char* hostname, int httpPort, int httpsPort) {
    ProxiesData* data = CFAllocatorAllocate(nil, sizeof(ProxiesData), 0);
    
    data->hostname = hostname;
    data->httpPort = httpPort;
    data->httpsPort = httpsPort;
    
    bool ret = applyToNetworkServices(setProxiesCallback, true, data);
    
    CFAllocatorDeallocate(nil, data);
    return ret;
}


bool IsInternetReachable(const char* hostname) {
    SCNetworkReachabilityRef reachability = SCNetworkReachabilityCreateWithName(nil, hostname);
    
    if(reachability == nil) {
        return false;
    }
    
    SCNetworkReachabilityFlags flags;
    if(!SCNetworkReachabilityGetFlags(reachability, &flags)) {
        CFRelease(reachability);
        return false;
    }
    
    bool isReachable = flags & kSCNetworkReachabilityFlagsReachable;
    
    CFRelease(reachability);
    return isReachable;
}

SecCertificateRef CreateCertificateFromData(void* data, int length) {
    CFDataRef certData = CFDataCreate(nil, data, length);
    
    SecCertificateRef cert = SecCertificateCreateWithData(nil, certData);
    
    CFRelease(certData);
    return cert;
}

SecCertificateRef GetFromKeychain(const char* label) {
    NSDictionary* getquery = @{ (id)kSecClass: (id)kSecClassCertificate,
                                (id)kSecAttrLabel: [NSString stringWithUTF8String:label],
                                (id)kSecReturnRef: @YES,
                                };
    
    SecCertificateRef certificate = nil;
    
    OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)getquery, (CFTypeRef*)&certificate);
    
    if(status != errSecSuccess) {
        return nil;
    } else {
        return certificate;
    }
}

SecCertificateRef AddToKeychain(void* data, int length, const char* label) {
    SecCertificateRef cert = CreateCertificateFromData(data, length);
    
    NSDictionary* addquery = @{ (id)kSecValueRef: (__bridge id)cert,
                                (id)kSecClass: (id)kSecClassCertificate,
                                (id)kSecAttrLabel: [NSString stringWithUTF8String:label]
                                };
    
    OSStatus status = SecItemAdd((__bridge CFDictionaryRef)addquery, nil);
    if(status != errSecSuccess) {
        if(cert) {
            CFRelease(cert);
        }
        
        return nil;
    } else {
        return cert;
    }
}

void EnsureCertificateTrust(SecCertificateRef cert) {
    SecTrustSettingsSetTrustSettings(cert, kSecTrustSettingsDomainAdmin, nil);
}

void* GetCertificateBytes(SecCertificateRef cert, int* length) {
    CFDataRef certData = SecCertificateCopyData(cert);
    
    CFIndex _len = CFDataGetLength(certData);
    
    void* buf = CFAllocatorAllocate(nil, _len, 0);
    CFDataGetBytes(certData, CFRangeMake(0, _len), buf);
    
    if(length != nil) {
        *length = (int)_len;
    }
    
    return buf;
}

void __CFRelease(CFTypeRef ptr) {
    CFRelease(ptr);
}
