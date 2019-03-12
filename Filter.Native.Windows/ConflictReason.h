#pragma once

using namespace System;

#define CONFLICT_REASON_FAILED -1
#define CONFLICT_REASON_BLUECOAT 1
#define CONFLICT_REASON_CLEANINTERNET 2
#define CONFLICT_REASON_MCAFFEE 3
#define CONFLICT_REASON_ESET 4
#define CONFLICT_REASON_AVAST 5
#define CONFLICT_REASON_AVG 6

namespace FilterNativeWindows {
    public enum ConflictReason {
        Failed = CONFLICT_REASON_FAILED,
        Bluecoat = CONFLICT_REASON_BLUECOAT,
        CleanInternet = CONFLICT_REASON_CLEANINTERNET,
        McAffee = CONFLICT_REASON_MCAFFEE,
        Eset = CONFLICT_REASON_ESET,
        Avast = CONFLICT_REASON_AVAST,
        AVGEnhancedFirewall = CONFLICT_REASON_AVG
    };
}