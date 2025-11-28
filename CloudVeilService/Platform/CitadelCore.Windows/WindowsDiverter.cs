/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeilCore.Extensions;
using CloudVeilCore.Net.Proxy;
using CloudVeilCore.Windows.WinAPI;
using Filter.Platform.Common.Util;
using Sentry.Protocol;
using Swan;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using WinDivertSharp;
using WinDivertSharp.WinAPI;

namespace CloudVeilCore.Windows.Diversion
{
    public class WindowsDiverter
    {
        private NLog.Logger logger;

        private ushort proxyPort;        
        private volatile bool isRunning = false;
        private readonly object startStopLock = new object();
        private IntPtr diversionHandle = IntPtr.Zero;
        private List<Thread> diversionThreads = new List<Thread>();

        private static readonly IntPtr s_InvalidHandleValue = new IntPtr(-1);
        public bool IsRunning
        {
            get
            {
                return isRunning;
            }
        }

        public WindowsDiverter()
        {
            logger = LoggerUtil.GetAppWideLogger();
        }

        public void UpdatePorts(ushort proxyPort)
        {
            this.proxyPort = (ushort)proxyPort;
            WinDivert.WinDivertSetParam(diversionHandle, WinDivertParam.ProxyPort, proxyPort);
        }

        public delegate void StartEventHandler();

        /// <summary>
        /// Starts the packet diversion with the given number of threads.
        /// </summary>
        /// <param name="numThreads">
        /// The number of threads to use for diversion. If equal to or less than zero, will default
        /// to Environment.ProcessorCount.
        /// </param>
        /// <remarks>
        /// The number of threads ought not to exceed Environment.ProcessorCount but this is not
        /// enforced with a bounds check.
        /// </remarks>
        public void Start(StartEventHandler startHandler = null)
        {
            lock (startStopLock)
            {
                if (isRunning)
                {
                    return;
                }
                string mainFilterString = "outbound and !loopback and (tcp or (udp and (remotePort == 80 or remotePort == 443)))";
                diversionHandle = WinDivert.WinDivertOpen(mainFilterString, WinDivertLayer.Redirect, -1000, 0);

                if (diversionHandle == s_InvalidHandleValue || diversionHandle == IntPtr.Zero)
                {
                    // Invalid handle value.
                    throw new Exception(string.Format("Failed to open main diversion handle. Got Win32 error code {0}.", Marshal.GetLastWin32Error()));
                }


                // Set everything to maximum values.
                WinDivert.WinDivertSetParam(diversionHandle, WinDivertParam.QueueLen, 16384);
                WinDivert.WinDivertSetParam(diversionHandle, WinDivertParam.QueueTime, 8000); 
                WinDivert.WinDivertSetParam(diversionHandle, WinDivertParam.QueueSize, 33554432);
                WinDivert.WinDivertSetParam(diversionHandle, WinDivertParam.ProxyPort, proxyPort);
                WinDivert.WinDivertSetParam(diversionHandle, WinDivertParam.ProxyPid, (ulong)Process.GetCurrentProcess().Id);
                isRunning = true;

                diversionThreads.Add(new Thread(() =>
                {
                    RunDiversion();
                }));

                diversionThreads.Last().Start();

                startHandler?.Invoke();
            }
        }
        
        public void CleanApplist()
        {
            if (IsRunning)
            {
                logger.Info("Cleaning app list");
                WinDivert.WinDivertSetParam(diversionHandle, WinDivertParam.CleanApps, 0);
            }
        }

        public void AddWhiteListedApp(string appName, Process[] allRunningProcesses)
        {
            if (IsRunning)
            {
                // logger.Info("Whitelisted: " + appName);
                var appNameTruncated = appName.ToLower().Replace(".exe", "");
                foreach (var process in allRunningProcesses)
                {
                    if (process.ProcessName.ToLower().Contains(appNameTruncated))
                    {
                        //   logger.Info("Whitelisted: id " + process.Id);
                        WinDivert.WinDivertAddWhitelistedPID(diversionHandle, (ulong)process.Id);
                    }
                }
                WinDivert.WinDivertAddWhitelistedApp(diversionHandle, appName);
            }
        }

        public void AddBlackListedApp(string appName, Process[] allRunningProcesses)
        {
            if (IsRunning)
            {
               // logger.Info("Blacklisted: " + appName);
                var appNameTruncated = appName.ToLower().Replace(".exe", "");
                foreach (var process in allRunningProcesses)
                {
                    if (process.ProcessName.ToLower().Contains(appNameTruncated))
                    {
                        WinDivert.WinDivertAddBlacklistedPID(diversionHandle, (ulong)process.Id);
                    }
                }
                WinDivert.WinDivertAddBlacklistedApp(diversionHandle, appName);
            }
        }

