#pragma once

#include "ConflictReason.h"

using namespace System;
using namespace System::Collections::Generic;

unsigned int GetActiveSessionId();
int SearchConflictReason(int** arr);
int CreateProcessInCurrentSession(wchar_t* filename, wchar_t* cmdline, int winlogonPid);

namespace FilterNativeWindows {
	public ref class ConflictDetection
	{
    public:
        static List<ConflictReason>^ SearchConflictReason();

	};

    public ref class ProcessCreation
    {
    public:
        static bool CreateElevatedProcessInCurrentSession(String^ filename, String^ arguments);
    };
}
