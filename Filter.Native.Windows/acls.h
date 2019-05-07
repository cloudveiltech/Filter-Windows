#pragma once

#include <Windows.h>
#include <AccCtrl.h>
#include <AclAPI.h>

#define ERROR_WAYPOINT_1 1
#define ERROR_WAYPOINT_2 2
#define ERROR_WAYPOINT_3 3
#define ERROR_WAYPOINT_4 4

#define ACL_INSUFFICIENT_MEMORY 2L
#define ACL_UNKNOWN_ERROR 3L

int SetObjectAsSystemOnly(LPWSTR objectName, SE_OBJECT_TYPE objectType);

SC_HANDLE GetServiceHandleFromName(wchar_t* name);