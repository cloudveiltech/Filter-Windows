using Filter.Platform.Common.Client;
using System;
using System.Linq;
using System.Diagnostics;
using Citadel.Core.WinAPI;

namespace Citadel.Core.Windows.Client
{
    public class WindowsGUIChecks : IGUIChecks
    {
        public void DisplayExistingUI()
        {
            var thisProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(thisProcess.ProcessName).Where(p => p.Id != thisProcess.Id);

            foreach (Process runningProcess in processes)
            {
                foreach (var handle in WindowHelpers.EnumerateProcessWindowHandles(runningProcess.Id))
                {
                    // Send window show.
                    WindowHelpers.SendMessage(handle, (uint)WindowMessages.SHOWWINDOW, 9, 0);
                }
            }
        }

        public bool IsInIsolatedSession()
        {
            return Process.GetCurrentProcess().SessionId <= 0;
        }
    }
}
