#include <Windows.h>
#include <WtsApi32.h>

#include "Filter.Native.Windows.h"

unsigned int GetActiveSessionId() {
    PWTS_SESSION_INFOW pSessionInfo = NULL;
    DWORD activeSessionId = -1;
    DWORD sessionCount = 0;

    if (WTSEnumerateSessionsW(WTS_CURRENT_SERVER_HANDLE, 0, 1, &pSessionInfo, &sessionCount) != 0) {
        for (int i = 0; i < sessionCount; i++) {
            if (pSessionInfo[i].State == WTSActive) {
                activeSessionId = pSessionInfo[i].SessionId;
            }
        }
    }

    // If enumerating did not work, fall back to the old method.
    if (activeSessionId == -1) {
        activeSessionId = WTSGetActiveConsoleSessionId();
    }

    return activeSessionId;
}

static BOOL GetSessionUserToken(PHANDLE phUserToken) {
    HANDLE hImpersonationToken = NULL;
    PWTS_SESSION_INFOW pSessionInfo = NULL;
    BOOL bResult = false;

    DWORD activeSessionId = GetActiveSessionId();

    if (WTSQueryUserToken(activeSessionId, &hImpersonationToken) != 0) {
        bResult = DuplicateTokenEx(hImpersonationToken, 0, NULL, SecurityImpersonation, TokenPrimary, phUserToken);

        CloseHandle(hImpersonationToken);
    }

    return bResult;
}

int CreateProcessInCurrentSession(wchar_t* filename, wchar_t* cmdline, int winlogonPid) {
    if (winlogonPid < 0) {
        return false;
    }

    HANDLE winlogon = OpenProcess(MAXIMUM_ALLOWED, false, winlogonPid);

    if (winlogon == NULL) {
        return false;
    }

    HANDLE tokenHandle = NULL, duplicatedToken = NULL;
    if (!OpenProcessToken(winlogon, TOKEN_DUPLICATE, &tokenHandle)) {
        CloseHandle(winlogon);
        return false;
    }

    // Now that we have access to winlogon's token, we can duplicate it and use that duplicate to start an elevated process in the current session.
    if (!DuplicateTokenEx(tokenHandle, MAXIMUM_ALLOWED, NULL, SecurityIdentification, TokenPrimary, &duplicatedToken)) {
        CloseHandle(winlogon);
        CloseHandle(tokenHandle);

        return false;
    }

    PROCESS_INFORMATION newProcessInfo;

    STARTUPINFO si;
    memset(&si, 0, sizeof(STARTUPINFO));
    si.cb = sizeof(STARTUPINFO);

    // Interactive window station parameter; this indicates that the process can display a GUI on the desktop.
    si.lpDesktop = L"winsta0\\default";

    BOOL result = CreateProcessAsUser(duplicatedToken, filename, cmdline, NULL, NULL, false,
        NORMAL_PRIORITY_CLASS | CREATE_NEW_CONSOLE, NULL, NULL, &si, &newProcessInfo);

    return result;
}