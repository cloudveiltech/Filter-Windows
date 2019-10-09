using Citadel.Core.Windows.Util.Update;
using Filter.Platform.Common;
using System;
using System.IO;
using FilterNativeWindows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Citadel.Core.Windows
{
    public class WindowsFilterUpdater : IFilterUpdater

    {
        public WindowsFilterUpdater()
        {
            logger = Filter.Platform.Common.Util.LoggerUtil.GetAppWideLogger();
        }

        private NLog.Logger logger;

        private void beginInstallUpdateExe(ApplicationUpdate update,bool restartApplication = true)
        {
            if (!File.Exists(update.UpdateFileLocalPath))
            {
                throw new Exception("Target update installer does not exist at the expected location.");
            }

            var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var email = PlatformTypes.New<IAuthenticationStorage>().UserEmail;
            var fingerPrint = FingerprintService.Default.Value;
            var userId = email + ":" + fingerPrint;
            string filename, args;
            if (restartApplication)
            {
                string executingProcess = Process.GetCurrentProcess().MainModule.FileName;

                filename = update.UpdateFileLocalPath;
                args = $"\"{filename}\" /upgrade /passive /waitforexit /userid={userId}"; // The /waitforexit argument makes sure FilterServiceProvider.exe is stopped before displaying its UI.
            }
            else
            {
                filename = update.UpdateFileLocalPath;
                args = $"\"{filename}\" /upgrade /passive /waitforexit /userid={userId}";
            }

            try
            {
                if (!ProcessCreation.CreateElevatedProcessInCurrentSession(filename, args))
                {
                    logger.Error($"Failed to create elevated process with {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                }
            } catch(Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void beginInstallUpdateDelayedMsi(ApplicationUpdate update, int secondDelay = 5, bool restartApplication = true)
        {
            // TODO: Implement cross platform stuff.

            if (!File.Exists(update.UpdateFileLocalPath))
            {
                throw new Exception("Target update installer does not exist at the expected location.");
            }

            ProcessStartInfo updaterStartupInfo;

            var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);

            if (restartApplication)
            {
                var executingProcess = Process.GetCurrentProcess().MainModule.FileName;
                var args = string.Format("\"{0}\\cmd.exe\" /C TIMEOUT {1} && \"{2}\\msiexec\" /I \"{3}\" {4} && \"{5}\"", systemFolder, secondDelay, systemFolder, update.UpdateFileLocalPath, update.UpdaterArguments, executingProcess);
                Console.WriteLine(args);
                updaterStartupInfo = new ProcessStartInfo(args);
            }
            else
            {
                var args = string.Format("\"{0}\\cmd.exe\" /C TIMEOUT {1} && \"{2}\\msiexec\" /I \"{3}\" {4}", systemFolder, secondDelay, systemFolder, update.UpdateFileLocalPath, update.UpdaterArguments);
                Console.WriteLine(args);
                updaterStartupInfo = new ProcessStartInfo(args);
            }

            updaterStartupInfo.UseShellExecute = false;
            //updaterStartupInfo.WindowStyle = ProcessWindowStyle.Hidden;
            updaterStartupInfo.CreateNoWindow = true;
            updaterStartupInfo.Arguments = update.UpdaterArguments;
            Process.Start(updaterStartupInfo);
        }


        /// <summary>
        /// Begins the external installation after a N second delay specified. 
        /// </summary>
        /// <param name="secondDelay">
        /// The number of seconds to wait before starting the actual update.
        /// </param>
        /// <exception cref="Exception">
        /// If the file designated at UpdateFileLocalPath does not exist at the time of this call,
        /// this method will throw.
        /// </exception>
        public void BeginInstallUpdate(ApplicationUpdate update, bool restartApplication = true)
        {
            switch (update.Kind)
            {
                case UpdateKind.InstallerPackage:
                    beginInstallUpdateDelayedMsi(update, 5, restartApplication);
                    break;

                case UpdateKind.ExecutablePackage:
                    beginInstallUpdateExe(update, restartApplication);
                    break;
            }
        }
    }
}
