using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using CloudVeilInstallerUI.ViewModels;

namespace CloudVeilInstallerUI
{
    public class Services
    { 
        public Services(CloudVeilBootstrapper ba)
        {
            bootstrapper = ba;
        }

        private CloudVeilBootstrapper bootstrapper;

        public bool Exists(string name)
        {
            try
            {
                ServiceController sc = new ServiceController(name);
                string _placeholder = sc.DisplayName; // This is a decent way to trigger an exception if the service does not exist.
                return true;
            }
            catch(InvalidOperationException)
            {
                return false;
            }
        }

        public void Start(string name)
        {
            try
            {
                ServiceController sc = new ServiceController(name);
                if(sc.Status == ServiceControllerStatus.Stopped)
                {
                    sc.Start();
                }
            }
            catch(Exception ex)
            {
                bootstrapper.Engine.Log(Microsoft.Tools.WindowsInstallerXml.Bootstrapper.LogLevel.Error, $"Error occurred in Services.Start(): {ex}");
            }
        }
    }
}
