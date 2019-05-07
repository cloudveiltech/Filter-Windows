#pragma once

#include <AccCtrl.h>

namespace FilterNativeWindows {
    public enum class SeObjectType {
        UnknownObject = SE_UNKNOWN_OBJECT_TYPE,
        FileObject = SE_FILE_OBJECT,
        Service = SE_SERVICE,
        Printer = SE_PRINTER,
        RegistryKey = SE_REGISTRY_KEY,
        LmShare = SE_LMSHARE,
        KernelObject = SE_KERNEL_OBJECT,
        WindowObject = SE_WINDOW_OBJECT,
        DsObject = SE_DS_OBJECT,
        DsObjectAll = SE_DS_OBJECT_ALL,
        ProviderDefinedObject = SE_PROVIDER_DEFINED_OBJECT,
        WmiGuidObject = SE_WMIGUID_OBJECT,
        RegistryWOW6432Key = SE_REGISTRY_WOW64_32KEY
    };
}