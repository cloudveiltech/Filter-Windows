using Citadel.Core.Windows.Util;
using CloudVeilGUI.Platform.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeilGUI.Platform.Windows
{
    public class WindowsFilterStarter : IFilterStarter
    {
        public void StartFilter()
        {
            bool mainServiceViable = true;
            try
            {
                var sc = new ServiceController("FilterServiceProvider");

                switch(sc.Status)
                {
                    case ServiceControllerStatus.Stopped:
                    case ServiceControllerStatus.StopPending:
                        mainServiceViable = false;
                        break;
                }
            }
            catch(Exception ex)
            {
                mainServiceViable = false;
            }

            if(!mainServiceViable)
            {
                try
                {
                    ProcessStartInfo startupInfo = new ProcessStartInfo();
                    startupInfo.FileName = "FilterAgent.Windows.exe";
                    startupInfo.Arguments = "start";
                    startupInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    startupInfo.Verb = "runas";
                    startupInfo.CreateNoWindow = true;
                    Process.Start(startupInfo);
                }
                catch(Exception ex)
                {
                    LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), ex);
                }
            }
        }
    }
}
