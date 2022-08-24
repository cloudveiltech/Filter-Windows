#include "acls.h"
#include "Security.h"

#include <vcclr.h>

namespace FilterNativeWindows {
    wchar_t* getCStringFromString(String^ str) {
        pin_ptr<const wchar_t> pinnedStr = PtrToStringChars(str);
        wchar_t* c_str = new wchar_t[wcslen(pinnedStr) + 1];
        wcscpy(c_str, pinnedStr);

        return c_str;
    }

    void Security::SetObjectAsSystemOnly(String^ objectName, SeObjectType objectType) {
        if (objectName == nullptr) {
            throw gcnew ArgumentNullException("objectName");
        }

        pin_ptr<const wchar_t> c_objectName = PtrToStringChars(objectName);
        wchar_t* mutable_objectName = new wchar_t[wcslen(c_objectName) + 1];
        wcscpy(mutable_objectName, c_objectName);

        int ret = ::SetObjectAsSystemOnly(mutable_objectName, (SE_OBJECT_TYPE)objectType);
        delete mutable_objectName;
    }

    bool Security::SetServiceSecurity(String^ serviceName, RawSecurityDescriptor^ securityDescriptor) {
        if (serviceName == nullptr) {
            throw gcnew ArgumentNullException("serviceName");
        }

        wchar_t* name = NULL;
        SC_HANDLE service = NULL;
        SECURITY_DESCRIPTOR* descriptor = NULL;

        try {
            name = getCStringFromString(serviceName);

            array<unsigned char>^ binaryForm = gcnew array<unsigned char>(securityDescriptor->BinaryLength);
            securityDescriptor->GetBinaryForm(binaryForm, 0);
            Marshal::Copy(binaryForm, 0, IntPtr::IntPtr(descriptor), securityDescriptor->BinaryLength);

            service = GetServiceHandleFromName(name);
            if (service == NULL) {
                return false;
            }

            if (!SetServiceObjectSecurity(service, DACL_SECURITY_INFORMATION, descriptor)) {
                return false;
            }

            return true;
        }
        finally {
            if (name != NULL) {
                delete name;
            }

            if (service != NULL) {
                CloseServiceHandle(service);
            }

            if (descriptor != NULL) {
                delete descriptor;
            }
        }
    }

    bool Security::GetServiceSecurity(String^ serviceName, [Out] RawSecurityDescriptor^% securityDescriptor) {
        if (serviceName == nullptr) {
            throw gcnew ArgumentNullException("serviceName");
        }

        wchar_t* name = NULL;
        SC_HANDLE service = NULL;
        SECURITY_DESCRIPTOR* descriptor = NULL;

        try {
            name = getCStringFromString(serviceName);

            service = GetServiceHandleFromName(name);
            if (service == NULL) {
                throw gcnew NullReferenceException("service is null");
            }

            DWORD bufSize = 4096;
            descriptor = (SECURITY_DESCRIPTOR*)new unsigned char[bufSize];
            DWORD bytesNeeded = 0;

            while (true) {
                if (!QueryServiceObjectSecurity(service, DACL_SECURITY_INFORMATION, descriptor, bufSize, &bytesNeeded)) {
                    if (bytesNeeded > bufSize) {
                        delete descriptor;

                        bufSize = bytesNeeded;
                        descriptor = (SECURITY_DESCRIPTOR*)new unsigned char[bufSize];
                    }
                    else {
                        return false;
                    }
                }
                else {

                    break;
                }
            }

            array<unsigned char>^ arr = gcnew array<unsigned char>(bytesNeeded);
            Marshal::Copy(IntPtr::IntPtr(descriptor), arr, 0, (int)bytesNeeded);

            securityDescriptor = gcnew RawSecurityDescriptor(arr, 0);
            return true;
        }
        finally {
            if (name != NULL) {
                delete name;
            }

            if (service != NULL) {
                CloseServiceHandle(service);
            }

            if (descriptor != NULL) {
                delete descriptor;
            }
        }
    }

}