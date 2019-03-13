#include <Windows.h>
#include <Psapi.h>

#include "ConflictReason.h"

#define MAX_DEVICE_DRIVERS_ENUM 1024
#define ARRAY_SIZE 1024

typedef struct DriverConflict {
    const wchar_t* name;
    int conflict;
} DriverConflict;

static const wchar_t* avastDrivers[] = {
    TEXT("aswVmm.sys"),
    TEXT("aswSP.sys"),
    TEXT("aswbidsdriver.sys"),
    TEXT("aswbidsh.sys"),
    TEXT("aswblog.sys"),
    TEXT("aswbuniv.sys"),
    TEXT("aswSnx.sys"),
    TEXT("aswArPot.sys"),
    TEXT("aswHdsKe.sys"),
    TEXT("aswKbd.sys"),
    TEXT("aswRdr2.sys"),
    TEXT("aswMonFlt.sys"),
    TEXT("aswStm.sys")
};

static int avastDriversLength = sizeof(avastDrivers) / sizeof(avastDrivers[0]);

static const wchar_t* mcafeeDrivers[] = {
    TEXT("mfeaack.sys"),
    TEXT("mfeplk.sys"),
    TEXT("mfeavfk.sys"),
    TEXT("mfefirek.sys"),
    TEXT("mfencbdc.sys")
};

static int mcafeeDriversLength = sizeof(mcafeeDrivers) / sizeof(mcafeeDrivers[0]);

static int conflictsArrayLength;
static DriverConflict* conflicts = NULL;

static void initializeDriverConflictArray() {
    int totalLength = 0;

    totalLength += avastDriversLength; // Avast.
    totalLength += 2; // BlueCoat, CleanInternet.
    totalLength += mcafeeDriversLength; // McAfee

    conflicts = new DriverConflict[totalLength];

    // AVAST

    int i, j;
    for (i = 0, j = 0; i < avastDriversLength; i++, j++) {
        conflicts[j].conflict = CONFLICT_REASON_AVAST;
        conflicts[j].name = avastDrivers[i];
    }

    // BlueCoat TODO
    conflicts[j].conflict = CONFLICT_REASON_BLUECOAT;
    conflicts[j].name = TEXT("bcua-wfp.sys");
    j++;
    
    // CleanInternet
    conflicts[j].conflict = CONFLICT_REASON_CLEANINTERNET;
    conflicts[j].name = TEXT("wacdrvnt.sys");
    j++;

    // McAfee
    for (i = 0; i < mcafeeDriversLength; i++, j++) {
        conflicts[j].conflict = CONFLICT_REASON_MCAFEE;
        conflicts[j].name = mcafeeDrivers[i];
    }

    // Eset

    // AVG

    conflictsArrayLength = totalLength;
}

int SearchConflictReason() {
    if (conflicts == NULL) {
        initializeDriverConflictArray();
    }

    int fnRet = CONFLICT_NO_CONFLICT;

    LPVOID* driverList = new LPVOID[MAX_DEVICE_DRIVERS_ENUM];
    DWORD driverListSize = sizeof(LPVOID) * MAX_DEVICE_DRIVERS_ENUM;
    DWORD recvDriverListSize = 0;

    BOOL ret = EnumDeviceDrivers(driverList, driverListSize, &recvDriverListSize);

    if (!ret) {
        fnRet = CONFLICT_REASON_FAILED;
        goto cleanup;
    }

    if (recvDriverListSize > driverListSize) {
        delete driverList;
        driverList = (LPVOID*)new unsigned char[recvDriverListSize];
        driverListSize = recvDriverListSize;

        ret = EnumDeviceDrivers(driverList, driverListSize, &recvDriverListSize);

        if (!ret) {
            fnRet = CONFLICT_REASON_FAILED;
            goto cleanup;
        }
    }

    driverListSize /= sizeof(LPVOID);

    TCHAR driverName[ARRAY_SIZE];

    // Now that we got our list of device drivers, enumerate them to find the conflicting drivers.
    int i = 0;
    for (i = 0; i < driverListSize; i++) {
        if (GetDeviceDriverBaseName(driverList[i], driverName, sizeof(driverName) / sizeof(driverName[0]))) {
            int j = 0;
            for (j = 0; j < conflictsArrayLength; j++) {
                if (!wcscmp(driverName, conflicts[j].name)) {
                    fnRet = conflicts[j].conflict;
                    goto cleanup;
                }
            }
        }
    }

    // TODO: Check for services here.

    fnRet = CONFLICT_NO_CONFLICT;

cleanup:
    return fnRet;
}

int FindWFPDriverConflicts() {
    return 0; // TODO: Build this yet.
}