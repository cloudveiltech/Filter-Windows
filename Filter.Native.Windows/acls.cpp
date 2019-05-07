#include "acls.h"

HRESULT GetSid(LPCWSTR accountName, PSID* ppSid);

SC_HANDLE GetServiceHandleFromName(wchar_t* name) {
    SC_HANDLE ret = NULL;

    SC_HANDLE manager = OpenSCManagerW(NULL, NULL, SC_MANAGER_ALL_ACCESS);
    if (manager == NULL) {
        goto cleanup;
    }

    SC_HANDLE service = OpenServiceW(manager, name, ACCESS_SYSTEM_SECURITY | READ_CONTROL | WRITE_DAC);
    if (service == NULL) {
        goto cleanup;
    }
    else {
        ret = service;
    }

cleanup:
    if (manager != NULL) {
        CloseServiceHandle(manager);
    }

    return ret;
}

int SetObjectAsSystemOnly(LPWSTR objectName, SE_OBJECT_TYPE objectType) {
    PACL pDacl = NULL, pSacl = NULL;
    PSECURITY_DESCRIPTOR pSecurityDescriptor = NULL;
    PSID pSidOwner = NULL, pSidGroup = NULL;

    SID_IDENTIFIER_AUTHORITY SIDAuthNT = SECURITY_NT_AUTHORITY;

    int returnVal = 0;

    DWORD ret = 0;
    if ((ret = GetNamedSecurityInfoW(objectName, objectType, DACL_SECURITY_INFORMATION, NULL, NULL, &pDacl, NULL, NULL)) != ERROR_SUCCESS) {
        return ERROR_WAYPOINT_1;
    }

    PSID pSidSystem = NULL;
    HRESULT hr = GetSid(L"SYSTEM", &pSidSystem);
    if (FAILED(hr)) {
        return ERROR_WAYPOINT_2;
    }

    PACL pNewDacl = NULL;
    EXPLICIT_ACCESSW ea;

    ZeroMemory(&ea, sizeof(EXPLICIT_ACCESSW));
    ea.grfAccessPermissions = KEY_ALL_ACCESS;
    ea.grfAccessMode = SET_ACCESS;
    ea.grfInheritance = 
    ea.Trustee.TrusteeForm = TRUSTEE_IS_SID;
    ea.Trustee.TrusteeType = TRUSTEE_IS_USER;
    ea.Trustee.ptstrName = L"SYSTEM";
    
    DWORD dwRes = SetEntriesInAcl(1, &ea, NULL, &pNewDacl);
    if (dwRes != ERROR_SUCCESS) {
        returnVal = ERROR_WAYPOINT_3;
        goto cleanup;
    }

    dwRes = SetNamedSecurityInfoW(objectName, objectType, DACL_SECURITY_INFORMATION, NULL, NULL, pNewDacl, NULL);
    if (dwRes != ERROR_SUCCESS) {
        returnVal = ERROR_WAYPOINT_4;
        goto cleanup;
    }

cleanup:
    if (pSidSystem != NULL) {
        FreeSid(pSidSystem);
    }

    if (pNewDacl != NULL) {
        LocalFree((HLOCAL)pNewDacl);
    }

    return returnVal;
}

HRESULT GetSid(LPCWSTR accountName, PSID* ppSid) {
    if (accountName == NULL || ppSid == NULL) {
        return ERROR_INVALID_PARAMETER;
    }

    // Create buffers that may be large enough.  
    // If a buffer is too small, the count parameter will be set to the size needed.  
    const DWORD INITIAL_SIZE = 32;
    DWORD cbSid = 0;
    DWORD dwSidBufferSize = INITIAL_SIZE;
    DWORD cchDomainName = 0;
    DWORD dwDomainBufferSize = INITIAL_SIZE;
    WCHAR * domainName = NULL;
    SID_NAME_USE eSidType;
    DWORD dwErrorCode = 0;
    HRESULT hr = ERROR_SUCCESS;

    *ppSid = (PSID) new BYTE[dwSidBufferSize];
    if (*ppSid == NULL) {
        return ACL_INSUFFICIENT_MEMORY;
    }
    memset(*ppSid, 0, dwSidBufferSize);

    domainName = new WCHAR[dwDomainBufferSize];
    if (domainName == NULL) {
        return ACL_INSUFFICIENT_MEMORY;
    }
    memset(domainName, 0, dwDomainBufferSize * sizeof(WCHAR));

    for (;;) {
        cbSid = dwSidBufferSize;
        cchDomainName = dwDomainBufferSize;
        if (LookupAccountNameW(NULL, accountName, *ppSid, &cbSid, domainName, &cchDomainName, &eSidType)) {
            if(IsValidSid(*ppSid) == FALSE) {
                dwErrorCode = ACL_UNKNOWN_ERROR;
                break;
            }
        }

        dwErrorCode = GetLastError();

        if (dwErrorCode == ERROR_INSUFFICIENT_BUFFER) {
            if (cbSid > dwSidBufferSize) {
                FreeSid(*ppSid);
                *ppSid = (PSID) new BYTE[cbSid];
                if (*ppSid == NULL) {
                    return ACL_INSUFFICIENT_MEMORY;
                }

                memset(*ppSid, 0, cbSid);
                dwSidBufferSize = cbSid;
            }

            if (cchDomainName > dwDomainBufferSize) {
                delete[] domainName;
                domainName = new WCHAR[cchDomainName];
                if (domainName == NULL) {
                    return ACL_INSUFFICIENT_MEMORY;
                }
                memset(domainName, 0, cchDomainName * sizeof(WCHAR));
                dwDomainBufferSize = cchDomainName;
            }
        }
        else {
            hr = HRESULT_FROM_WIN32(dwErrorCode);
            break;
        }
    }

    delete[] domainName;
    return hr;

}
