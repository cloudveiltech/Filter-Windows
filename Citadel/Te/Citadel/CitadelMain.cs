/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Microsoft.VisualBasic.ApplicationServices;
using NLog;
using System;
using System.IO;
using Te.Citadel.Util;

namespace Te.Citadel
{
    /// <summary>
    /// Various exit codes indicating the reason for a shutdown.
    /// </summary>
    public enum ExitCodes : int
    {
        ShutdownWithSafeguards,
        ShutdownWithoutSafeguards = 100,
    }

    /// <summary>
    /// Enforces that only a single instance of this application can be run at any given time.
    /// </summary>
    public class SingleAppInstanceManager : WindowsFormsApplicationBase
    {
        private CitadelApp m_app;

        /// <summary>
        /// </summary>
        public SingleAppInstanceManager()
        {
            IsSingleInstance = true;
        }

        /// <summary>
        /// </summary>
        /// <param name="eventArgs">
        /// </param>
        /// <returns>
        /// </returns>
        protected override bool OnStartup(StartupEventArgs eventArgs)
        {
            m_app = new CitadelApp();
            m_app.InitializeComponent();
            m_app.Run();
            return false;
        }
        
        /// <summary>
        /// </summary>
        /// <param name="eventArgs">
        /// </param>
        protected override void OnStartupNextInstance(StartupNextInstanceEventArgs eventArgs)
        {
            base.OnStartupNextInstance(eventArgs);
            m_app.BringAppToFocus();
        }
    }

    public static class CitadelMain
    {
        public static Logger MainLogger;

        /// <summary>
        /// </summary>
        /// <param name="args">
        /// </param>
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                // Let's always overwrite the NLog config with our packed version to ensure
                // that this doesn't get screwed or tampered easily.\
                var nlogCfgPath = AppDomain.CurrentDomain.BaseDirectory + @"NLog.config";
                
                var nlogCfgUri = new Uri("pack://application:,,,/Resources/NLog.config");
                var resourceStream = System.Windows.Application.GetResourceStream(nlogCfgUri);
                TextReader tsr = new StreamReader(resourceStream.Stream);
                var nlogConfigText = tsr.ReadToEnd();
                resourceStream.Stream.Close();
                resourceStream.Stream.Dispose();
                File.WriteAllText(nlogCfgPath, nlogConfigText);

                MainLogger = LoggerUtil.GetAppWideLogger();
            }
            catch
            {
                // What can be done? WHAT. CAN. BE. DONE!?!?! X(
            }

            try
            {
                SingleAppInstanceManager appManager = new SingleAppInstanceManager();
                appManager.Run(args);
            }
            catch(Exception e)
            {
                MainLogger = LoggerUtil.GetAppWideLogger();
                LoggerUtil.RecursivelyLogException(MainLogger, e);
            }

            // No matter what, always ensure that critical flags are removed from our process before
            // exiting.
            ProcessProtection.Unprotect();
        }
    }
}