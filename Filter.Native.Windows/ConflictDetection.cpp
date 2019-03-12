#include <Windows.h>
#include <Psapi.h>

#include "ConflictReason.h"

#define MAX_DEVICE_DRIVERS_ENUM 1024

int SearchConflictReason() {
    LPVOID* driverList = new LPVOID[MAX_DEVICE_DRIVERS_ENUM];
    DWORD driverListSize = sizeof(LPVOID) * MAX_DEVICE_DRIVERS_ENUM;
    DWORD recvDriverListSize = 0;

    BOOL ret = EnumDeviceDrivers(driverList, driverListSize, &recvDriverListSize);

    if (!ret) {
        return CONFLICT_REASON_FAILED;
    }

    if (recvDriverListSize > driverListSize) {
        delete driverList;
        driverList = (LPVOID*)new unsigned char[recvDriverListSize];
        driverListSize = recvDriverListSize;

        ret = EnumDeviceDrivers(driverList, driverListSize, &recvDriverListSize);

        if (!ret) {
            return CONFLICT_REASON_FAILED;
        }
    }

    // Now that we got our list of device drivers, enumerate them to find the conflicting drivers.

    delete driverList;
}

int FindWFPDriverConflicts() {
    return 0; // TODO: Build this yet.
}