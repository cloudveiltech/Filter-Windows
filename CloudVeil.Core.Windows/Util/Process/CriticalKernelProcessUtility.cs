﻿/*
* Copyright � 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Gui.CloudVeil.Util
{
    /// <summary>
    /// Class responsible for exposing undocumented functionality making the host process unkillable.
    /// </summary>
    public static class CriticalKernelProcessUtility
    {
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern void RtlSetProcessIsCritical(UInt32 v1, UInt32 v2, UInt32 v3);

        /// <summary>
        /// Flag for maintaining the state of protection.
        /// </summary>
        private static volatile bool isProtected = false;

        /// <summary>
        /// For synchronizing our current state.
        /// </summary>
        private static ReaderWriterLockSlim isProtectedLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Gets whether or not the host process is currently protected.
        /// </summary>
        public static bool IsMyProcessKernelCritical
        {
            get
            {
                try
                {
                    isProtectedLock.EnterReadLock();

                    return isProtected;
                }
                finally
                {
                    isProtectedLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// If not alreay protected, will make the host process a system-critical process so it
        /// cannot be terminated without causing a shutdown of the entire system.
        /// </summary>
        public static void SetMyProcessAsKernelCritical()
        {
            try
            {
                isProtectedLock.EnterWriteLock();

                if(!isProtected)
                {
                    System.Diagnostics.Process.EnterDebugMode();
                    RtlSetProcessIsCritical(1, 0, 0);
                    isProtected = true;
                }
            }
            finally
            {
                isProtectedLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// If already protected, will remove protection from the host process, so that it will no
        /// longer be a system-critical process and thus will be able to shut down safely.
        /// </summary>
        public static void SetMyProcessAsNonKernelCritical()
        {
            try
            {
                isProtectedLock.EnterWriteLock();

                if(isProtected)
                {
                    RtlSetProcessIsCritical(0, 0, 0);
                    isProtected = false;
                }
            }
            finally
            {
                isProtectedLock.ExitWriteLock();
            }
        }
    }
}