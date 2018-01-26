#include "stdafx.h"

#include <Windows.h>
#include <Psapi.h>
#include <tchar.h>
#include <stdio.h>
#include <vector>

using namespace std;

#define ARRAY_SIZE 1024

typedef struct DriverListEntry
{
    const char* MsiPropertyName;
    vector<TCHAR*> MatchingDriverNames;
} DriverListEntry;

typedef struct ProgramListEntry
{
    const char* MsiPropertyName;
    vector<TCHAR*> MatchingServiceNames;
} ProgramListEntry;

int SetMsiPropertiesFromDrivers(MSIHANDLE installerHandle);
int SetMsiPropertiesFromPrograms(MSIHANDLE installerHandle);

UINT __stdcall DetectInstalledConflicts(
    MSIHANDLE hInstall
)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    hr = WcaInitialize(hInstall, "CustomAction1");
    ExitOnFailure(hr, "Failed to initialize");

    WcaLog(LOGMSG_STANDARD, "Initialized.");

    // Enumerate device drivers.
    LPVOID *drivers = NULL;
    drivers = (LPVOID*)LocalAlloc(0, sizeof(LPVOID) * ARRAY_SIZE);

    DWORD bytesNeeded;
    DWORD bytesTmp = 0;
    bool failed = false;

    int driverCount;

    if (drivers != NULL && EnumDeviceDrivers(drivers, sizeof(LPVOID) * ARRAY_SIZE, &bytesNeeded) && bytesNeeded <= sizeof(LPVOID) * ARRAY_SIZE)
    {
        driverCount = bytesNeeded / sizeof(LPVOID);
    }
    else
    {
        if (bytesNeeded > sizeof(drivers))
        {
            LocalFree(drivers);
            drivers = (LPVOID*)LocalAlloc(0, bytesNeeded);
            bytesTmp = bytesNeeded;
            bytesNeeded = 0;

            if (drivers != NULL && EnumDeviceDrivers(drivers, bytesTmp, &bytesNeeded) && bytesNeeded <= bytesTmp)
            {
                driverCount = bytesNeeded / sizeof(LPVOID);
            }
        }
        else
        {
            failed = true;
        }
    }

    if (!failed && drivers != NULL)
    {
        // Process device drivers here.
        TCHAR szDriver[ARRAY_SIZE];

        for (int i = 0; i < driverCount; i++)
        {

        }
    }

LExit:
	er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
	return WcaFinalize(er);
}


// DllMain - Initialize and cleanup WiX custom action utils.
extern "C" BOOL WINAPI DllMain(
	__in HINSTANCE hInst,
	__in ULONG ulReason,
	__in LPVOID
	)
{
	switch(ulReason)
	{
	case DLL_PROCESS_ATTACH:
		WcaGlobalInitialize(hInst);
		break;

	case DLL_PROCESS_DETACH:
		WcaGlobalFinalize();
		break;
	}

	return TRUE;
}


#define ARRAY_SIZE 1024

vector<DriverListEntry*> badDrivers;
vector<ProgramListEntry*> badPrograms;

void init()
{
    DriverListEntry* tmp = new DriverListEntry;

    tmp->MsiPropertyName = "IS_CV4W_INSTALLED";
    tmp->MatchingDriverNames.push_back(L"WinDivert.sys");
    tmp->MatchingDriverNames.push_back(L"WinDivert64.sys");

    badDrivers.push_back(tmp);

    tmp = new DriverListEntry;
    tmp->MsiPropertyName = "IS_CLEANINTERNET_INSTALLED";
    tmp->MatchingDriverNames.push_back(L"wacdrvnt.sys");
    tmp->MatchingDriverNames.push_back(L"wacdrvnt64.sys");

    ProgramListEntry* program = new ProgramListEntry;
    program->MsiPropertyName = "IS_BLUECOAT_THREATPULSE_INSTALLED";
    program->MatchingServiceNames.push_back(L"bcua-notifier.exe");
    program->MatchingServiceNames.push_back(L"bcua-service.exe");

    badPrograms.push_back(program);
}

