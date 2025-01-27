#pragma once

#include "SeObjectType.h"

using namespace System;
using namespace System::Security::AccessControl;
using namespace System::Runtime::InteropServices;

namespace FilterNativeWindows {
    public ref class Security {
    public:
        static void SetObjectAsSystemOnly(String^ objectName, SeObjectType objectType);
        
        static bool GetServiceSecurity(String^ serviceName, [Out] RawSecurityDescriptor^% securityDescriptor);
        static bool SetServiceSecurity(String^ serviceName, RawSecurityDescriptor^ securityDescriptor);
    };
}