        public void AddBlockedApp(string appName, Process[] allRunningProcesses)
        {
            if (IsRunning)
            {
                //  logger.Info("Blocked: " + appName);
                var appNameTruncated = appName.ToLower().Replace(".exe", "");
                foreach (var process in allRunningProcesses)
                {
                    if (process.ProcessName.ToLower().Contains(appNameTruncated))
                    {
                        WinDivert.WinDivertAddBlockedPID(diversionHandle, (ulong)process.Id);
                    }
                }
                WinDivert.WinDivertAddBlockedApp(diversionHandle, appName);
            }
        }
        

        private unsafe void RunDiversion()
        {
            var packet = new WinDivertBuffer();
            var addr = new WinDivertAddress();
            uint recvLength = 0;

            NativeOverlapped recvOverlapped;

            IntPtr recvEvent = IntPtr.Zero;
            recvEvent = WinDivertSharp.WinAPI.Kernel32.CreateEvent(IntPtr.Zero, false, false, IntPtr.Zero);

            if (recvEvent == IntPtr.Zero || recvEvent == new IntPtr(-1))
            {
                logger.Error("Failed to initialize receive IO event.");
                return;
            }

            uint recvAsyncIoLen = 0;

            Span<byte> payloadBufferPtr = null;

            while (isRunning)
            {
                try
                {
                    payloadBufferPtr = null;

                    recvLength = 0;
                    addr.Reset();
                    recvAsyncIoLen = 0;

                    recvOverlapped = new NativeOverlapped();
                    WinAPI.Kernel32.ResetEvent(recvEvent);

                    recvOverlapped.EventHandle = recvEvent;
                    uint addrLen = (uint)sizeof(WinDivertAddress);

                    if (!WinDivert.WinDivertRecvEx(diversionHandle, packet, ref recvLength, 0, ref addr, ref addrLen, ref recvOverlapped))
                    {
                        var error = Marshal.GetLastWin32Error();

                        // 997 == ERROR_IO_PENDING
                        if (error != 997)
                        {
                            logger.Warn(string.Format("Unknown IO error ID {0}while awaiting overlapped result.", error));
                            continue;
                        }

                        // 258 == WAIT_TIMEOUT
                        while (isRunning && WinDivertSharp.WinAPI.Kernel32.WaitForSingleObject(recvEvent, 1000) == (uint)WaitForSingleObjectResult.WaitTimeout)
                        {
                           // logger.Info("Diverter: wait");
                        }

                        if (!WinDivertSharp.WinAPI.Kernel32.GetOverlappedResult(diversionHandle, ref recvOverlapped, ref recvAsyncIoLen, false))
                        {
                            logger.Warn("Failed to get overlapped result.");
                            continue;
                        }

                        recvLength = recvAsyncIoLen;
                    }
                    
                    var localPort = (int)IPAddress.HostToNetworkOrder((short)addr.LocalPort);
                    var remotePort = (int)IPAddress.HostToNetworkOrder((short)addr.RemotePort);

                    List<byte> ipBytes = new List<byte>();
                    if (addr.RemoteAddr1 != 0 || addr.RemoteAddr2 != 0 || addr.RemoteAddr3 != 0 || addr.RemoteAddr4 != 0) 
                    {
                        ipBytes.AddRange(BitConverter.GetBytes(addr.RemoteAddr1));
                    }
                    if ((addr.RemoteAddr2 != 0 && addr.RemoteAddr2 != UInt16.MaxValue) || addr.RemoteAddr3 != 0 || addr.RemoteAddr4 != 0) {                     
                        ipBytes.AddRange(BitConverter.GetBytes(addr.RemoteAddr2));
                    }
                    if (addr.RemoteAddr3 != 0 || addr.RemoteAddr4 != 0)
                    {
                        ipBytes.AddRange(BitConverter.GetBytes(addr.RemoteAddr3));
                    }
                    if (addr.RemoteAddr4 != 0)
                    {
                        ipBytes.AddRange(BitConverter.GetBytes(addr.RemoteAddr4));
                    }

                    ipBytes.Reverse();
                    var ip = new IPAddress(ipBytes.ToArray());
                    var port = (int)IPAddress.HostToNetworkOrder((short)remotePort);

                    GoproxyWrapper.GoProxy.Instance.SetDestPortForLocalPort(localPort, remotePort, ip.ToString());
                }
                catch (Exception loopException)
                {
                    logger.Error(loopException);
                }
            } 
        }

        /// <summary>
        /// If running, stops the diversion process and disposes of diversion handles.
        /// </summary>
        public void Stop()
        {
            lock (startStopLock)
            {
                if (!isRunning)
                {
                    return;
                }

                isRunning = false;

                foreach (var dt in diversionThreads)
                {
                    dt.Join();
                }

                WinDivert.WinDivertClose(diversionHandle);
            }
        }
    }
}