int SetMsiPropertiesFromDrivers(MSIHANDLE installHandle)
{
    // Enumerate device drivers.
    LPVOID *drivers = NULL;
    drivers = (LPVOID*)LocalAlloc(0, sizeof(LPVOID) * ARRAY_SIZE);

    DWORD bytesNeeded;
    DWORD bytesTmp = 0;
    bool failed = false;

    int driverCount;

    if (drivers != NULL && EnumDeviceDrivers(drivers, sizeof(LPVOID) * ARRAY_SIZE, &bytesNeeded) && bytesNeeded <= sizeof(LPVOID) * ARRAY_SIZE)
    {
        driverCount = bytesNeeded / sizeof(LPVOID);
    }
    else
    {
        if (bytesNeeded > sizeof(drivers))
        {
            LocalFree(drivers);
            drivers = (LPVOID*)LocalAlloc(0, bytesNeeded);
            bytesTmp = bytesNeeded;
            bytesNeeded = 0;

            if (drivers != NULL && EnumDeviceDrivers(drivers, bytesTmp, &bytesNeeded) && bytesNeeded <= bytesTmp)
            {
                driverCount = bytesNeeded / sizeof(LPVOID);
            }
        }
        else
        {
            failed = true;
        }
    }

    if (!failed && drivers != NULL)
    {
        // Process device drivers here.
        TCHAR szDriver[ARRAY_SIZE];

        for (int i = 0; i < driverCount; i++)
        {
            if (GetDeviceDriverBaseName(drivers[i], szDriver, sizeof(szDriver) / sizeof(szDriver[0])))
            {
                for (vector<DriverListEntry*>::iterator itr = badDrivers.begin(); itr != badDrivers.end(); itr++)
                {
                    for (vector<TCHAR*>::iterator nameItr = (*itr)->MatchingDriverNames.begin(); nameItr != (*itr)->MatchingDriverNames.end(); nameItr++)
                    {
                        if (!lstrcmpiW(*nameItr, szDriver))
                        {
                            MsiSetPropertyA(installHandle, (*itr)->MsiPropertyName, "1");
                        }
                    }
                }
            }
        }

        return 0;
    }
    else
    {
        return 1;
    }
}

TCHAR* GetProcessName(DWORD processId)
{
    TCHAR* processName = NULL;

    HANDLE processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, processId);

    if (processHandle != NULL)
    {
        HMODULE module;
        DWORD bytesNeeded;

        if (EnumProcessModules(processHandle, &module, sizeof(HMODULE), &bytesNeeded))
        {
            processName = new TCHAR[MAX_PATH];
            GetModuleBaseName(processHandle, module, processName, MAX_PATH);
        }
    }

    if (processName == NULL && processHandle == NULL)
    {
        return NULL;
    }

    CloseHandle(processHandle);
    return processName;
}

int SetMsiPropertiesFromPrograms(MSIHANDLE installHandle)
{
    // Enumerate device drivers.
    DWORD *processIds = NULL;
    processIds = (DWORD*)LocalAlloc(0, sizeof(DWORD) * ARRAY_SIZE);

    DWORD bytesNeeded;
    DWORD bytesTmp = 0;
    bool failed = false;

    int processCount;

    if (processIds != NULL && EnumProcesses(processIds, sizeof(DWORD) * ARRAY_SIZE, &bytesNeeded) && bytesNeeded <= sizeof(DWORD) * ARRAY_SIZE)
    {
        processCount = bytesNeeded / sizeof(DWORD);
    }
    else
    {
        if (bytesNeeded > sizeof(processIds))
        {
            LocalFree(processIds);
            processIds = (DWORD*)LocalAlloc(0, bytesNeeded);
            bytesTmp = bytesNeeded;
            bytesNeeded = 0;

            if (processIds != NULL && EnumProcesses(processIds, bytesTmp, &bytesNeeded) && bytesNeeded <= bytesTmp)
            {
                processCount = bytesNeeded / sizeof(LPVOID);
            }
        }
        else
        {
            failed = true;
        }
    }

    if (!failed && processIds != NULL)
    {
        // Process device drivers here.
        TCHAR szModuleBaseName[MAX_PATH];

        for (int i = 0; i < processCount; i++)
        {
            TCHAR* processName = GetProcessName(processIds[i]);
            if (processName != NULL)
            {
                for (vector<ProgramListEntry*>::iterator itr = badPrograms.begin(); itr != badPrograms.end(); itr++)
                {
                    for (vector<TCHAR*>::iterator nameItr = (*itr)->MatchingServiceNames.begin(); nameItr != (*itr)->MatchingServiceNames.end(); nameItr++)
                    {
                        if (!lstrcmpiW(*nameItr, processName))
                        {
                            MsiSetPropertyA(installHandle, (*itr)->MsiPropertyName, "1");
                        }
                    }
                }

                delete processName;
            }
        }

        return 0;
    }
    else
    {
        return 1;
    }
}